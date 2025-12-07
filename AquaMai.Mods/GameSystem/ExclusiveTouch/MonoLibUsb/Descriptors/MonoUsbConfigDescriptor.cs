using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LibUsbDotNet.Descriptors;
using MonoLibUsb.Profile;

namespace MonoLibUsb.Descriptors;

[StructLayout(LayoutKind.Sequential)]
public class MonoUsbConfigDescriptor
{
	public readonly byte bLength;

	public readonly DescriptorType bDescriptorType;

	public readonly short wTotalLength;

	public readonly byte bNumInterfaces;

	public readonly byte bConfigurationValue;

	public readonly byte iConfiguration;

	public readonly byte bmAttributes;

	public readonly byte MaxPower;

	private readonly IntPtr pInterfaces;

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

	public List<MonoUsbInterface> InterfaceList
	{
		get
		{
			List<MonoUsbInterface> list = new List<MonoUsbInterface>();
			for (int i = 0; i < bNumInterfaces; i++)
			{
				IntPtr ptr = new IntPtr(pInterfaces.ToInt64() + Marshal.SizeOf(typeof(MonoUsbInterface)) * i);
				MonoUsbInterface monoUsbInterface = new MonoUsbInterface();
				Marshal.PtrToStructure(ptr, monoUsbInterface);
				list.Add(monoUsbInterface);
			}
			return list;
		}
	}

	internal MonoUsbConfigDescriptor()
	{
	}

	public MonoUsbConfigDescriptor(MonoUsbConfigHandle configHandle)
	{
		Marshal.PtrToStructure(configHandle.DangerousGetHandle(), this);
	}
}
