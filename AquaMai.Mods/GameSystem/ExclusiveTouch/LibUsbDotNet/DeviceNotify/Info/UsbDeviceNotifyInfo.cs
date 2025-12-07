using System;
using System.Runtime.InteropServices;
using LibUsbDotNet.DeviceNotify.Internal;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;

namespace LibUsbDotNet.DeviceNotify.Info;

public class UsbDeviceNotifyInfo : IUsbDeviceNotifyInfo
{
	private readonly DevBroadcastDeviceinterface mBaseHdr = new DevBroadcastDeviceinterface();

	private readonly string mName;

	private UsbSymbolicName mSymbolicName;

	public UsbSymbolicName SymbolicName
	{
		get
		{
			if (mSymbolicName == null)
			{
				mSymbolicName = new UsbSymbolicName(mName);
			}
			return mSymbolicName;
		}
	}

	public string Name => mName;

	public Guid ClassGuid => SymbolicName.ClassGuid;

	public int IdVendor => SymbolicName.Vid;

	public int IdProduct => SymbolicName.Pid;

	public string SerialNumber => SymbolicName.SerialNumber;

	internal UsbDeviceNotifyInfo(IntPtr lParam)
	{
		Marshal.PtrToStructure(lParam, mBaseHdr);
		IntPtr ptr = new IntPtr(lParam.ToInt64() + Marshal.OffsetOf(typeof(DevBroadcastDeviceinterface), "mNameHolder").ToInt64());
		mName = Marshal.PtrToStringUni(ptr);
	}

	public override string ToString()
	{
		return SymbolicName.ToString();
	}

	public bool Open(out UsbDevice usbDevice)
	{
		LibUsbDevice usbDevice2;
		bool result = LibUsbDevice.Open(Name, out usbDevice2);
		usbDevice = usbDevice2;
		return result;
	}
}
