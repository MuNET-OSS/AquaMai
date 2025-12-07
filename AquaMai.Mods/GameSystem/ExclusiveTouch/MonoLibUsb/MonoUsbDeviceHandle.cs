using System;
using LibUsbDotNet.Main;
using MonoLibUsb.Profile;

namespace MonoLibUsb;

public class MonoUsbDeviceHandle : SafeContextHandle
{
	private static object handleLOCK = new object();

	private static MonoUsbError mLastReturnCode;

	private static string mLastReturnString = string.Empty;

	public static string LastErrorString
	{
		get
		{
			lock (handleLOCK)
			{
				return mLastReturnString;
			}
		}
	}

	public static MonoUsbError LastErrorCode
	{
		get
		{
			lock (handleLOCK)
			{
				return mLastReturnCode;
			}
		}
	}

	public MonoUsbDeviceHandle(MonoUsbProfileHandle profileHandle)
		: base(IntPtr.Zero)
	{
		IntPtr deviceHandle = IntPtr.Zero;
		int num = MonoUsbApi.Open(profileHandle, ref deviceHandle);
		if (num < 0 || deviceHandle == IntPtr.Zero)
		{
			lock (handleLOCK)
			{
				mLastReturnCode = (MonoUsbError)num;
				mLastReturnString = MonoUsbApi.StrError(mLastReturnCode);
			}
			SetHandleAsInvalid();
		}
		else
		{
			SetHandle(deviceHandle);
		}
	}

	internal MonoUsbDeviceHandle(IntPtr pDeviceHandle)
		: base(pDeviceHandle)
	{
	}

	protected override bool ReleaseHandle()
	{
		if (!IsInvalid)
		{
			MonoUsbApi.Close(handle);
			SetHandleAsInvalid();
		}
		return true;
	}
}
