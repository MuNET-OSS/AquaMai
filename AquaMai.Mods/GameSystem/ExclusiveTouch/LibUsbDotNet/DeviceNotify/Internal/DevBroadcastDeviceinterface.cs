using System;
using System.Runtime.InteropServices;

namespace LibUsbDotNet.DeviceNotify.Internal;

[StructLayout(LayoutKind.Sequential)]
internal class DevBroadcastDeviceinterface : DevBroadcastHdr
{
	public Guid ClassGuid = Guid.Empty;

	private char mNameHolder;

	public DevBroadcastDeviceinterface()
	{
		Size = Marshal.SizeOf(this);
		DeviceType = DeviceType.DeviceInterface;
	}

	public DevBroadcastDeviceinterface(Guid guid)
		: this()
	{
		ClassGuid = guid;
	}
}
