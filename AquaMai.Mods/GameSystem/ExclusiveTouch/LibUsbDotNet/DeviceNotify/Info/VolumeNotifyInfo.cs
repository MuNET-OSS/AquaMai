using System;
using System.Runtime.InteropServices;
using LibUsbDotNet.DeviceNotify.Internal;

namespace LibUsbDotNet.DeviceNotify.Info;

public class VolumeNotifyInfo : IVolumeNotifyInfo
{
	private const int DBTF_MEDIA = 1;

	private const int DBTF_NET = 2;

	private readonly DevBroadcastVolume mBaseHdr = new DevBroadcastVolume();

	public string Letter
	{
		get
		{
			int num = Unitmask;
			for (byte b = 65; b < 97; b++)
			{
				byte b2 = b;
				if (b2 > 90)
				{
					b2 -= 43;
				}
				if ((num & 1) == 1)
				{
					char c = (char)b2;
					return c.ToString();
				}
				num >>= 1;
			}
			return '?'.ToString();
		}
	}

	public bool ChangeAffectsMediaInDrive => (Flags & 1) == 1;

	public bool IsNetworkVolume => (Flags & 2) == 2;

	public short Flags => mBaseHdr.Flags;

	public int Unitmask => mBaseHdr.UnitMask;

	internal VolumeNotifyInfo(IntPtr lParam)
	{
		Marshal.PtrToStructure(lParam, mBaseHdr);
	}

	public override string ToString()
	{
		return $"[Letter:{Letter}] [IsNetworkVolume:{IsNetworkVolume}] [ChangeAffectsMediaInDrive:{ChangeAffectsMediaInDrive}] ";
	}
}
