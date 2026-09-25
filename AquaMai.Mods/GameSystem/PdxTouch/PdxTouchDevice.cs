using System;
using System.Collections.Generic;
using AquaMai.Mods.GameSystem.ExclusiveTouch;
using LibUsbDotNet;
using LibUsbDotNet.Info;
using LibUsbDotNet.Main;
using PdxTouchConfig = AquaMai.Mods.GameSystem.PdxTouch.PdxTouch;

namespace AquaMai.Mods.GameSystem.PdxTouch;

internal sealed partial class PdxTouchDevice(int playerNo, string locationPath) : ExclusiveTouchBase(
    playerNo,
    vid: 0x3356,
    pid: 0x3003,
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

    protected override TouchEndpoint ResolveEndpoint(UsbDevice usbDevice)
    {
        var configs = usbDevice.Configs;
        if (configs.Count == 0)
        {
            throw new InvalidOperationException("PDX device has no USB configuration");
        }

        var interfaces = configs[0].InterfaceInfoList;
        // Ep02 是原 PDX，Ep01 是 AT32 的旧 PDX
        if (HasEndpoint(interfaces, interfaceNumber: 1, endpointId: (byte)ReadEndpointID.Ep02))
        {
            kind = PdxKind.New;
            return new TouchEndpoint(1, ReadEndpointID.Ep02);
        }

        if (HasEndpoint(interfaces, interfaceNumber: 0, endpointId: (byte)ReadEndpointID.Ep01))
        {
            kind = PdxKind.OldAt32;
            return new TouchEndpoint(0, ReadEndpointID.Ep01);
        }

        throw new InvalidOperationException("PDX device has no supported touch endpoint");
    }

    private static bool HasEndpoint(IReadOnlyCollection<UsbInterfaceInfo> interfaces,
        int interfaceNumber, byte endpointId)
    {
        foreach (var iface in interfaces)
        {
            if (iface.Descriptor.InterfaceID != interfaceNumber) continue;
            foreach (var endpoint in iface.EndpointInfoList)
            {
                if (endpoint.Descriptor.EndpointID == endpointId) return true;
            }
        }

        return false;
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
