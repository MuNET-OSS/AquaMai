using System;
using System.Collections.Generic;
using AquaMai.Mods.GameSystem.ExclusiveTouch;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using PdxTouchConfig = AquaMai.Mods.GameSystem.PdxTouch.PdxTouch;

namespace AquaMai.Mods.GameSystem.PdxTouch;

internal sealed class FlTouchDevice(int playerNo, string locationPath) : ExclusiveTouchBase(
    playerNo,
    vid: 0x227D,
    pid: 0x0103,
    serialNumber: locationPath,
    locationPath,
    configuration: 1,
    packetSize: 64,
    minX: 18432,
    minY: 0,
    maxX: 0,
    maxY: 32767,
    flip: true,
    PdxTouchConfig.radius,
    PdxTouchConfig.aAreaExtraRadius,
    PdxTouchConfig.bAreaExtraRadius,
    PdxTouchConfig.cAreaExtraRadius,
    PdxTouchConfig.dAreaExtraRadius,
    PdxTouchConfig.eAreaExtraRadius,
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
    private readonly object reportLock = new();

    protected override string DiagnosticName => "FL";

    protected override void OnDeviceConnected()
    {
        lock (reportLock)
        {
            ResetFrameState();
        }
    }

    protected override void OnDeviceDisconnected()
    {
        lock (reportLock)
        {
            ResetFrameState();
        }
    }

    private void ResetFrameState()
    {
        remaining = 0;
        pendingUpdates.Clear();
        releaseUpdates.Clear();
    }

    protected override TouchEndpoint ResolveEndpoint(UsbDevice _)
        => new(0, ReadEndpointID.Ep01);

    protected override void InitializeDevice(UsbDevice usbDevice)
    {
        var reportInfo = new byte[2];
        var getReportPacket = new UsbSetupPacket(0xA1, 0x01, 0x0303, 0, reportInfo.Length);
        if (!usbDevice.ControlTransfer(ref getReportPacket, reportInfo, reportInfo.Length, out var reportInfoLength) ||
            reportInfoLength != reportInfo.Length || reportInfo[0] != 0x03)
        {
            throw new InvalidOperationException("FLTouch capability report query failed");
        }

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
        lock (reportLock)
        {
            OnTouchDataCore(data);
        }
    }

    private void OnTouchDataCore(byte[] data)
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
