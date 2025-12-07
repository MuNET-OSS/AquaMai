using System.Runtime.InteropServices;

namespace MonoLibUsb.Descriptors;

[StructLayout(LayoutKind.Sequential)]
public class MonoUsbVersion
{
	public readonly ushort Major;

	public readonly ushort Minor;

	public readonly ushort Micro;

	public readonly ushort Nano;

	[MarshalAs(UnmanagedType.LPStr)]
	public readonly string RC;

	[MarshalAs(UnmanagedType.LPStr)]
	public readonly string Describe;
}
