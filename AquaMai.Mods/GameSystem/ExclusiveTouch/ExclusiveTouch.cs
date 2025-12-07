using System;
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
    private static ulong[] touchData = [0, 0];

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

        // 解析数据
        byte touchStatus = data[1];
        ushort x = BitConverter.ToUInt16(data, 2);
        ushort y = BitConverter.ToUInt16(data, 4);

        MelonLogger.Msg($"[ExclusiveTouch] {playerNo + 1}P: {touchStatus} - X: {x,5}, Y: {y,5}");

        // 是松开，其他的位大概是触摸点是第几个（？
        if ((touchStatus & 1) != 1) return;

        touchData[playerNo] |= touchSensorMappers[playerNo].ParseTouchPoint(x, y);
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
        MelonLogger.Msg($"[ExclusiveTouch] Execute {____monitorIndex + 1}P: {touchData[____monitorIndex]:x8}");
        InputManager.SetNewTouchPanel(____monitorIndex, touchData[____monitorIndex], ++____dataCounter);
        touchData[____monitorIndex] = 0;
        return false;
    }
}
