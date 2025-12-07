using System.Runtime.InteropServices;

namespace LibUsbDotNet.DeviceNotify.Internal;

[StructLayout(LayoutKind.Sequential)]
internal class DevBroadcastHdr
{
	public int Size;

	public DeviceType DeviceType;

	public int Rsrvd1;

	internal DevBroadcastHdr()
	{
		Size = Marshal.SizeOf(this);
	}
}
