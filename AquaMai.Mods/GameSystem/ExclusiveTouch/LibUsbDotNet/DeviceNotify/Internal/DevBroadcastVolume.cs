using System.Runtime.InteropServices;

namespace LibUsbDotNet.DeviceNotify.Internal;

[StructLayout(LayoutKind.Sequential)]
internal class DevBroadcastVolume : DevBroadcastHdr
{
	public int UnitMask;

	public short Flags;

	public DevBroadcastVolume()
	{
		Size = Marshal.SizeOf(this);
		DeviceType = DeviceType.Volume;
	}
}
