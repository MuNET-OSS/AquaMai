using System;
using LibUsbDotNet.Main;

namespace LibUsbDotNet.DeviceNotify.Info;

public interface IUsbDeviceNotifyInfo
{
	UsbSymbolicName SymbolicName { get; }

	string Name { get; }

	Guid ClassGuid { get; }

	int IdVendor { get; }

	int IdProduct { get; }

	string SerialNumber { get; }

	new string ToString();

	bool Open(out UsbDevice usbDevice);
}
