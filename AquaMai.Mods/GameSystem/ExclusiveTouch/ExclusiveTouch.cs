using System;
using System.Collections.Generic;
using AquaMai.Config.Attributes;
using LibUsbDotNet.Main;
using LibUsbDotNet;
using MelonLoader;
using UnityEngine;
using AquaMai.Core.Helpers;

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
    // 持久化的触摸状态：每个手指ID对应的触摸区域掩码
    private static Dictionary<int, ulong>[] activeTouches = [[], []];

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
                var reader = device.OpenEndpointReader(ReadEndpointID.Ep01);
                reader.DataReceived += (sender, e) => OnTouchData(0, sender, e);
                reader.DataReceivedEnabled = true;
                Application.quitting += () =>
                {
                    if (wholeDevice != null)
                    {
                        wholeDevice.ReleaseInterface(0);
                    }
                    device.Close();
                };

                devices[0] = device;
                TouchStatusProvider.RegisterTouchStatusProvider(0, GetTouchState);
            }
        }
    }

    private static void OnTouchData(int playerNo, object sender, EndpointDataEventArgs e)
    {
        if (e.Count < 14) return;

        byte[] data = e.Buffer;
        byte reportId = data[0];
        if (reportId != 0x0D) return;

        var touches = activeTouches[playerNo];

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
    }

    private static void HandleFinger(ushort x, ushort y, int fingerId, bool isPressed, int playerNo)
    {
        var touches = activeTouches[playerNo];
        if (isPressed)
        {
            ulong touchMask = touchSensorMappers[playerNo].ParseTouchPoint(x, y);
            touches[fingerId] = touchMask;
            MelonLogger.Msg($"[ExclusiveTouch] {playerNo + 1}P: 手指{fingerId} 按下 at ({x}, {y}) -> 0x{touchMask:X}");
        }
        else
        {
            if (touches.ContainsKey(fingerId))
            {
                touches.Remove(fingerId);
                MelonLogger.Msg($"[ExclusiveTouch] {playerNo + 1}P: 手指{fingerId} 松开");
            }
        }

    }

    public static ulong GetTouchState(int playerNo)
    {
        if (activeTouches[playerNo] == null) return 0;

        // 合并所有活动手指的触摸区域
        ulong currentTouchData = 0;
        foreach (var touchMask in activeTouches[playerNo].Values)
        {
            currentTouchData |= touchMask;
        }

        return currentTouchData;
    }
}
