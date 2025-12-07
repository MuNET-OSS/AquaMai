using System;

namespace LibUsbDotNet.DeviceNotify;

public interface IDeviceNotifier
{
	bool Enabled { get; set; }

	event EventHandler<DeviceNotifyEventArgs> OnDeviceNotify;
}
