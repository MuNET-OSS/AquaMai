using System;
using System.Runtime.InteropServices;
using LibUsbDotNet.DeviceNotify.Internal;

namespace LibUsbDotNet.DeviceNotify.Info;

public class PortNotifyInfo : IPortNotifyInfo
{
	private readonly DevBroadcastPort mBaseHdr = new DevBroadcastPort();

	private readonly string mName;

	public string Name => mName;

	internal PortNotifyInfo(IntPtr lParam)
	{
		Marshal.PtrToStructure(lParam, mBaseHdr);
		IntPtr ptr = new IntPtr(lParam.ToInt64() + Marshal.OffsetOf(typeof(DevBroadcastPort), "mNameHolder").ToInt64());
		mName = Marshal.PtrToStringUni(ptr);
	}

	public override string ToString()
	{
		return $"[Port Name:{Name}] ";
	}
}
