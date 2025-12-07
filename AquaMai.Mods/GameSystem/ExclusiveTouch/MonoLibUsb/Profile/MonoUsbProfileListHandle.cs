using System;
using System.Collections;
using System.Collections.Generic;
using LibUsbDotNet.Main;

namespace MonoLibUsb.Profile;

public class MonoUsbProfileListHandle : SafeContextHandle, IEnumerable<MonoUsbProfileHandle>, IEnumerable
{
	private MonoUsbProfileListHandle()
		: base(IntPtr.Zero)
	{
	}

	internal MonoUsbProfileListHandle(IntPtr pHandleToOwn)
		: base(pHandleToOwn)
	{
	}

	public IEnumerator<MonoUsbProfileHandle> GetEnumerator()
	{
		return new MonoUsbProfileHandleEnumerator(this);
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	protected override bool ReleaseHandle()
	{
		if (!IsInvalid)
		{
			MonoUsbApi.FreeDeviceList(handle, 1);
			SetHandleAsInvalid();
		}
		return true;
	}
}
