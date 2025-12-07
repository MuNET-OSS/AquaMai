using System;
using System.Runtime.InteropServices;
using MonoLibUsb.Transfer.Internal;

namespace MonoLibUsb.Transfer;

[StructLayout(LayoutKind.Sequential)]
public class MonoUsbIsoPacket
{
	private static readonly int OfsActualLength = Marshal.OffsetOf(typeof(libusb_iso_packet_descriptor), "actual_length").ToInt32();

	private static readonly int OfsLength = Marshal.OffsetOf(typeof(libusb_iso_packet_descriptor), "length").ToInt32();

	private static readonly int OfsStatus = Marshal.OffsetOf(typeof(libusb_iso_packet_descriptor), "status").ToInt32();

	private IntPtr mpMonoUsbIsoPacket = IntPtr.Zero;

	public IntPtr PtrIsoPacket => mpMonoUsbIsoPacket;

	public int ActualLength
	{
		get
		{
			return Marshal.ReadInt32(mpMonoUsbIsoPacket, OfsActualLength);
		}
		set
		{
			Marshal.WriteInt32(mpMonoUsbIsoPacket, OfsActualLength, value);
		}
	}

	public int Length
	{
		get
		{
			return Marshal.ReadInt32(mpMonoUsbIsoPacket, OfsLength);
		}
		set
		{
			Marshal.WriteInt32(mpMonoUsbIsoPacket, OfsLength, value);
		}
	}

	public MonoUsbTansferStatus Status
	{
		get
		{
			return (MonoUsbTansferStatus)Marshal.ReadInt32(mpMonoUsbIsoPacket, OfsStatus);
		}
		set
		{
			Marshal.WriteInt32(mpMonoUsbIsoPacket, OfsStatus, (int)value);
		}
	}

	public MonoUsbIsoPacket(IntPtr isoPacketPtr)
	{
		mpMonoUsbIsoPacket = isoPacketPtr;
	}
}
