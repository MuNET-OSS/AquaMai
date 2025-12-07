namespace LibUsbDotNet.DeviceNotify;

public enum EventType
{
	CustomEvent = 32774,
	DeviceArrival = 32768,
	DeviceQueryRemove = 32769,
	DeviceQueryRemoveFailed = 32770,
	DeviceRemoveComplete = 32772,
	DeviceRemovePending = 32771,
	DeviceTypeSpecific = 32773
}
