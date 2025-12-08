using System;
using AquaMai.Config.Attributes;
using LibUsbDotNet.Main;
using LibUsbDotNet;
using MelonLoader;
using UnityEngine;
using AquaMai.Core.Helpers;
using System.Threading;

namespace AquaMai.Mods.GameSystem.ExclusiveTouch;

[ConfigCollapseNamespace]
[ConfigSection]
public class ExclusiveTouch
{
    [ConfigEntry]
    public static readonly bool enable1p;

    [ConfigEntry]
    public static readonly int vid1p;
    [ConfigEntry]
    public static readonly int pid1p;
    [ConfigEntry]
    public static readonly string serialNumber1p = "";
    [ConfigEntry]
    public static readonly byte configuration1p = 1;
    [ConfigEntry]
    public static readonly int interfaceNumber1p = 0;
    [ConfigEntry]
    public static readonly int reportId1p;
    [ConfigEntry]
    public static readonly ReadEndpointID endpoint1p = ReadEndpointID.Ep01;
    [ConfigEntry]
    public static readonly int packetSize1p = 64;
    [ConfigEntry]
    public static readonly int minX1p;
    [ConfigEntry]
    public static readonly int minY1p;
    [ConfigEntry]
    public static readonly int maxX1p;
    [ConfigEntry]
    public static readonly int maxY1p;
    [ConfigEntry]
    public static readonly bool flip1p;

    [ConfigEntry("触摸体积半径", zh: "基准是 1440x1440")]
    public static readonly int radius1p;

    private static UsbDevice[] devices = new UsbDevice[2];
    private static TouchSensorMapper[] touchSensorMappers = new TouchSensorMapper[2];

    private class TouchPoint
    {
        public ulong Mask;
        public DateTime LastUpdate;
        public bool IsActive;
    }

    // [玩家][手指ID]
    private static readonly TouchPoint[][] allFingerPoints = new TouchPoint[2][];

    // 防吃键
    private static readonly ulong[] frameAccumulators = new ulong[2];
    private static readonly object[] touchLocks = [new object(), new object()];

    private const int TouchTimeoutMs = 20;

    public static void OnBeforePatch()
    {
        if (enable1p)
        {
            // 方便组 2P
            var serialNumber = string.IsNullOrWhiteSpace(serialNumber1p) ? null : serialNumber1p;
            var finder = new UsbDeviceFinder(vid1p, pid1p, serialNumber);
            var device = UsbDevice.OpenUsbDevice(finder);
            if (device == null)
            {
                MelonLogger.Msg("[ExclusiveTouch] Cannot connect 1P");
            }
            else
            {
                IUsbDevice wholeDevice = device as IUsbDevice;
                if (wholeDevice != null)
                {
                    wholeDevice.SetConfiguration(configuration1p);
                    wholeDevice.ClaimInterface(interfaceNumber1p);
                }
                touchSensorMappers[0] = new TouchSensorMapper(minX1p, minY1p, maxX1p, maxY1p, radius1p, flip1p);
                Application.quitting += () =>
                {
                    devices[0] = null;
                    if (wholeDevice != null)
                    {
                        wholeDevice.ReleaseInterface(interfaceNumber1p);
                    }
                    device.Close();
                };
                
                allFingerPoints[0] = new TouchPoint[256];
                for (int i = 0; i < 256; i++)
                {
                    allFingerPoints[0][i] = new TouchPoint();
                }

                devices[0] = device;
                Thread readThread = new Thread(() => ReadThread(0));
                readThread.Start();
                TouchStatusProvider.RegisterTouchStatusProvider(0, GetTouchState);
            }
        }
    }

    private static void ReadThread(int playerNo)
    {
        byte[] buffer = new byte[packetSize1p];
        var reader = devices[playerNo].OpenEndpointReader(endpoint1p);
        while (devices[playerNo] != null)
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
                OnTouchData(playerNo, buffer);
            }
        }
    }

    private static void OnTouchData(int playerNo, byte[] data)
    {
        byte reportId = data[0];
        if (reportId != reportId1p) return;

#if true // PDX
        for (int i = 0; i < 10; i++)
        {
            var index = i * 6 + 1;
            if (data[index] == 0) continue;
            bool isPressed = (data[index] & 0x01) == 1;
            var fingerId = data[index + 1];
            ushort x = BitConverter.ToUInt16(data, index + 2);
            ushort y = BitConverter.ToUInt16(data, index + 4);
            HandleFinger(x, y, fingerId, isPressed, playerNo);
        }
#else // 凌莞的便携屏
        // 解析第一根手指
        if (data.Length >= 7)
        {
            byte status1 = data[1];
            int fingerId1 = (status1 >> 4) & 0x0F;  // 高4位：手指ID
            bool isPressed1 = (status1 & 0x01) == 1; // 低位：按下状态
            ushort x1 = BitConverter.ToUInt16(data, 2);
            ushort y1 = BitConverter.ToUInt16(data, 4);

            HandleFinger(x1, y1, fingerId1, isPressed1, playerNo);
        }

        // 解析第二根手指
        if (data.Length >= 14)
        {
            byte status2 = data[6];
            int fingerId2 = (status2 >> 4) & 0x0F;
            bool isPressed2 = (status2 & 0x01) == 1;
            ushort x2 = BitConverter.ToUInt16(data, 7);
            ushort y2 = BitConverter.ToUInt16(data, 9);

            // 只有坐标非零才处理第二根手指
            if (x2 != 0 || y2 != 0)
            {
                HandleFinger(x2, y2, fingerId2, isPressed2, playerNo);
            }
        }
#endif
    }

    private static void HandleFinger(ushort x, ushort y, int fingerId, bool isPressed, int playerNo)
    {
        // 安全检查，防止越界
        if (fingerId < 0 || fingerId >= 256) return;

        lock (touchLocks[playerNo])
        {
            var point = allFingerPoints[playerNo][fingerId];

            if (isPressed)
            {
                ulong touchMask = touchSensorMappers[playerNo].ParseTouchPoint(x, y);

                if (!point.IsActive)
                {
                    point.IsActive = true;
                    MelonLogger.Msg($"[ExclusiveTouch] {playerNo + 1}P: 手指{fingerId} 按下 at ({x}, {y}) -> 0x{touchMask:X}");
                }

                point.Mask = touchMask;
                point.LastUpdate = DateTime.Now;
                
                frameAccumulators[playerNo] |= touchMask;
            }
            else
            {
                if (point.IsActive)
                {
                    point.IsActive = false;
                    MelonLogger.Msg($"[ExclusiveTouch] {playerNo + 1}P: 手指{fingerId} 松开");
                }
            }
        }
    }

    public static ulong GetTouchState(int playerNo)
    {
        lock (touchLocks[playerNo])
        {
            ulong currentTouchData = 0;
            var now = DateTime.Now;
            var points = allFingerPoints[playerNo];

            for (int i = 0; i < points.Length; i++)
            {
                var point = points[i];
                if (point.IsActive)
                {
                    if ((now - point.LastUpdate).TotalMilliseconds > TouchTimeoutMs)
                    {
                        point.IsActive = false;
                        MelonLogger.Msg($"[ExclusiveTouch] {playerNo + 1}P: 手指{i} 超时自动释放");
                    }
                    else
                    {
                        currentTouchData |= point.Mask;
                    }
                }
            }
            
            ulong finalResult = currentTouchData | frameAccumulators[playerNo];
            frameAccumulators[playerNo] = 0;

            return finalResult;
        }
    }
}
