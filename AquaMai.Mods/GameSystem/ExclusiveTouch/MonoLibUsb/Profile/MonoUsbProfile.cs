using LibUsbDotNet;
using LibUsbDotNet.Main;
using MonoLibUsb.Descriptors;

namespace MonoLibUsb.Profile;

public class MonoUsbProfile
{
	private readonly byte mBusNumber;

	private readonly byte mDeviceAddress;

	private readonly MonoUsbDeviceDescriptor mMonoUsbDeviceDescriptor = new MonoUsbDeviceDescriptor();

	private readonly MonoUsbProfileHandle mMonoUSBProfileHandle;

	internal bool mDiscovered;

	public MonoUsbDeviceDescriptor DeviceDescriptor => mMonoUsbDeviceDescriptor;

	public byte BusNumber => mBusNumber;

	public byte DeviceAddress => mDeviceAddress;

	public MonoUsbProfileHandle ProfileHandle => mMonoUSBProfileHandle;

	internal MonoUsbProfile(MonoUsbProfileHandle monoUSBProfileHandle)
	{
		mMonoUSBProfileHandle = monoUSBProfileHandle;
		mBusNumber = MonoUsbApi.GetBusNumber(mMonoUSBProfileHandle);
		mDeviceAddress = MonoUsbApi.GetDeviceAddress(mMonoUSBProfileHandle);
		GetDeviceDescriptor(out mMonoUsbDeviceDescriptor);
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
		{
			return false;
		}
		if (this == obj)
		{
			return true;
		}
		if (obj.GetType() != typeof(MonoUsbProfile))
		{
			return false;
		}
		return Equals((MonoUsbProfile)obj);
	}

	public override int GetHashCode()
	{
		return (mBusNumber.GetHashCode() * 397) ^ mDeviceAddress.GetHashCode();
	}

	public static bool operator ==(MonoUsbProfile left, MonoUsbProfile right)
	{
		return object.Equals(left, right);
	}

	public static bool operator !=(MonoUsbProfile left, MonoUsbProfile right)
	{
		return !object.Equals(left, right);
	}

	private MonoUsbError GetDeviceDescriptor(out MonoUsbDeviceDescriptor monoUsbDeviceDescriptor)
	{
		MonoUsbError monoUsbError = MonoUsbError.Success;
		monoUsbDeviceDescriptor = new MonoUsbDeviceDescriptor();
		monoUsbError = (MonoUsbError)MonoUsbApi.GetDeviceDescriptor(mMonoUSBProfileHandle, monoUsbDeviceDescriptor);
		if (monoUsbError != MonoUsbError.Success)
		{
			UsbError.Error(ErrorCode.MonoApiError, (int)monoUsbError, "GetDeviceDescriptor Failed", this);
			monoUsbDeviceDescriptor = null;
		}
		return monoUsbError;
	}

	public void Close()
	{
		if (!mMonoUSBProfileHandle.IsClosed)
		{
			mMonoUSBProfileHandle.Dispose();
		}
	}

	public MonoUsbDeviceHandle OpenDeviceHandle()
	{
		return new MonoUsbDeviceHandle(ProfileHandle);
	}

	public bool Equals(MonoUsbProfile other)
	{
		if ((object)other == null)
		{
			return false;
		}
		if ((object)this == other)
		{
			return true;
		}
		if (other.mBusNumber == mBusNumber)
		{
			return other.mDeviceAddress == mDeviceAddress;
		}
		return false;
	}

	public string MakeDevicePath()
	{
		return $"usbdev{BusNumber}.{DeviceAddress}";
	}
}
