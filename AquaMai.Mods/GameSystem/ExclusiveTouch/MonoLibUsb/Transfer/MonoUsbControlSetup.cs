using System;
using System.Runtime.InteropServices;
using LibUsbDotNet.Main;
using MonoLibUsb.Transfer.Internal;

namespace MonoLibUsb.Transfer;

[StructLayout(LayoutKind.Sequential)]
public class MonoUsbControlSetup
{
	public static int SETUP_PACKET_SIZE = Marshal.SizeOf(typeof(libusb_control_setup));

	private static readonly int OfsRequestType = Marshal.OffsetOf(typeof(libusb_control_setup), "bmRequestType").ToInt32();

	private static readonly int OfsRequest = Marshal.OffsetOf(typeof(libusb_control_setup), "bRequest").ToInt32();

	private static readonly int OfsValue = Marshal.OffsetOf(typeof(libusb_control_setup), "wValue").ToInt32();

	private static readonly int OfsIndex = Marshal.OffsetOf(typeof(libusb_control_setup), "wIndex").ToInt32();

	private static readonly int OfsLength = Marshal.OffsetOf(typeof(libusb_control_setup), "wLength").ToInt32();

	private static readonly int OfsPtrData = SETUP_PACKET_SIZE;

	private IntPtr handle;

	public byte RequestType
	{
		get
		{
			return Marshal.ReadByte(handle, OfsRequestType);
		}
		set
		{
			Marshal.WriteByte(handle, OfsRequestType, value);
		}
	}

	public byte Request
	{
		get
		{
			return Marshal.ReadByte(handle, OfsRequest);
		}
		set
		{
			Marshal.WriteByte(handle, OfsRequest, value);
		}
	}

	public short Value
	{
		get
		{
			return Helper.HostEndianToLE16(Marshal.ReadInt16(handle, OfsValue));
		}
		set
		{
			Marshal.WriteInt16(handle, OfsValue, Helper.HostEndianToLE16(value));
		}
	}

	public short Index
	{
		get
		{
			return Helper.HostEndianToLE16(Marshal.ReadInt16(handle, OfsIndex));
		}
		set
		{
			Marshal.WriteInt16(handle, OfsIndex, Helper.HostEndianToLE16(value));
		}
	}

	public short Length
	{
		get
		{
			return Helper.HostEndianToLE16(Marshal.ReadInt16(handle, OfsLength));
		}
		set
		{
			Marshal.WriteInt16(handle, OfsLength, Helper.HostEndianToLE16(value));
		}
	}

	public IntPtr PtrData => new IntPtr(handle.ToInt64() + OfsPtrData);

	public MonoUsbControlSetup(IntPtr pControlSetup)
	{
		handle = pControlSetup;
	}

	public void SetData(object data, int offset, int length)
	{
		PinnedHandle pinnedHandle = new PinnedHandle(data);
		byte[] array = new byte[length];
		Marshal.Copy(pinnedHandle.Handle, array, offset, length);
		pinnedHandle.Dispose();
		Marshal.Copy(array, 0, PtrData, length);
	}

	public byte[] GetData(int transferLength)
	{
		byte[] array = new byte[transferLength];
		Marshal.Copy(PtrData, array, 0, array.Length);
		return array;
	}
}
