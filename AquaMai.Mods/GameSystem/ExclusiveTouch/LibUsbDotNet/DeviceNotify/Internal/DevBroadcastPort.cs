using System.Runtime.InteropServices;

namespace LibUsbDotNet.DeviceNotify.Internal;

[StructLayout(LayoutKind.Sequential)]
internal class DevBroadcastPort : DevBroadcastHdr
{
	private char mNameHolder;

	public DevBroadcastPort()
	{
		Size = Marshal.SizeOf(this);
		DeviceType = DeviceType.Port;
	}
}
