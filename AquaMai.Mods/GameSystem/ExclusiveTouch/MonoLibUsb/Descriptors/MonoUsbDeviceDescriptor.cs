using System.Runtime.InteropServices;
using LibUsbDotNet.Descriptors;
using LibUsbDotNet.Main;

namespace MonoLibUsb.Descriptors;

[StructLayout(LayoutKind.Sequential)]
public class MonoUsbDeviceDescriptor
{
	public static readonly int Size = Marshal.SizeOf(typeof(MonoUsbDeviceDescriptor));

	public byte Length;

	public DescriptorType DescriptorType;

	public readonly short BcdUsb;

	public readonly ClassCodeType Class;

	public readonly byte SubClass;

	public readonly byte Protocol;

	public readonly byte MaxPacketSize0;

	public readonly short VendorID;

	public readonly short ProductID;

	public readonly short BcdDevice;

	public readonly byte ManufacturerStringIndex;

	public readonly byte ProductStringIndex;

	public readonly byte SerialStringIndex;

	public readonly byte ConfigurationCount;

	public override string ToString()
	{
		object[] array = new object[14]
		{
			Length,
			DescriptorType,
			"0x" + BcdUsb.ToString("X4"),
			Class,
			SubClass,
			Protocol,
			MaxPacketSize0,
			"0x" + VendorID.ToString("X4"),
			"0x" + ProductID.ToString("X4"),
			BcdDevice,
			ManufacturerStringIndex,
			ProductStringIndex,
			SerialStringIndex,
			ConfigurationCount
		};
		string[] array2 = new string[14]
		{
			"Length", "DescriptorType", "BcdUsb", "Class", "SubClass", "Protocol", "MaxPacketSize0", "VendorID", "ProductID", "BcdDevice",
			"ManufacturerStringIndex", "ProductStringIndex", "SerialStringIndex", "ConfigurationCount"
		};
		return Helper.ToString("", array2, ":", array, "\r\n");
	}
}
