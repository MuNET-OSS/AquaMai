using System;
using LibUsbDotNet.Main;

namespace MonoLibUsb;

public class MonoUsbSessionHandle : SafeContextHandle
{
	private static object sessionLOCK = new object();

	private static MonoUsbError mLastReturnCode;

	private static string mLastReturnString = string.Empty;

	private static int mSessionCount;

	private static string DLL_NOT_FOUND_LINUX = "libusb-1.0 library not found.  This is often an indication that libusb-1.0 was installed to '/usr/local/lib' and mono.net is not looking for it there. To resolve this, add the path '/usr/local/lib' to '/etc/ld.so.conf' and run 'ldconfig' as root. (http://www.mono-project.com/DllNotFoundException)";

	private static string DLL_NOT_FOUND_WINDOWS = "libusb-1.0.dll not found. If this is a 64bit operating system, ensure that the 64bit version of libusb-1.0.dll exists in the '\\Windows\\System32' directory.";

	public static MonoUsbError LastErrorCode
	{
		get
		{
			lock (sessionLOCK)
			{
				return mLastReturnCode;
			}
		}
	}

	public static string LastErrorString
	{
		get
		{
			lock (sessionLOCK)
			{
				return mLastReturnString;
			}
		}
	}

	public MonoUsbSessionHandle()
		: base(IntPtr.Zero, ownsHandle: true)
	{
		lock (sessionLOCK)
		{
			IntPtr pContext = IntPtr.Zero;
			try
			{
				mLastReturnCode = (MonoUsbError)MonoUsbApi.Init(ref pContext);
			}
			catch (DllNotFoundException inner)
			{
				if (Helper.IsLinux)
				{
					throw new DllNotFoundException(DLL_NOT_FOUND_LINUX, inner);
				}
				throw new DllNotFoundException(DLL_NOT_FOUND_WINDOWS, inner);
			}
			if (mLastReturnCode < MonoUsbError.Success)
			{
				mLastReturnString = MonoUsbApi.StrError(mLastReturnCode);
				SetHandleAsInvalid();
			}
			else
			{
				SetHandle(pContext);
				mSessionCount++;
			}
		}
	}

	protected override bool ReleaseHandle()
	{
		if (!IsInvalid)
		{
			lock (sessionLOCK)
			{
				MonoUsbApi.Exit(handle);
				SetHandleAsInvalid();
				mSessionCount--;
			}
		}
		return true;
	}
}
