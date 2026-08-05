using System;
using System.Collections.Generic;
using AquaMai.Config.Attributes;
using AquaMai.Mods.GameSystem.ExclusiveTouch;
using LibUsbDotNet;
using LibUsbDotNet.Main;

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

    [ConfigEntry("触摸诊断日志",
        en: "Write touch report diagnostics to UserData/AquaMaiTouch.log.",
        zh: "将触摸报告诊断信息写入 UserData/AquaMaiTouch.log")]
    public static readonly bool diagnosticLog = false;

    public static void OnBeforeEnableCheck()
    {
        ExclusiveTouchDiagnostics.Configure(diagnosticLog);
        ExclusiveTouchHost.StartDevices("PdxTouch", path1p, path2p,
            (playerNo, path) => new PdxTouchDevice(playerNo, path),
            (playerNo, path) => new FlTouchDevice(playerNo, path));
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
        private int reportSequence;

        protected override string DiagnosticName => "PDX";

        protected override void OnTouchData(byte[] data)
        {
            byte reportId = data[0];
            if (reportId != ReportId) return;

            reportSequence++;
            var diagnosticsEnabled = ExclusiveTouchDiagnostics.Enabled;
            var contacts = diagnosticsEnabled ? new System.Text.StringBuilder() : null;
            var validSlots = 0;

            for (int i = 0; i < 10; i++)
            {
                var index = i * 6 + 1;
                if (data[index] == 0) continue;
                validSlots++;
                bool isPressed = (data[index] & 0x01) == 1;
                var fingerId = data[index + 1];
                ushort x = BitConverter.ToUInt16(data, index + 2);
                ushort y = BitConverter.ToUInt16(data, index + 4);
                if (diagnosticsEnabled)
                {
                    if (contacts.Length > 0) contacts.Append(' ');
                    contacts.Append($"id={fingerId}:st=0x{data[index]:X2},p={isPressed},x={x},y={y}");
                }
                HandleFinger(x, y, fingerId, isPressed);
            }

            if (diagnosticsEnabled)
            {
                ExclusiveTouchDiagnostics.Log(
                    "PDX player={0} report={1} slots={2} {3}",
                    PlayerNo + 1, reportSequence, validSlots, contacts);
            }
        }
    }

    private class FlTouchDevice(int playerNo, string locationPath) : ExclusiveTouchBase(
        playerNo,
        vid: 0x227D,
        pid: 0x0103,
        serialNumber: null,  // 复合设备的 MI_00 子设备路径里不带序列号，只能用端口路径
        locationPath,
        configuration: 1,
        interfaceNumber: 0,
        ReadEndpointID.Ep01,
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
        eAreaExtraRadius,
        timeoutMilliseconds: 100)
    {
        private const byte ReportId = 2;
        private const int SlotStart = 2;
        private const int SlotSize = 10;
        private const int SlotsPerReport = 6;
        private static readonly byte[] MultipleInputModeReport = { 0x04, 0x02, 0x00 };

        // 一帧超过 6 个点时会拆成多个报告连续发来，只有首个报告带总数
        private int remaining;
        private int reportSequence;
        private readonly List<TouchUpdate> pendingUpdates = new(SlotsPerReport * 2);
        private readonly List<TouchUpdate> releaseUpdates = new(SlotsPerReport);

        protected override string DiagnosticName => "FL";

        protected override void InitializeDevice(UsbDevice usbDevice)
        {
            var setupPacket = new UsbSetupPacket(0x21, 0x09, 0x0304, 0, MultipleInputModeReport.Length);
            if (!usbDevice.ControlTransfer(ref setupPacket, MultipleInputModeReport,
                MultipleInputModeReport.Length, out var lengthTransferred) ||
                lengthTransferred != MultipleInputModeReport.Length)
            {
                throw new InvalidOperationException("FLTouch multiple input mode setup failed");
            }
        }

        protected override void OnTouchData(byte[] data)
        {
            if (data[0] != ReportId) return;

            reportSequence++;
            var diagnosticsEnabled = ExclusiveTouchDiagnostics.Enabled;
            int count = data[1];
            int remainingBefore = remaining;
            if (count > 0)
            {
                if (remaining > 0 && diagnosticsEnabled)
                {
                    ExclusiveTouchDiagnostics.Log(
                        "FL player={0} report={1} resync drop-pending={2}",
                        PlayerNo + 1, reportSequence, pendingUpdates.Count);
                }
                pendingUpdates.Clear();
                // 新帧的帧头。上一帧没收满就丢了，这里直接重置
                remaining = count;
            }
            else if (remaining <= 0)
            {
                // 从帧中间开始读，没有帧头，只能丢
                if (diagnosticsEnabled)
                {
                    ExclusiveTouchDiagnostics.Log(
                        "FL player={0} report={1} zero-count-no-pending",
                        PlayerNo + 1, reportSequence);
                }
                return;
            }

            // 剩余数量之外的槽里是上一个报告的残留数据，不清零，读了会变成幻影触摸
            int take = Math.Min(remaining, SlotsPerReport);
            var contacts = diagnosticsEnabled ? new System.Text.StringBuilder() : null;
            releaseUpdates.Clear();
            for (int i = 0; i < take; i++)
            {
                var index = SlotStart + i * SlotSize;
                var fingerId = data[index + 1];
                ushort x = BitConverter.ToUInt16(data, index + 2);
                ushort y = BitConverter.ToUInt16(data, index + 4);
                ushort w = BitConverter.ToUInt16(data, index + 6);
                ushort h = BitConverter.ToUInt16(data, index + 8);

                // 一次触摸的状态序列是 04(有面积) -> 07 -> 04(面积归零) -> 00，
                // Tip Switch 位只在 07 出现。用面积判定比等 Tip Switch 早一帧
                // （4~8ms），抬起时面积归零和 Tip Switch 清零在同一帧，没有区别
                bool isPressed = w > 0 || h > 0;
                if (diagnosticsEnabled)
                {
                    if (contacts.Length > 0) contacts.Append(' ');
                    contacts.Append($"id={fingerId},p={isPressed},x={x},y={y},w={w},h={h}");
                }
                var update = new TouchUpdate(x, y, fingerId, isPressed);
                pendingUpdates.Add(update);
                if (!isPressed)
                {
                    releaseUpdates.Add(update);
                }
            }

            remaining -= take;
            if (diagnosticsEnabled)
            {
                ExclusiveTouchDiagnostics.Log(
                    "FL player={0} report={1} count={2} remaining={3}->{4} take={5} {6}",
                    PlayerNo + 1, reportSequence, count, remainingBefore, remaining, take, contacts);
            }
            if (releaseUpdates.Count > 0)
            {
                HandleReleases(releaseUpdates);
            }
            if (remaining == 0)
            {
                HandleFrame(pendingUpdates);
                pendingUpdates.Clear();
            }
        }
    }
}
