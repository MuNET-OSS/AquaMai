using System;
using System.Runtime.InteropServices;

namespace MonoLibUsb.Profile;

[StructLayout(LayoutKind.Sequential)]
public class PollfdItem
{
	public readonly int fd;

	public readonly short events;

	internal PollfdItem(IntPtr pPollfd)
	{
		Marshal.PtrToStructure(pPollfd, this);
	}
}
