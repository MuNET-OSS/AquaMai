using System;
using System.Collections.Generic;
using System.Threading;
using Comio;
using MelonLoader;
using AquaMai.Mods.GameSystem.Lib;

namespace AquaMai.Mods.GameSystem;

/// <summary>
/// NPro 自定义固件的 GAME 管道设备（WinUSB）。
///
/// 与 ADX/IO4 的 HID 方案并列，由 <see cref="AdxHidInput"/> 统一调度，
/// 因此对一个玩家槽位来说"要么 HID，要么 WinUSB"，上层输入逻辑不变。
///
/// 两个方向：
///  - 上行：8 字节输入报告（touch[6] + buttons + ext_buttons），
///    bulk 是字节流，一次 Read 可能返回多笔报告的拼接，必须按 8 字节重组。
///  - 下行：从游戏的串口发送层抓取原始 SEGA 灯板包并原样转发，fade 交给板子插值，
///    因此与真机行为一致；静止时用 SetLedGsUpdate 保活，避免固件
///    LED_CONTROL_TIMEOUT_MS(5s) 超时后回落到 idle 灯效。
/// </summary>
public sealed class NproDevice : IDisposable
{
    private static readonly Guid GameInterfaceGuid = new("7F2A9C41-5E3B-4D8A-9F16-3C7E5B2D9A48");

    // SEGA 灯板协议（Comio.BD15070_4）
    private const byte Sync = 0xE0;
    private const byte Escape = 0xD0;
    private const byte DstNodeId = 0x11;
    private const byte SrcNodeId = 0x01;
    private const int InputReportLength = 8;

    private const byte CmdSetLedGs8Bit = 0x31;
    private const byte CmdSetLedGs8BitMulti = 0x32;
    private const byte CmdSetLedGs8BitMultiFade = 0x33;
    private const byte CmdSetLedFet = 0x39;
    private const byte CmdSetLedGsUpdate = 0x3C;

    // 静止保活间隔：必须明显小于固件 5s 超时。
    private const int KeepAliveIntervalMs = 1000;

    // net472 没有 Environment.TickCount64；用单调的 Stopwatch 计时。
    private static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();

    private readonly int _player;
    private readonly object _writeLock = new();
    private readonly Action<ulong, byte, byte> _onInput;

    private WinUsbIo.Device _device;
    private Thread _readThread;
    private volatile bool _stopping;

    private long _lastKeepAliveTicks;

    public bool IsConnected
    {
        get
        {
            var device = Volatile.Read(ref _device);
            return device != null && device.IsOpen;
        }
    }

    public NproDevice(int player, Action<ulong, byte, byte> onInput)
    {
        _player = player;
        _onInput = onInput;
    }

    #region 连接管理

    /// <summary>尝试枚举并打开 GAME 管道。成功返回 true。</summary>
    public bool TryConnect()
    {
        if (IsConnected) return true;

        var targetPid = _player == 0 ? 0x5751 : 0x5752;
        List<WinUsbIo.DevicePath> paths;
        try
        {
            paths = WinUsbIo.EnumerateWinUsbInterfaces(
                (ushort)0x2E3C, (ushort)targetPid, 0, "NoronDX GAME", GameInterfaceGuid);
        }
        catch (Exception e)
        {
            MelonLogger.Msg($"[NPro] 枚举 GAME 接口失败: {e.Message}");
            return false;
        }

        foreach (var path in paths)
        {
            // 1P/2P 靠 PID 区分：HID 接口已移除，字符串描述符不再带 1P/2P 之外的信息。
            if (path.Pid != targetPid) continue;

            try
            {
                var device = WinUsbIo.Device.Open(path.Path);
                Interlocked.Exchange(ref _device, device)?.Dispose();
                _stopping = false;
                _lastKeepAliveTicks = Clock.ElapsedMilliseconds;
                StartReadThread(device);
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Msg($"[NPro] 打开 GAME 管道失败: {e.Message}");
                Interlocked.Exchange(ref _device, null)?.Dispose();
            }
        }

        return false;
    }

    private void StartReadThread(WinUsbIo.Device device)
    {
        _readThread = new Thread(() => ReadLoop(device))
            { IsBackground = true, Name = $"NPro-Read-{_player + 1}P" };
        _readThread.Start();
    }

    private void ReadLoop(WinUsbIo.Device device)
    {
        var buffer = new byte[256];
        var pending = new List<byte>(512);

        while (!_stopping && ReferenceEquals(Volatile.Read(ref _device), device))
        {
            if (!device.IsOpen) break;

            int read;
            try
            {
                read = device.Read(buffer, 0, buffer.Length, 200);
            }
            catch
            {
                break;
            }

            if (read < 0) break; // 设备断开
            if (read == 0) continue; // 超时，重新挂一个 pending 读

            for (var i = 0; i < read; i++) pending.Add(buffer[i]);

            // bulk 是字节流：按固定 8 字节重组，避免把多笔报告当成一笔。
            while (pending.Count >= InputReportLength)
            {
                ulong touch = 0;
                for (var i = 0; i < 6; i++) touch |= (ulong)pending[i] << (i * 8);
                var buttons = pending[6];
                var ext = pending[7];
                pending.RemoveRange(0, InputReportLength);
                PushInput(touch, buttons, ext);
            }
        }

        if (!_stopping) MarkDisconnected(device);
    }

    private void PushInput(ulong touch, byte buttons, byte extButtons)
    {
        _onInput?.Invoke(touch, buttons, extButtons);
    }

    public void Disconnect()
    {
        _stopping = true;
        var device = Interlocked.Exchange(ref _device, null);
        device?.Abort();
        device?.Dispose();
        _readThread = null;
    }

    private void MarkDisconnected(WinUsbIo.Device device)
    {
        if (!ReferenceEquals(Interlocked.CompareExchange(ref _device, null, device), device)) return;
        device.Dispose();
    }

    #endregion

    #region 灯光

    /// <summary>
    /// 周期性调用：静止时发保活，保证固件不会因 LED 通道静默而回落到 idle 灯效。
    /// </summary>
    public void TickKeepAlive()
    {
        if (!IsConnected) return;

        var now = Clock.ElapsedMilliseconds;
        if (now - _lastKeepAliveTicks < KeepAliveIntervalMs) return;
        _lastKeepAliveTicks = now;

        // 幂等、无视觉副作用，正好用作保活。
        SendCommand(CmdSetLedGsUpdate, Array.Empty<byte>());
    }

    /// <summary>转发游戏串口层已经组好的灯板请求。</summary>
    public void SendLedPacket(Packet packet)
    {
        if (packet == null || packet.Count < 6 || packet[0] != Sync) return;
        if (packet[3] + 5 != packet.Count) return;

        var command = packet[4];
        if (command != CmdSetLedGs8Bit &&
            command != CmdSetLedGs8BitMulti &&
            command != CmdSetLedGs8BitMultiFade &&
            command != CmdSetLedFet &&
            command != CmdSetLedGsUpdate)
        {
            return;
        }

        var payload = new byte[packet.Count - 6];
        for (var i = 0; i < payload.Length; i++)
        {
            payload[i] = packet[5 + i];
        }
        SendCommand(command, payload);
    }

    #endregion

    #region SEGA 组帧

    private void SendCommand(byte command, byte[] payload)
    {
        var device = Volatile.Read(ref _device);
        if (device == null || !device.IsOpen) return;

        // 明文帧：E0 | dst | src | len | cmd | payload | sum
        // len = 命令字 + payload 长度；sum = bytes[1 .. n-2] 之和。
        var plain = new byte[5 + payload.Length + 1];
        plain[0] = Sync;
        plain[1] = DstNodeId;
        plain[2] = SrcNodeId;
        plain[3] = (byte)(1 + payload.Length);
        plain[4] = command;
        Buffer.BlockCopy(payload, 0, plain, 5, payload.Length);

        var sum = 0;
        for (var i = 1; i < plain.Length - 1; i++) sum += plain[i];
        plain[plain.Length - 1] = (byte)sum;

        // 转义后写入；最大 76 字节上限由协议保证（这里最长 13 字节）。
        var encoded = new byte[plain.Length * 2 + 1];
        var n = 1;
        encoded[0] = Sync;
        for (var i = 1; i < plain.Length; i++)
        {
            if (plain[i] == Sync || plain[i] == Escape)
            {
                encoded[n++] = Escape;
                encoded[n++] = (byte)(plain[i] - 1);
            }
            else
            {
                encoded[n++] = plain[i];
            }
        }

        var frame = new byte[n];
        Buffer.BlockCopy(encoded, 0, frame, 0, n);
        lock (_writeLock)
        {
            try
            {
                if (device.Write(frame, 0, frame.Length, 100) != frame.Length)
                    MarkDisconnected(device);
            }
            catch
            {
                MarkDisconnected(device);
            }
        }
    }

    #endregion

    public void Dispose()
    {
        Disconnect();
    }
}
