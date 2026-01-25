using System;
using LibUsbDotNet.Main;
using LibUsbDotNet;
using MelonLoader;
using UnityEngine;
using AquaMai.Core.Helpers;
using System.Threading;
using JetBrains.Annotations;

namespace AquaMai.Mods.GameSystem.ExclusiveTouch;

public abstract class ExclusiveTouchBase(int playerNo, int vid, int pid, [CanBeNull] string serialNumber, [CanBeNull] string locationPath, byte configuration, int interfaceNumber, ReadEndpointID endpoint, int packetSize, int minX, int minY, int maxX, int maxY, bool flip, int radius)
{
    private UsbDevice device;
    private TouchSensorMapper touchSensorMapper;

    private class TouchPoint
    {
        public ulong Mask;
        public DateTime LastUpdate;
        public bool IsActive;
    }

    // [手指ID]
    private readonly TouchPoint[] allFingerPoints = new TouchPoint[256];

    // 防吃键
    private ulong frameAccumulators;
    private readonly object touchLock = new();

    private const int TouchTimeoutMs = 20;

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
            touchSensorMapper = new TouchSensorMapper(minX, minY, maxX, maxY, radius, flip);
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

                if (!point.IsActive)
                {
                    point.IsActive = true;
                }

                point.Mask = touchMask;
                point.LastUpdate = DateTime.Now;

                frameAccumulators |= touchMask;
            }
            else
            {
                if (point.IsActive)
                {
                    point.IsActive = false;
                }
            }
        }
    }

    private ulong GetTouchState(int player)
    {
        if (player != playerNo) return 0;
        lock (touchLock)
        {
            ulong currentTouchData = 0;
            var now = DateTime.Now;

            for (int i = 0; i < allFingerPoints.Length; i++)
            {
                var point = allFingerPoints[i];
                if (point.IsActive)
                {
                    if ((now - point.LastUpdate).TotalMilliseconds > TouchTimeoutMs)
                    {
                        point.IsActive = false;
                    }
                    else
                    {
                        currentTouchData |= point.Mask;
                    }
                }
            }

            ulong finalResult = currentTouchData | frameAccumulators;
            frameAccumulators = 0;

            return finalResult;
        }
    }
}
