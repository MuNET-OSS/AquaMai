using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LibUsbDotNet.Descriptors;
using LibUsbDotNet.Main;
using MonoLibUsb.Descriptors;

namespace LibUsbDotNet.Info;

public class UsbInterfaceInfo : UsbBaseInfo
{
	internal readonly UsbInterfaceDescriptor mUsbInterfaceDescriptor;

	internal List<UsbEndpointInfo> mEndpointInfo = new List<UsbEndpointInfo>();

	private string mInterfaceString;

	internal UsbDevice mUsbDevice;

	public UsbInterfaceDescriptor Descriptor => mUsbInterfaceDescriptor;

	public ReadOnlyCollection<UsbEndpointInfo> EndpointInfoList => mEndpointInfo.AsReadOnly();

	public string InterfaceString
	{
		get
		{
			if (mInterfaceString == null)
			{
				mInterfaceString = string.Empty;
				if (Descriptor.StringIndex > 0)
				{
					mUsbDevice.GetString(out mInterfaceString, mUsbDevice.Info.CurrentCultureLangID, Descriptor.StringIndex);
				}
			}
			return mInterfaceString;
		}
	}

	internal UsbInterfaceInfo(UsbDevice usbDevice, byte[] descriptor)
	{
		mUsbDevice = usbDevice;
		mUsbInterfaceDescriptor = new UsbInterfaceDescriptor();
		Helper.BytesToObject(descriptor, 0, Math.Min(UsbInterfaceDescriptor.Size, descriptor[0]), mUsbInterfaceDescriptor);
	}

	internal UsbInterfaceInfo(UsbDevice usbDevice, MonoUsbAltInterfaceDescriptor monoUSBAltInterfaceDescriptor)
	{
		mUsbDevice = usbDevice;
		if (monoUSBAltInterfaceDescriptor.ExtraLength > 0)
		{
			mRawDescriptors.Add(monoUSBAltInterfaceDescriptor.ExtraBytes);
		}
		mUsbInterfaceDescriptor = new UsbInterfaceDescriptor(monoUSBAltInterfaceDescriptor);
		foreach (MonoUsbEndpointDescriptor endpoint in monoUSBAltInterfaceDescriptor.EndpointList)
		{
			mEndpointInfo.Add(new UsbEndpointInfo(endpoint));
		}
	}

	public override string ToString()
	{
		return ToString("", UsbDescriptor.ToStringParamValueSeperator, UsbDescriptor.ToStringFieldSeperator);
	}

	public string ToString(string prefixSeperator, string entitySperator, string suffixSeperator)
	{
		object[] array = new object[1] { InterfaceString };
		string[] array2 = new string[1] { "InterfaceString" };
		return Descriptor.ToString(prefixSeperator, entitySperator, suffixSeperator) + Helper.ToString(prefixSeperator, array2, entitySperator, array, suffixSeperator);
	}
}
