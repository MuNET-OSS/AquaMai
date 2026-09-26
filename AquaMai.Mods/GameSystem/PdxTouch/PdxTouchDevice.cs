using System;
using System.Collections.Generic;
using AquaMai.Mods.GameSystem.ExclusiveTouch;
using AquaMai.Mods.GameSystem.Lib;
using PdxTouchConfig = AquaMai.Mods.GameSystem.PdxTouch.PdxTouch;

namespace AquaMai.Mods.GameSystem.PdxTouch;

internal sealed partial class PdxTouchDevice(int playerNo, string locationPath) : ExclusiveTouchBase(
    playerNo,
    vid: 0x3356,
    pid: 0x3003,
    identifier: locationPath,
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
    PdxTouchConfig.eAreaExtraRadius)
{
    private const byte ReportId = 2;
    private const int MaxContacts = 40;
    private const int SlotsPerReport = 6;

    private enum PdxKind
    {
        New,
        OldAt32,
    }

    private PdxKind kind;
    private int reportSequence;
    private int remaining;
    private readonly List<TouchUpdate> pendingUpdates = new(MaxContacts);
    private readonly List<TouchUpdate> releaseUpdates = new(SlotsPerReport);
    private readonly object reportLock = new();

    protected override string DiagnosticName => kind == PdxKind.OldAt32 ? "PDX-AT32" : "PDX";

    protected override TouchEndpoint ResolveEndpoint(WinUsbIo.DevicePath devicePath)
    {
        // Ep02 是原 PDX，Ep01 是 AT32 的旧 PDX
        if (devicePath.InterfaceNumber == 1)
        {
            kind = PdxKind.New;
            return new TouchEndpoint(1, 0x82);
        }

        if (devicePath.InterfaceNumber == 0)
        {
            kind = PdxKind.OldAt32;
            return new TouchEndpoint(0, 0x81);
        }

        throw new InvalidOperationException($"PDX device has unsupported interface {devicePath.InterfaceNumber}");
    }

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

    protected override void OnTouchData(byte[] data)
    {
        lock (reportLock)
        {
            switch (kind)
            {
                case PdxKind.New:
                    OnNewTouchData(data);
                    break;
                case PdxKind.OldAt32:
                    OnOldTouchData(data);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported PDX kind: {kind}");
            }
        }
    }
}
