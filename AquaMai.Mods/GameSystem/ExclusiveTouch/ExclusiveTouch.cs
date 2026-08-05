
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using LibUsbDotNet.Main;
using LibUsbDotNet;
using LibUsbDotNet.WinUsb;
using MelonLoader;
using UnityEngine;
using AquaMai.Core.Helpers;
using System.Threading;
using JetBrains.Annotations;

namespace AquaMai.Mods.GameSystem.ExclusiveTouch;

public abstract class ExclusiveTouchBase(int playerNo, int vid, int pid, [CanBeNull] string serialNumber, [CanBeNull] string locationPath, byte configuration, int interfaceNumber, ReadEndpointID endpoint, int packetSize, int minX, int minY, int maxX, int maxY, bool flip, int radius,
    float aExtraRadius = 0, float bExtraRadius = 0, float cExtraRadius = 0, float dExtraRadius = 0, float eExtraRadius = 0,
    int timeoutMilliseconds = 20)
{
    private UsbDevice device;
    private readonly object deviceLock = new();
    private volatile bool stopping;
    private TouchSensorMapper touchSensorMapper;

    public bool IsConnected
    {
        get
        {
            lock (deviceLock)
            {
                return device != null;
            }
        }
    }

    protected int PlayerNo => playerNo;

    private class TouchPoint
    {
        public ulong Mask;
        public long LastUpdateTick;
        public bool IsActive;
    }

    protected readonly struct TouchUpdate
    {
        public readonly ushort X;
        public readonly ushort Y;
        public readonly int FingerId;
        public readonly bool IsPressed;

        public TouchUpdate(ushort x, ushort y, int fingerId, bool isPressed)
        {
            X = x;
            Y = y;
            FingerId = fingerId;
            IsPressed = isPressed;
        }
    }

    // [手指ID]
    private readonly TouchPoint[] allFingerPoints = new TouchPoint[256];

    // 防吃键
    private readonly InputLatch _touchLatch = new();
    private readonly object touchLock = new();

    private readonly long TouchTimeoutTicks = Stopwatch.Frequency * timeoutMilliseconds / 1000;

    private ulong _lastDiagnosticRead;
    private bool _hasDiagnosticRead;

    protected virtual string DiagnosticName => "ExclusiveTouch";

    public bool Start(bool logConnectionFailure = true)
    {
        stopping = false;
        if (!TryConnectDevice())
        {
            if (logConnectionFailure)
            {
                MelonLogger.Msg($"[ExclusiveTouch] Cannot connect {playerNo + 1}P");
            }
            return false;
        }

        try
        {
            touchSensorMapper = new TouchSensorMapper(minX, minY, maxX, maxY, radius, flip,
                aExtraRadius, bExtraRadius, cExtraRadius, dExtraRadius, eExtraRadius);

            for (int i = 0; i < 256; i++)
            {
                allFingerPoints[i] = new TouchPoint();
            }

            Thread readThread = new(ReadThread);
            readThread.IsBackground = true;
            readThread.Start();
            Application.quitting += () =>
            {
                stopping = true;
                CloseCurrentDevice();
            };
            return true;
        }
        catch (Exception e)
        {
            MelonLogger.Error($"[ExclusiveTouch] Cannot initialize {playerNo + 1}P: {e}");
            CloseCurrentDevice();
            return false;
        }
    }

    protected virtual void InitializeDevice(UsbDevice usbDevice) { }

    private UsbDeviceFinder CreateFinder()
    {
        // 方便组 2P
        if (!string.IsNullOrWhiteSpace(serialNumber))
        {
            // 优先使用序列号
            return new UsbDeviceFinder(vid, pid, serialNumber);
        }

        if (!string.IsNullOrWhiteSpace(locationPath))
        {
            // 使用位置路径匹配
            return new UsbDeviceLocationFinder(vid, pid, locationPath);
        }

        // 使用第一个匹配的设备
        return new UsbDeviceFinder(vid, pid);
    }

    private bool TryConnectDevice()
    {
        UsbDevice newDevice = null;
        try
        {
            newDevice = UsbDevice.OpenUsbDevice(CreateFinder());
            if (newDevice == null) return false;

            if (newDevice is WinUsbDevice winUsbDevice)
            {
                // 触摸屏固件不能可靠处理 WinUSB 的选择性挂起
                winUsbDevice.PowerPolicy.AutoSuspend = false;
            }

            IUsbDevice wholeDevice = newDevice as IUsbDevice;
            if (wholeDevice != null &&
                (!wholeDevice.SetConfiguration(configuration) || !wholeDevice.ClaimInterface(interfaceNumber)))
            {
                throw new InvalidOperationException("USB interface setup failed");
            }

            InitializeDevice(newDevice);
            lock (deviceLock)
            {
                if (stopping)
                {
                    CloseDevice(newDevice);
                    return false;
                }

                device = newDevice;
            }
            ExclusiveTouchDiagnostics.Log(
                "{0} player={1} connected driver={2}",
                DiagnosticName, playerNo + 1, newDevice.DriverMode);
            return true;
        }
        catch (Exception e)
        {
            MelonLogger.Error($"[ExclusiveTouch] Cannot initialize {playerNo + 1}P: {e.Message}");
            if (newDevice != null) CloseDevice(newDevice);
            return false;
        }
    }

    private void CloseCurrentDevice()
    {
        UsbDevice oldDevice;
        lock (deviceLock)
        {
            oldDevice = device;
            device = null;
        }

        if (oldDevice != null) CloseDevice(oldDevice);
    }

    private bool IsCurrentDevice(UsbDevice target)
    {
        lock (deviceLock)
        {
            return ReferenceEquals(target, device);
        }
    }

    private void CloseDevice(UsbDevice target)
    {
        try
        {
            if (target is IUsbDevice wholeDevice)
            {
                wholeDevice.ReleaseInterface(interfaceNumber);
            }
        }
        catch (Exception e)
        {
            MelonLogger.Warning($"[ExclusiveTouch] Cannot release {playerNo + 1}P interface: {e.Message}");
        }

        try
        {
            target.Close();
        }
        catch (Exception e)
        {
            MelonLogger.Warning($"[ExclusiveTouch] Cannot close {playerNo + 1}P device: {e.Message}");
        }
    }

    private void ReadThread()
    {
        byte[] buffer = new byte[packetSize];
        using var pinnedBuffer = new PinnedHandle(buffer);

        try
        {
            while (!stopping)
            {
                UsbDevice currentDevice;
                lock (deviceLock)
                {
                    currentDevice = device;
                }

                if (currentDevice == null)
                {
                    Thread.Sleep(1000);
                    TryConnectDevice();
                    continue;
                }

                try
                {
                    using var reader = currentDevice.OpenEndpointReader(endpoint);
                    while (!stopping)
                    {
                        int bytesRead;
                        ErrorCode ec = reader.Read(pinnedBuffer.Handle, 0, buffer.Length, 100, out bytesRead); // 100ms 超时

                        if (ec == ErrorCode.IoTimedOut) continue; // 超时就继续等
                        if (ec != ErrorCode.None)
                        {
                            MelonLogger.Msg($"[ExclusiveTouch] {playerNo + 1}P: 读取错误: {ec}，尝试重连");
                            ExclusiveTouchDiagnostics.Log(
                                "{0} player={1} reconnect reason=read-error code={2}",
                                DiagnosticName, playerNo + 1, ec);
                            CloseCurrentDevice();
                            break;
                        }

                        if (bytesRead > 0 && IsCurrentDevice(currentDevice))
                        {
                            OnTouchData(buffer);
                        }
                    }
                }
                catch (Exception e)
                {
                    if (stopping) break;
                    MelonLogger.Msg($"[ExclusiveTouch] {playerNo + 1}P: 读取异常: {e.Message}，尝试重连");
                    ExclusiveTouchDiagnostics.Log(
                        "{0} player={1} reconnect reason=read-exception error={2}",
                        DiagnosticName, playerNo + 1, e.Message);
                    CloseCurrentDevice();
                }
            }
        }
        finally
        {
            CloseCurrentDevice();
        }
    }

    protected abstract void OnTouchData(byte[] data);

    private void ApplyFinger(TouchUpdate update, long timestamp)
    {
        if (update.FingerId < 0 || update.FingerId >= 256) return;

        var point = allFingerPoints[update.FingerId];
        if (update.IsPressed)
        {
            point.Mask = touchSensorMapper.ParseTouchPoint(update.X, update.Y);
            point.IsActive = true;
            point.LastUpdateTick = timestamp;
        }
        else
        {
            point.IsActive = false;
        }
    }

    protected void HandleFinger(ushort x, ushort y, int fingerId, bool isPressed)
    {
        // 安全检查，防止越界
        if (fingerId < 0 || fingerId >= 256) return;
        lock (touchLock)
        {
            ApplyFinger(new TouchUpdate(x, y, fingerId, isPressed), Stopwatch.GetTimestamp());
            var state = ComputeActiveMask();
            _touchLatch.Update(state);
            if (ExclusiveTouchDiagnostics.Enabled)
            {
                ExclusiveTouchDiagnostics.Log(
                    "{0} player={1} finger={2} pressed={3} mask=0x{4:X16}",
                    DiagnosticName, playerNo + 1, fingerId, isPressed, state);
            }
        }
    }

    private void HandleUpdates(List<TouchUpdate> updates, string eventName, bool replaceState)
    {
        lock (touchLock)
        {
            var now = Stopwatch.GetTimestamp();
            if (replaceState)
            {
                for (int i = 0; i < allFingerPoints.Length; i++)
                {
                    allFingerPoints[i].IsActive = false;
                }
            }
            for (int i = 0; i < updates.Count; i++)
            {
                ApplyFinger(updates[i], now);
            }

            var state = ComputeActiveMask();
            _touchLatch.Update(state);
            if (ExclusiveTouchDiagnostics.Enabled)
            {
                ExclusiveTouchDiagnostics.Log(
                    "{0} player={1} {2} updates={3} state=0x{4:X16}",
                    DiagnosticName, playerNo + 1, eventName, updates.Count, state);
            }
        }
    }

    protected void HandleFrame(List<TouchUpdate> updates)
    {
        HandleUpdates(updates, "frame-commit", replaceState: true);
    }

    protected void HandleReleases(List<TouchUpdate> updates)
    {
        HandleUpdates(updates, "release-commit", replaceState: false);
    }

    private ulong ComputeActiveMask()
    {
        ulong mask = 0;
        for (int i = 0; i < allFingerPoints.Length; i++)
        {
            if (allFingerPoints[i].IsActive)
                mask |= allFingerPoints[i].Mask;
        }
        return mask;
    }
    internal ulong GetTouchState(int player)
    {
        if (player != playerNo) return 0;
        lock (touchLock)
        {
            var now = Stopwatch.GetTimestamp();
            var diagnosticsEnabled = ExclusiveTouchDiagnostics.Enabled;
            StringBuilder timedOut = null;
            for (int i = 0; i < allFingerPoints.Length; i++)
            {
                var point = allFingerPoints[i];
                if (point.IsActive && (now - point.LastUpdateTick) > TouchTimeoutTicks)
                {
                    point.IsActive = false;
                    if (diagnosticsEnabled)
                    {
                        timedOut ??= new StringBuilder();
                        if (timedOut.Length > 0) timedOut.Append(',');
                        timedOut.Append(i);
                    }
                }
            }
            var state = ComputeActiveMask();
            _touchLatch.Update(state);
            var result = _touchLatch.Read();
            if (diagnosticsEnabled && (timedOut != null || !_hasDiagnosticRead || result != _lastDiagnosticRead))
            {
                ExclusiveTouchDiagnostics.Log(
                    "{0} player={1} poll state=0x{2:X16} result=0x{3:X16} timeout=[{4}]",
                    DiagnosticName, playerNo + 1, state, result, timedOut);
                _lastDiagnosticRead = result;
                _hasDiagnosticRead = true;
            }
            return result;
        }
    }
}

internal static class ExclusiveTouchDiagnostics
{
    private static readonly object Sync = new();
    private static StreamWriter writer;

    public static bool Enabled => writer != null;

    public static void Configure(bool enabled)
    {
        if (!enabled) return;

        lock (Sync)
        {
            if (writer != null) return;
            try
            {
                var directory = Path.Combine(Environment.CurrentDirectory, "UserData");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "AquaMaiTouch.log");
                writer = new StreamWriter(path, append: true, Encoding.UTF8)
                {
                    AutoFlush = true
                };
                MelonLogger.Msg($"[ExclusiveTouch] Diagnostic log: {path}");
                Log("session-start");
            }
            catch (Exception e)
            {
                MelonLogger.Error($"[ExclusiveTouch] Cannot open diagnostic log: {e}");
            }
        }
    }

    public static void Log(string format, params object[] args)
    {
        lock (Sync)
        {
            if (writer == null) return;
            try
            {
                writer.WriteLine($"{DateTime.UtcNow:O} {string.Format(format, args)}");
            }
            catch (Exception e)
            {
                MelonLogger.Error($"[ExclusiveTouch] Diagnostic log write failed: {e}");
                writer.Dispose();
                writer = null;
            }
        }
    }
}
