
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using LibUsbDotNet.Main;
using LibUsbDotNet;
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
    private TouchSensorMapper touchSensorMapper;

    public bool IsConnected => device != null;

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

    public void Start()
    {
        // 方便组 2P
        UsbDeviceFinder finder;
        
        if (!string.IsNullOrWhiteSpace(serialNumber))
        {
            // 优先使用序列号
            finder = new UsbDeviceFinder(vid, pid, serialNumber);
        }
        else if (!string.IsNullOrWhiteSpace(locationPath))
        {
            // 使用位置路径匹配
            finder = new UsbDeviceLocationFinder(vid, pid, locationPath);
        }
        else
        {
            // 使用第一个匹配的设备
            finder = new UsbDeviceFinder(vid, pid);
        }
        
        device = UsbDevice.OpenUsbDevice(finder);
        if (device == null)
        {
            MelonLogger.Msg($"[ExclusiveTouch] Cannot connect {playerNo + 1}P");
        }
        else
        {
            IUsbDevice wholeDevice = device as IUsbDevice;
            if (wholeDevice != null)
            {
                wholeDevice.SetConfiguration(configuration);
                wholeDevice.ClaimInterface(interfaceNumber);
            }
            touchSensorMapper = new TouchSensorMapper(minX, minY, maxX, maxY, radius, flip,
                aExtraRadius, bExtraRadius, cExtraRadius, dExtraRadius, eExtraRadius);
            Application.quitting += () =>
            {
                var tmpDevice = device;
                device = null;
                if (wholeDevice != null)
                {
                    wholeDevice.ReleaseInterface(interfaceNumber);
                }
                tmpDevice.Close();
            };

            for (int i = 0; i < 256; i++)
            {
                allFingerPoints[i] = new TouchPoint();
            }

            Thread readThread = new(ReadThread);
            readThread.Start();
            TouchStatusProvider.RegisterTouchStatusProvider(playerNo, GetTouchState);
        }
    }

    private void ReadThread()
    {
        byte[] buffer = new byte[packetSize];
        var reader = device.OpenEndpointReader(endpoint);
        
        try
        {
            while (device != null)
            {
                int bytesRead;
                ErrorCode ec = reader.Read(buffer, 100, out bytesRead); // 100ms 超时

                if (ec != ErrorCode.None)
                {
                    if (ec == ErrorCode.IoTimedOut) continue; // 超时就继续等
                    MelonLogger.Msg($"[ExclusiveTouch] {playerNo + 1}P: 读取错误: {ec}");
                    break;
                }

                if (bytesRead > 0)
                {
                    OnTouchData(buffer);
                }
            }
        }
        finally
        {
            // 确保 reader 被正确释放
            reader?.Dispose();
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
            ExclusiveTouchDiagnostics.Log(
                "{0} player={1} finger={2} pressed={3} mask=0x{4:X16}",
                DiagnosticName, playerNo + 1, fingerId, isPressed, state);
        }
    }

    protected void BeginTouchFrame()
    {
        lock (touchLock)
        {
            var now = Stopwatch.GetTimestamp();
            for (int i = 0; i < allFingerPoints.Length; i++)
            {
                if (allFingerPoints[i].IsActive)
                    allFingerPoints[i].LastUpdateTick = now;
            }
        }
    }

    private void HandleUpdates(IReadOnlyList<TouchUpdate> updates, string eventName, bool replaceState)
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
            foreach (var update in updates)
            {
                ApplyFinger(update, now);
            }

            var state = ComputeActiveMask();
            _touchLatch.Update(state);
            ExclusiveTouchDiagnostics.Log(
                "{0} player={1} {2} updates={3} state=0x{4:X16}",
                DiagnosticName, playerNo + 1, eventName, updates.Count, state);
        }
    }

    protected void HandleFrame(IReadOnlyList<TouchUpdate> updates)
    {
        HandleUpdates(updates, "frame-commit", replaceState: true);
    }

    protected void HandleReleases(IReadOnlyList<TouchUpdate> updates)
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
    private ulong GetTouchState(int player)
    {
        if (player != playerNo) return 0;
        lock (touchLock)
        {
            var now = Stopwatch.GetTimestamp();
            var timedOut = new StringBuilder();
            for (int i = 0; i < allFingerPoints.Length; i++)
            {
                var point = allFingerPoints[i];
                if (point.IsActive && (now - point.LastUpdateTick) > TouchTimeoutTicks)
                {
                    point.IsActive = false;
                    if (timedOut.Length > 0) timedOut.Append(',');
                    timedOut.Append(i);
                }
            }
            var state = ComputeActiveMask();
            _touchLatch.Update(state);
            var result = _touchLatch.Read();
            if (timedOut.Length > 0 || !_hasDiagnosticRead || result != _lastDiagnosticRead)
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
