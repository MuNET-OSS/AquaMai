using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MonoLibUsb.Descriptors;

[StructLayout(LayoutKind.Sequential)]
public class MonoUsbInterface
{
	private IntPtr pAltSetting;

	public readonly int num_altsetting;

	public List<MonoUsbAltInterfaceDescriptor> AltInterfaceList
	{
		get
		{
			List<MonoUsbAltInterfaceDescriptor> list = new List<MonoUsbAltInterfaceDescriptor>();
			for (int i = 0; i < num_altsetting; i++)
			{
				IntPtr ptr = new IntPtr(pAltSetting.ToInt64() + Marshal.SizeOf(typeof(MonoUsbAltInterfaceDescriptor)) * i);
				MonoUsbAltInterfaceDescriptor monoUsbAltInterfaceDescriptor = new MonoUsbAltInterfaceDescriptor();
				Marshal.PtrToStructure(ptr, monoUsbAltInterfaceDescriptor);
				list.Add(monoUsbAltInterfaceDescriptor);
			}
			return list;
		}
	}
}
