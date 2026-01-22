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

    [ConfigEntry("1P 序列号", zh: "如果要组 2P，请在这里指定对应的序列号。否则将自动使用第一个检测到的设备作为 1P")]
    public static readonly string serial1p;
    [ConfigEntry("2P 序列号")]
    public static readonly string serial2p;

    private static readonly PdxTouchDevice[] devices = new PdxTouchDevice[2];

    public static void OnBeforePatch()
    {
        if (string.IsNullOrWhiteSpace(serial1p) && string.IsNullOrWhiteSpace(serial2p))
        {
            devices[0] = new PdxTouchDevice(0, null);
            devices[0].Start();
        }
        if (!string.IsNullOrWhiteSpace(serial1p))
        {
            devices[0] = new PdxTouchDevice(0, serial1p);
            devices[0].Start();
        }
        if (!string.IsNullOrWhiteSpace(serial2p))
        {
            devices[1] = new PdxTouchDevice(1, serial2p);
            devices[1].Start();
        }
    }

    private class PdxTouchDevice(int playerNo, string serialNumber) : ExclusiveTouchBase(playerNo, vid: 0x3356, pid: 0x3003, serialNumber, configuration: 1, interfaceNumber: 1, ReadEndpointID.Ep02, packetSize: 64, minX: 18432, minY: 0, maxX: 0, maxY: 32767, flip: true, radius)
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
