#nullable enable

using System;

using System.Linq;
using System.Diagnostics;
using System.Threading;
using HidLibrary;
using MelonLoader;
using UnityEngine;
using AquaMai.Mods.GameSystem.Lib;

namespace AquaMai.Mods.GameSystem.MaimollerIO.Libs;

public class MaimollerDeviceNative : IMaimollerDevice
{
    private const int VID = 0x0E8F;
    private const int PID = 0x1224;

    private const int ButtonBitOffset = 34;
    private const int SystemBitOffset = 42;
    private const ulong TouchMask = (1UL << 34) - 1; // bits 0-33
    // 固件静默多久会回落到待机灯是实测值, 超过这个间隔写线程就补发一次
    private const int OutputKeepAliveMs = 500;
    private static readonly long OutputKeepAliveTicks = Stopwatch.Frequency * OutputKeepAliveMs / 1000;

    private readonly int _player;

    private volatile HidDevice? _device;
    private byte[]? _readBuffer; // pre-allocated, sized from device capabilities
    private bool _hidThreadRunning;
    private long _lastWriteTick; // 只在写线程里访问
    private volatile bool _connected;

    private readonly InputLatch _inputLatch = new();
    private readonly MaimollerOutputReport _output = new(); // 只有主线程会修改
    private readonly MaimollerLedManager _ledManager;

    // HID 写入全部交给写线程, 这把锁只包住 64 字节快照的拷贝和比较, 不包 HID I/O
    private readonly object _outputSync = new();
    private readonly byte[] _scratch = new byte[64];    // 主线程私有
    private readonly byte[] _published = new byte[64];  // 最新已发布的状态
    private readonly byte[] _sendBuffer = new byte[64]; // 写线程私有
    private bool _outputDirty;


    public MaimollerDeviceNative(int player)
    {
        _player = player;
        _ledManager = new MaimollerLedManager(_output);
    }

    public void Open()
    {
        if (_hidThreadRunning) throw new InvalidOperationException($"MaimollerDevice {_player + 1}P already opened");

        if (!TryConnectDevice())
        {
            MelonLogger.Warning($"[MaimollerDevice] {_player + 1}P device not found");
        }

        // 启动 HID 读取线程
        _hidThreadRunning = true;
        var hidThread = new Thread(HidInputThread)
        {
            IsBackground = true
        };
        hidThread.Start();

        // 唯一的 HID 写入线程: 有变化立即发, 空闲时按 OutputKeepAliveMs 补发,
        // 避免游戏初始化/加载/卡顿期间手台 LED 通道静默回落到固件自带的待机灯
        PublishOutput();
        var outputThread = new Thread(OutputThread)
        {
            IsBackground = true
        };
        outputThread.Start();
    }

    public void Update()
    {
        if (!_hidThreadRunning) throw new InvalidOperationException($"MaimollerDevice {_player + 1}P not opened");

        PublishOutput();
    }

    #region Input

    public bool IsButtonPressed(int buttonIndex1To8)
    {
        if (buttonIndex1To8 < 1 || buttonIndex1To8 > 8) return false;
        return _inputLatch.ReadBit(ButtonBitOffset + buttonIndex1To8 - 1);
    }

    public bool IsSystemButtonPressed(SystemButton button)
    {
        return _inputLatch.ReadBit(SystemBitOffset + (int)button);
    }

    public ulong GetTouchState()
    {
        return _inputLatch.ReadBits(TouchMask);
    }

    #endregion

    #region Connection

    private bool TryConnectDevice()
    {
        var devices = HidDevices.Enumerate(VID, PID)
            .Where(d => d.DevicePath.Contains("&mi_00#"))
            .ToArray();
        if (devices.Length == 0) return false;
        foreach (var device in devices)
        {
            try
            {
                device.OpenDevice();
            }
            catch
            {
                continue;
            }
            if (!device.ReadFeatureData(out var featureData, 1) || featureData.Length <= 32)
            {
                MelonLogger.Warning($"[MaimollerDevice] Failed to read feature report from {device.DevicePath}");
                device.CloseDevice();
                continue;
            }
            int devicePlayer = featureData[4] == 2 ? 1 : 0;
            if (devicePlayer != _player)
            {
                device.CloseDevice();
                continue;
            }
            _device = device;
            _readBuffer = new byte[device.Capabilities.InputReportByteLength];
            _connected = true;
            // 重连后设备状态未知, 立即补发一次
            lock (_outputSync)
            {
                _outputDirty = true;
                System.Threading.Monitor.Pulse(_outputSync);
            }
            MelonLogger.Msg($"[MaimollerDevice] {_player + 1}P connected");
            return true;
        }
        return false;
    }
    private bool IsDeviceConnected()
    {
        return _device != null && _connected;
    }
    private void DisconnectDevice()
    {
        if (_device == null) return;
        try
        {
            _device.CloseDevice();
        }
        catch
        {
            // ignore
        }
        _connected = false;
        _device = null;
        _readBuffer = null;
        _inputLatch.Clear();
        MelonLogger.Msg($"[MaimollerDevice] {_player + 1}P disconnected");
    }
    #endregion
    #region HID Thread
    private void HidInputThread()
    {
        while (_hidThreadRunning)
        {
            while (_hidThreadRunning && !IsDeviceConnected())
            {
                Thread.Sleep(500);
                TryConnectDevice();
            }
            if (!_hidThreadRunning) break;
            var device = _device;
            if (device == null) continue;
            try
            {
                var buf = _readBuffer;
                if (buf == null) continue;
                if (!HidRawIO.Read(device, buf, out var bytesRead) || bytesRead < 8)
                {
                    DisconnectDevice();
                    continue;
                }
                ulong state = 0;
                state |= (ulong)buf[1];                    // Touch A
                state |= (ulong)buf[2] << 8;              // Touch B
                state |= (ulong)(buf[3] & 0x03) << 16;    // Touch C (2 bits)
                state |= (ulong)buf[4] << 18;              // Touch D
                state |= (ulong)buf[5] << 26;              // Touch E
                state |= (ulong)buf[6] << 34;              // Player buttons
                state |= (ulong)(buf[7] & 0x0F) << 42;    // System buttons (4 bits)
                _inputLatch.Update(state);
            }
            catch
            {
                DisconnectDevice();
            }
        }
    }
    #endregion
    #region Output
    // 主线程每帧调用: 只把当前状态发布成快照并通知写线程, 不做任何 HID I/O
    private void PublishOutput()
    {
        SerializeOutput(_scratch);
        lock (_outputSync)
        {
            if (_scratch.SequenceEqual(_published)) return;
            Array.Copy(_scratch, _published, 64);
            _outputDirty = true;
            System.Threading.Monitor.Pulse(_outputSync);
        }
    }

    // 写线程: 被 PublishOutput 唤醒或等到保活超时就发一次, 是唯一的 HID 写入方
    private void OutputThread()
    {
        while (_hidThreadRunning)
        {
            if (!IsDeviceConnected())
            {
                Thread.Sleep(OutputKeepAliveMs);
                continue;
            }

            bool due;
            lock (_outputSync)
            {
                var idle = Stopwatch.GetTimestamp() - _lastWriteTick;
                if (!_outputDirty && idle < OutputKeepAliveTicks)
                    System.Threading.Monitor.Wait(_outputSync, (int)Math.Max(1, (OutputKeepAliveTicks - idle) * 1000 / Stopwatch.Frequency));
                due = _outputDirty || Stopwatch.GetTimestamp() - _lastWriteTick >= OutputKeepAliveTicks;
                if (due) _outputDirty = false;
            }
            if (!due) continue;

            try
            {
                WriteOutputReport();
            }
            catch
            {
                // 写入失败视为断线
                DisconnectDevice();
            }
        }
    }

    private void WriteOutputReport()
    {
        var device = _device;
        if (device == null || !_connected) return;
        lock (_outputSync)
        {
            Array.Copy(_published, _sendBuffer, 64);
        }
        HidRawIO.Write(device, _sendBuffer);
        _lastWriteTick = Stopwatch.GetTimestamp();
    }

    private void SerializeOutput(byte[] buffer)
    {
        buffer[0] = 1; // report ID
        Array.Copy(_output.buttonColors, 0, buffer, 1, 24);
        buffer[25] = _output.circleBrightness;
        buffer[26] = _output.bodyBrightness;
        buffer[27] = _output.sideBrightness;
        Array.Copy(_output.billboardColor, 0, buffer, 28, 3);
        buffer[31] = (byte)_output.indicators;
    }
    #endregion
    #region LED
    public void LedPreExecute()
    {
        _ledManager.PreExecute();
        // 渐变插值在这里算完, 是每帧 LED 状态的终点, 顺便发布一次
        PublishOutput();
    }
    public void SetButtonColor(int index, Color32 color) => _ledManager.SetButtonColor(index, color);
    public void SetButtonColorFade(int index, Color32 color, long duration) => _ledManager.SetButtonColorFade(index, color, duration);
    public void SetBodyIntensity(int index, byte intensity) => _ledManager.SetBodyIntensity(index, intensity);
    public void SetBillboardColor(Color32 color) => _ledManager.SetBillboardColor(color);
    #endregion
}
