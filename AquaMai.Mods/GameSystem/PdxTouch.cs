using System;
using AquaMai.Config.Attributes;
using AquaMai.Mods.GameSettings;
using AquaMai.Mods.GameSystem.ExclusiveTouch;
using LibUsbDotNet.Main;
using MelonLoader;

namespace AquaMai.Mods.GameSystem;

[ConfigSection("PDX 独占触摸")]
public class PdxTouch
{
    [ConfigEntry("触摸体积半径", zh: "基准是 1440x1440")]
    public static readonly int radius = 20;

    [ConfigEntry("A 区额外半径",
        en: "Extra radius for A area (outer ring buttons). Can be negative to shrink.",
        zh: "A 区（外圈按键）的额外半径，可以为负值来缩小")]
    public static readonly float aAreaExtraRadius = 0;

    [ConfigEntry("B 区额外半径",
        en: "Extra radius for B area (middle ring sensors). Can be negative to shrink.",
        zh: "B 区（中圈传感器）的额外半径，可以为负值来缩小")]
    public static readonly float bAreaExtraRadius = 25;

    [ConfigEntry("C 区额外半径",
        en: "Extra radius for C area (center sensors). Can be negative to shrink.",
        zh: "C 区（中心传感器）的额外半径，可以为负值来缩小")]
    public static readonly float cAreaExtraRadius = 0;

    [ConfigEntry("D 区额外半径",
        en: "Extra radius for D area (inner ring sensors). Can be negative to shrink.",
        zh: "D 区（内圈传感器）的额外半径，可以为负值来缩小")]
    public static readonly float dAreaExtraRadius = 0;

    [ConfigEntry("E 区额外半径",
        en: "Extra radius for E area (innermost ring sensors). Can be negative to shrink.",
        zh: "E 区（最内圈传感器）的额外半径，可以为负值来缩小")]
    public static readonly float eAreaExtraRadius = 30;

    [ConfigEntry("1P 设备路径", zh: "USB 端口路径，例如 2.2。请使用配置工具中显示的路径。留空则使用第一个检测到的设备作为 1P")]
    public static readonly string path1p = "";

    [ConfigEntry("2P 设备路径")]
    public static readonly string path2p = "";

    private static readonly PdxTouchDevice[] devices = new PdxTouchDevice[2];

    public static void OnBeforeEnableCheck()
    {
        if (string.IsNullOrWhiteSpace(path1p) && string.IsNullOrWhiteSpace(path2p))
        {
            // 没有配置任何路径,使用第一个设备
            devices[0] = new PdxTouchDevice(0, null);
            devices[0].Start();
        }
        else
        {
            // 配置了路径,按路径查找
            if (!string.IsNullOrWhiteSpace(path1p))
            {
                devices[0] = new PdxTouchDevice(0, path1p);
                devices[0].Start();
            }
            if (!string.IsNullOrWhiteSpace(path2p))
            {
                devices[1] = new PdxTouchDevice(1, path2p);
                devices[1].Start();
            }
        }

        for (int i = 0; i < 2; i++)
        {
            if (devices[i] != null && devices[i].IsConnected)
            {
                if (!JudgeAdjust.shouldEnableImplicitly)
                {
                    JudgeAdjust.shouldEnableImplicitly = true;
                }
                if (i == 0) JudgeAdjust.b_1P += 1.0;
                else JudgeAdjust.b_2P += 1.0;
                MelonLogger.Msg($"[PdxTouch] {i + 1}P connected");
            }
        }
    }

    private class PdxTouchDevice(int playerNo, string locationPath) : ExclusiveTouchBase(
        playerNo,
        vid: 0x3356,
        pid: 0x3003,
        serialNumber: null,  // PDX 设备没有序列号
        locationPath,        // 使用路径匹配
        configuration: 1,
        interfaceNumber: 1,
        ReadEndpointID.Ep02,
        packetSize: 64,
        minX: 18432,
        minY: 0,
        maxX: 0,
        maxY: 32767,
        flip: true,
        radius,
        aAreaExtraRadius,
        bAreaExtraRadius,
        cAreaExtraRadius,
        dAreaExtraRadius,
        eAreaExtraRadius)
    {
        private const byte ReportId = 2;
        protected override void OnTouchData(byte[] data)
        {
            byte reportId = data[0];
            if (reportId != ReportId) return;

            for (int i = 0; i < 10; i++)
            {
                var index = i * 6 + 1;
                if (data[index] == 0) continue;
                bool isPressed = (data[index] & 0x01) == 1;
                var fingerId = data[index + 1];
                ushort x = BitConverter.ToUInt16(data, index + 2);
                ushort y = BitConverter.ToUInt16(data, index + 4);
                HandleFinger(x, y, fingerId, isPressed);
            }
        }
    }
}
