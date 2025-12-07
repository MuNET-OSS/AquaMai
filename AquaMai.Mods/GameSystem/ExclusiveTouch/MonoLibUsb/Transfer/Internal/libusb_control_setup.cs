using System.Runtime.InteropServices;

namespace MonoLibUsb.Transfer.Internal;

[StructLayout(LayoutKind.Sequential)]
internal class libusb_control_setup
{
	public readonly byte bmRequestType;

	public readonly byte bRequest;

	public readonly short wValue;

	public readonly short wIndex;

	public readonly short wLength;
}
