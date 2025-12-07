using System.Runtime.InteropServices;

namespace MonoLibUsb.Transfer.Internal;

[StructLayout(LayoutKind.Sequential)]
internal class libusb_iso_packet_descriptor
{
	private uint length;

	private uint actual_length;

	private MonoUsbTansferStatus status;

	private libusb_iso_packet_descriptor()
	{
	}
}
