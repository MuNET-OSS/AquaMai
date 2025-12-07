using System;
using LibUsbDotNet.DeviceNotify.Info;
using LibUsbDotNet.DeviceNotify.Internal;

namespace LibUsbDotNet.DeviceNotify;

public class WindowsDeviceNotifyEventArgs : DeviceNotifyEventArgs
{
	private readonly DevBroadcastHdr mBaseHdr;

	internal WindowsDeviceNotifyEventArgs(DevBroadcastHdr hdr, IntPtr ptrHdr, EventType eventType)
	{
		mBaseHdr = hdr;
		base.EventType = eventType;
		base.DeviceType = mBaseHdr.DeviceType;
		switch (base.DeviceType)
		{
		case DeviceType.Volume:
			base.Volume = new VolumeNotifyInfo(ptrHdr);
			base.Object = base.Volume;
			break;
		case DeviceType.Port:
			base.Port = new PortNotifyInfo(ptrHdr);
			base.Object = base.Port;
			break;
		case DeviceType.DeviceInterface:
			base.Device = new UsbDeviceNotifyInfo(ptrHdr);
			base.Object = base.Device;
			break;
		case DeviceType.Net:
			break;
		}
	}
}
