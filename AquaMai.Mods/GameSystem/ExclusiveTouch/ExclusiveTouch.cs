using System;
using System.Collections.Generic;
using AquaMai.Config.Attributes;
using LibUsbDotNet.Main;
using HarmonyLib;
using IO;
using LibUsbDotNet;
using Manager;
using MelonLoader;
using UnityEngine;

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

    [ConfigEntry("触摸体积半径", zh: "基准是 1440x1440")]
    public static readonly int radius1p;

    private static UsbDevice[] devices = new UsbDevice[2];
    private static TouchSensorMapper[] touchSensorMappers = new TouchSensorMapper[2];
    // 持久化的触摸状态：每个手指ID对应的触摸区域掩码
    private static Dictionary<int, ulong>[] activeTouches = [new Dictionary<int, ulong>(), new Dictionary<int, ulong>()];

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
                touchSensorMappers[0] = new TouchSensorMapper(minX1p, minY1p, maxX1p, maxY1p, radius1p);
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

            if (isPressed1)
            {
                // 按下：计算并保存触摸区域
                ulong touchMask = touchSensorMappers[playerNo].ParseTouchPoint(x1, y1);
                touches[fingerId1] = touchMask;
                MelonLogger.Msg($"[ExclusiveTouch] {playerNo + 1}P: 手指{fingerId1} 按下 at ({x1}, {y1}) -> 0x{touchMask:X}");
            }
            else
            {
                // 松开：移除该手指的触摸区域
                if (touches.ContainsKey(fingerId1))
                {
                    touches.Remove(fingerId1);
                    MelonLogger.Msg($"[ExclusiveTouch] {playerNo + 1}P: 手指{fingerId1} 松开");
                }
            }
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
                if (isPressed2)
                {
                    ulong touchMask = touchSensorMappers[playerNo].ParseTouchPoint(x2, y2);
                    touches[fingerId2] = touchMask;
                    MelonLogger.Msg($"[ExclusiveTouch] {playerNo + 1}P: 手指{fingerId2} 按下 at ({x2}, {y2}) -> 0x{touchMask:X}");
                }
                else
                {
                    if (touches.ContainsKey(fingerId2))
                    {
                        touches.Remove(fingerId2);
                        MelonLogger.Msg($"[ExclusiveTouch] {playerNo + 1}P: 手指{fingerId2} 松开");
                    }
                }
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NewTouchPanel), "Start")]
    public static bool PreNewTouchPanelStart(uint ____monitorIndex, ref NewTouchPanel.StatusEnum ___Status, ref bool ____isRunning)
    {
        if (devices[____monitorIndex] == null) return true;
        ___Status = NewTouchPanel.StatusEnum.Drive;
        ____isRunning = true;
        MelonLogger.Msg($"[ExclusiveTouch] NewTouchPanel Start {____monitorIndex + 1}P");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NewTouchPanel), "Execute")]
    public static bool PreNewTouchPanelExecute(uint ____monitorIndex, ref uint ____dataCounter)
    {
        if (devices[____monitorIndex] == null) return true;
        
        // 合并所有活动手指的触摸区域
        ulong currentTouchData = 0;
        foreach (var touchMask in activeTouches[____monitorIndex].Values)
        {
            currentTouchData |= touchMask;
        }
        
        // MelonLogger.Msg($"[ExclusiveTouch] Execute {____monitorIndex + 1}P: 0x{currentTouchData:X} (活动手指数: {activeTouches[____monitorIndex].Count})");
        InputManager.SetNewTouchPanel(____monitorIndex, currentTouchData, ++____dataCounter);
        return false;
    }
}
