using System;

namespace MonoLibUsb.Profile;

public class AddRemoveEventArgs : EventArgs
{
	private readonly AddRemoveType mAddRemoveType;

	private readonly MonoUsbProfile mMonoUSBProfile;

	public MonoUsbProfile MonoUSBProfile => mMonoUSBProfile;

	public AddRemoveType EventType => mAddRemoveType;

	internal AddRemoveEventArgs(MonoUsbProfile monoUSBProfile, AddRemoveType addRemoveType)
	{
		mMonoUSBProfile = monoUSBProfile;
		mAddRemoveType = addRemoveType;
	}
}
