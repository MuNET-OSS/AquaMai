using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MonoLibUsb.Profile;

internal class MonoUsbProfileHandleEnumerator : IEnumerator<MonoUsbProfileHandle>, IDisposable, IEnumerator
{
	private readonly MonoUsbProfileListHandle mProfileListHandle;

	private MonoUsbProfileHandle mCurrentProfile;

	private int mNextDeviceProfilePos;

	public MonoUsbProfileHandle Current => mCurrentProfile;

	object IEnumerator.Current => Current;

	internal MonoUsbProfileHandleEnumerator(MonoUsbProfileListHandle profileListHandle)
	{
		mProfileListHandle = profileListHandle;
		Reset();
	}

	public void Dispose()
	{
		Reset();
	}

	public bool MoveNext()
	{
		IntPtr intPtr = Marshal.ReadIntPtr(new IntPtr(mProfileListHandle.DangerousGetHandle().ToInt64() + mNextDeviceProfilePos * IntPtr.Size));
		if (intPtr != IntPtr.Zero)
		{
			mCurrentProfile = new MonoUsbProfileHandle(intPtr);
			mNextDeviceProfilePos++;
			return true;
		}
		mCurrentProfile = null;
		return false;
	}

	public void Reset()
	{
		mNextDeviceProfilePos = 0;
		mCurrentProfile = null;
	}
}
