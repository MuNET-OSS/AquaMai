using System;
using LibUsbDotNet.Main;

namespace MonoLibUsb;

public struct UnixNativeTimeval
{
	private IntPtr mTvSecInternal;

	private IntPtr mTvUSecInternal;

	public static UnixNativeTimeval WindowsDefault => new UnixNativeTimeval(2L, 0L);

	public static UnixNativeTimeval LinuxDefault => new UnixNativeTimeval(2L, 0L);

	public static UnixNativeTimeval Default
	{
		get
		{
			if (!Helper.IsLinux)
			{
				return WindowsDefault;
			}
			return LinuxDefault;
		}
	}

	public long tv_sec
	{
		get
		{
			return mTvSecInternal.ToInt64();
		}
		set
		{
			mTvSecInternal = new IntPtr(value);
		}
	}

	public long tv_usec
	{
		get
		{
			return mTvUSecInternal.ToInt64();
		}
		set
		{
			mTvUSecInternal = new IntPtr(value);
		}
	}

	public UnixNativeTimeval(long tvSec, long tvUsec)
	{
		mTvSecInternal = new IntPtr(tvSec);
		mTvUSecInternal = new IntPtr(tvUsec);
	}
}
