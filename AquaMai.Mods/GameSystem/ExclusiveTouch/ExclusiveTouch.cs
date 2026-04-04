
using System.Diagnostics;
using LibUsbDotNet.Main;
using LibUsbDotNet;
using MelonLoader;
using UnityEngine;
using AquaMai.Core.Helpers;
using System.Threading;
using JetBrains.Annotations;

namespace AquaMai.Mods.GameSystem.ExclusiveTouch;

public abstract class ExclusiveTouchBase(int playerNo, int vid, int pid, [CanBeNull] string serialNumber, [CanBeNull] string locationPath, byte configuration, int interfaceNumber, ReadEndpointID endpoint, int packetSize, int minX, int minY, int maxX, int maxY, bool flip, int radius,
    float aExtraRadius = 0, float bExtraRadius = 0, float cExtraRadius = 0, float dExtraRadius = 0, float eExtraRadius = 0)
{
    private UsbDevice device;
    private TouchSensorMapper touchSensorMapper;

    public bool IsConnected => device != null;

    private class TouchPoint
    {
        public ulong Mask;
        public long LastUpdateTick;
        public bool IsActive;
    }

    // [手指ID]
    private readonly TouchPoint[] allFingerPoints = new TouchPoint[256];

    // 防吃键
    private readonly InputLatch _touchLatch = new();
    private readonly object touchLock = new();

    private static readonly long TouchTimeoutTicks = Stopwatch.Frequency / 50; // 20ms

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

    protected void HandleFinger(ushort x, ushort y, int fingerId, bool isPressed)
    {
        // 安全检查，防止越界
        if (fingerId < 0 || fingerId >= 256) return;
        lock (touchLock)
        {
            var point = allFingerPoints[fingerId];
            if (isPressed)
            {
                ulong touchMask = touchSensorMapper.ParseTouchPoint(x, y);
                point.IsActive = true;
                point.Mask = touchMask;
                point.LastUpdateTick = Stopwatch.GetTimestamp();
            }
            else
            {
                point.IsActive = false;
            }
            _touchLatch.Update(ComputeActiveMask());
        }
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
            for (int i = 0; i < allFingerPoints.Length; i++)
            {
                var point = allFingerPoints[i];
                if (point.IsActive && (now - point.LastUpdateTick) > TouchTimeoutTicks)
                {
                    point.IsActive = false;
                }
            }
            _touchLatch.Update(ComputeActiveMask());
            return _touchLatch.Read();
        }
    }
}
