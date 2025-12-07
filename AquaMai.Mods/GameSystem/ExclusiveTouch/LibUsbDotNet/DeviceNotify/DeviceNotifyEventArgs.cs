using System;
using LibUsbDotNet.DeviceNotify.Info;

namespace LibUsbDotNet.DeviceNotify;

public abstract class DeviceNotifyEventArgs : EventArgs
{
	public IVolumeNotifyInfo Volume { get; protected set; }

	public IPortNotifyInfo Port { get; protected set; }

	public IUsbDeviceNotifyInfo Device { get; protected set; }

	public EventType EventType { get; protected set; }

	public DeviceType DeviceType { get; protected set; }

	public object Object { get; protected set; }

	public override string ToString()
	{
		return $"[DeviceType:{DeviceType}] [EventType:{EventType}] {Object}";
	}
}
