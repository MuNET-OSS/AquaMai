using System.Threading;
using LibUsbDotNet.Main;

namespace MonoLibUsb;

public static class MonoUsbEventHandler
{
	private static readonly ManualResetEvent mIsStoppedEvent = new ManualResetEvent(initialState: true);

	private static bool mRunning;

	private static MonoUsbSessionHandle mSessionHandle;

	internal static Thread mUsbEventThread;

	private static ThreadPriority mPriority = ThreadPriority.Normal;

	private static UnixNativeTimeval mWaitUnixNativeTimeval;

	public static MonoUsbSessionHandle SessionHandle => mSessionHandle;

	public static bool IsStopped => mIsStoppedEvent.WaitOne(0);

	public static ThreadPriority Priority
	{
		get
		{
			return mPriority;
		}
		set
		{
			mPriority = value;
		}
	}

	public static void Exit()
	{
		Stop(bWait: true);
		if (mSessionHandle != null && !mSessionHandle.IsInvalid)
		{
			mSessionHandle.Dispose();
			mSessionHandle = null;
		}
	}

	private static void HandleEventFn(object oHandle)
	{
		MonoUsbSessionHandle sessionHandle = oHandle as MonoUsbSessionHandle;
		mIsStoppedEvent.Reset();
		while (mRunning)
		{
			MonoUsbApi.HandleEventsTimeout(sessionHandle, ref mWaitUnixNativeTimeval);
		}
		mIsStoppedEvent.Set();
	}

	public static void Init(long tvSec, long tvUsec)
	{
		Init(new UnixNativeTimeval(tvSec, tvUsec));
	}

	public static void Init()
	{
		Init(UnixNativeTimeval.Default);
	}

	private static void Init(UnixNativeTimeval unixNativeTimeval)
	{
		if (IsStopped && !mRunning && mSessionHandle == null)
		{
			mWaitUnixNativeTimeval = unixNativeTimeval;
			mSessionHandle = new MonoUsbSessionHandle();
			if (mSessionHandle.IsInvalid)
			{
				mSessionHandle = null;
				throw new UsbException(typeof(MonoUsbApi), $"Init:libusb_init Failed:Invalid Session Handle");
			}
		}
	}

	public static bool Start()
	{
		if (IsStopped && !mRunning && mSessionHandle != null)
		{
			mRunning = true;
			mUsbEventThread = new Thread(HandleEventFn);
			mUsbEventThread.Priority = mPriority;
			mUsbEventThread.Start(mSessionHandle);
		}
		return true;
	}

	public static void Stop(bool bWait)
	{
		if (!IsStopped && mRunning)
		{
			mRunning = false;
			if (bWait && !mUsbEventThread.Join((int)((double)(mWaitUnixNativeTimeval.tv_sec * 1000 + mWaitUnixNativeTimeval.tv_usec) * 1.2)))
			{
				mUsbEventThread.Abort();
				throw new UsbException(typeof(MonoUsbEventHandler), "Critical timeout failure! MonoUsbApi.HandleEventsTimeout did not return within the allotted time.");
			}
			mUsbEventThread = null;
		}
	}
}
