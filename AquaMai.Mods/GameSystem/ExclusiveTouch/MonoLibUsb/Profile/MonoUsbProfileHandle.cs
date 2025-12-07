using System;
using LibUsbDotNet.Main;

namespace MonoLibUsb.Profile;

public class MonoUsbProfileHandle : SafeContextHandle
{
	private static int mDeviceProfileRefCount;

	private static readonly object oDeviceProfileRefLock = new object();

	internal static int DeviceProfileRefCount
	{
		get
		{
			lock (oDeviceProfileRefLock)
			{
				return mDeviceProfileRefCount;
			}
		}
	}

	public MonoUsbProfileHandle(IntPtr pProfileHandle)
		: base(pProfileHandle, ownsHandle: true)
	{
		lock (oDeviceProfileRefLock)
		{
			MonoUsbApi.RefDevice(pProfileHandle);
			mDeviceProfileRefCount++;
		}
	}

	protected override bool ReleaseHandle()
	{
		lock (oDeviceProfileRefLock)
		{
			if (!IsInvalid)
			{
				MonoUsbApi.UnrefDevice(handle);
				mDeviceProfileRefCount--;
				SetHandleAsInvalid();
			}
			return true;
		}
	}
}
