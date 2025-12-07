using System;
using LibUsbDotNet.Main;

namespace MonoLibUsb.Profile;

public class MonoUsbConfigHandle : SafeContextHandle
{
	private MonoUsbConfigHandle()
		: base(IntPtr.Zero, ownsHandle: true)
	{
	}

	protected override bool ReleaseHandle()
	{
		if (!IsInvalid)
		{
			MonoUsbApi.FreeConfigDescriptor(handle);
			SetHandleAsInvalid();
		}
		return true;
	}
}
