using System;
using System.Runtime.InteropServices;
using LibUsbDotNet.Main;

namespace MonoLibUsb.Transfer.Internal;

[StructLayout(LayoutKind.Sequential)]
internal class libusb_transfer
{
	private IntPtr deviceHandle;

	private MonoUsbTransferFlags flags;

	private byte endpoint;

	private EndpointType type;

	private uint timeout;

	private MonoUsbTansferStatus status;

	private int length;

	private int actual_length;

	private IntPtr pCallbackFn;

	private IntPtr pUserData;

	private IntPtr pBuffer;

	private int num_iso_packets;

	private IntPtr iso_packets;

	private libusb_transfer()
	{
	}
}
