using System;
using System.Runtime.InteropServices;
using LibUsbDotNet.Descriptors;

namespace MonoLibUsb.Descriptors;

[StructLayout(LayoutKind.Sequential)]
public class MonoUsbEndpointDescriptor
{
	public readonly byte bLength;

	public readonly DescriptorType bDescriptorType;

	public readonly byte bEndpointAddress;

	public readonly byte bmAttributes;

	public readonly short wMaxPacketSize;

	public readonly byte bInterval;

	public readonly byte bRefresh;

	public readonly byte bSynchAddress;

	private readonly IntPtr pExtraBytes;

	public readonly int ExtraLength;

	public byte[] ExtraBytes
	{
		get
		{
			byte[] array = new byte[ExtraLength];
			Marshal.Copy(pExtraBytes, array, 0, array.Length);
			return array;
		}
	}
}
