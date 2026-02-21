using System;
using AquaMai.Config.Attributes;
using AquaMai.Mods.GameSystem.ExclusiveTouch;
using LibUsbDotNet.Main;

namespace AquaMai.Mods.GameSystem;

[ConfigSection("PDX 独占触摸")]
public class PdxTouch
{
    [ConfigEntry("触摸体积半径", zh: "基准是 1440x1440")]
    public static readonly int radius = 30;

    [ConfigEntry("1P 设备路径", zh: "USB 端口路径，例如 2.2。请使用配置工具中显示的路径。留空则使用第一个检测到的设备作为 1P")]
    public static readonly string path1p = "";

    [ConfigEntry("2P 设备路径")]
    public static readonly string path2p = "";

    private static readonly PdxTouchDevice[] devices = new PdxTouchDevice[2];

    public static void OnBeforePatch()
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
        radius)
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
