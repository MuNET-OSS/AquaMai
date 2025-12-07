using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LibUsbDotNet.Descriptors;

namespace MonoLibUsb.Descriptors;

[StructLayout(LayoutKind.Sequential)]
public class MonoUsbAltInterfaceDescriptor
{
	public readonly byte bLength;

	public readonly DescriptorType bDescriptorType;

	public readonly byte bInterfaceNumber;

	public readonly byte bAlternateSetting;

	public readonly byte bNumEndpoints;

	public readonly ClassCodeType bInterfaceClass;

	public readonly byte bInterfaceSubClass;

	public readonly byte bInterfaceProtocol;

	public readonly byte iInterface;

	private readonly IntPtr pEndpointDescriptors;

	private IntPtr pExtraBytes;

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

	public List<MonoUsbEndpointDescriptor> EndpointList
	{
		get
		{
			List<MonoUsbEndpointDescriptor> list = new List<MonoUsbEndpointDescriptor>();
			for (int i = 0; i < bNumEndpoints; i++)
			{
				IntPtr ptr = new IntPtr(pEndpointDescriptors.ToInt64() + Marshal.SizeOf(typeof(MonoUsbEndpointDescriptor)) * i);
				MonoUsbEndpointDescriptor monoUsbEndpointDescriptor = new MonoUsbEndpointDescriptor();
				Marshal.PtrToStructure(ptr, monoUsbEndpointDescriptor);
				list.Add(monoUsbEndpointDescriptor);
			}
			return list;
		}
	}
}
