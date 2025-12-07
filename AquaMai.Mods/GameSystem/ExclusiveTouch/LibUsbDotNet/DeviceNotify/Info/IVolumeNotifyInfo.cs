namespace LibUsbDotNet.DeviceNotify.Info;

public interface IVolumeNotifyInfo
{
	string Letter { get; }

	bool ChangeAffectsMediaInDrive { get; }

	bool IsNetworkVolume { get; }

	short Flags { get; }

	int Unitmask { get; }

	new string ToString();
}
