using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using LibUsbDotNet.Descriptors;
using LibUsbDotNet.Info;
using LibUsbDotNet.Main;
using MonoLibUsb;
using MonoLibUsb.Descriptors;
using MonoLibUsb.Profile;

namespace LibUsbDotNet.LudnMonoLibUsb;

public class MonoUsbDevice : UsbDevice, IUsbDevice, IUsbInterface
{
	internal static readonly object OLockDeviceList = new object();

	internal static MonoUsbProfileList mMonoUSBProfileList;

	private readonly MonoUsbProfile mMonoUSBProfile;

	public static int ControlTransferTimeout_ms { get; set; } = 1000;

	internal static MonoUsbProfileList ProfileList
	{
		get
		{
			lock (OLockDeviceList)
			{
				MonoUsbApi.InitAndStart();
				if (mMonoUSBProfileList == null)
				{
					mMonoUSBProfileList = new MonoUsbProfileList();
				}
				return mMonoUSBProfileList;
			}
		}
	}

	public static List<MonoUsbDevice> MonoUsbDeviceList
	{
		get
		{
			lock (OLockDeviceList)
			{
				MonoUsbApi.InitAndStart();
				if (mMonoUSBProfileList == null)
				{
					mMonoUSBProfileList = new MonoUsbProfileList();
				}
				if (mMonoUSBProfileList.Refresh(MonoUsbEventHandler.SessionHandle) < 0)
				{
					return null;
				}
				List<MonoUsbDevice> list = new List<MonoUsbDevice>();
				for (int i = 0; i < mMonoUSBProfileList.Count; i++)
				{
					MonoUsbProfile monoUSBProfile = mMonoUSBProfileList[i];
					if (monoUSBProfile.DeviceDescriptor.BcdUsb != 0)
					{
						MonoUsbDevice item = new MonoUsbDevice(ref monoUSBProfile);
						list.Add(item);
					}
				}
				return list;
			}
		}
	}

	public byte DeviceAddress => mMonoUSBProfile.DeviceAddress;

	public byte BusNumber => mMonoUSBProfile.BusNumber;

	public override DriverModeType DriverMode
	{
		get
		{
			if (UsbDevice.IsLinux)
			{
				return DriverModeType.MonoLibUsb;
			}
			return DriverModeType.LibUsbWinBack;
		}
	}

	public MonoUsbProfile Profile => mMonoUSBProfile;

	public override UsbRegistry UsbRegistryInfo => null;

	public override ReadOnlyCollection<UsbConfigInfo> Configs
	{
		get
		{
			if (mConfigs == null)
			{
				if (!base.IsOpen)
				{
					return null;
				}
				GetConfigs(this, out mConfigs);
			}
			return mConfigs.AsReadOnly();
		}
	}

	public override UsbDeviceInfo Info
	{
		get
		{
			if (mDeviceInfo == null)
			{
				mDeviceInfo = new UsbDeviceInfo(this, mMonoUSBProfile.DeviceDescriptor);
			}
			return mDeviceInfo;
		}
	}

	public override string DevicePath => mMonoUSBProfile.MakeDevicePath();

	internal MonoUsbDevice(ref MonoUsbProfile monoUSBProfile)
		: base(null, null)
	{
		mMonoUSBProfile = monoUSBProfile;
		mCachedDeviceDescriptor = new UsbDeviceDescriptor(monoUSBProfile.DeviceDescriptor);
	}

	public bool ResetDevice()
	{
		if (!base.IsOpen)
		{
			throw new UsbException(this, "Device is not opened.");
		}
		base.ActiveEndpoints.Clear();
		int num;
		if ((num = MonoUsbApi.ResetDevice((MonoUsbDeviceHandle)mUsbHandle)) != 0)
		{
			UsbError.Error(ErrorCode.MonoApiError, num, "ResetDevice Failed", this);
		}
		else
		{
			Close();
		}
		return num == 0;
	}

	public override bool Close()
	{
		base.ActiveEndpoints.Clear();
		if (base.IsOpen)
		{
			mUsbHandle.Dispose();
		}
		return true;
	}

	public override bool ControlTransfer(ref UsbSetupPacket setupPacket, IntPtr buffer, int bufferLength, out int lengthTransferred)
	{
		int num = MonoUsbApi.ControlTransferAsync((MonoUsbDeviceHandle)mUsbHandle, setupPacket.RequestType, setupPacket.Request, setupPacket.Value, setupPacket.Index, buffer, (short)bufferLength, ControlTransferTimeout_ms);
		if (num < 0)
		{
			UsbError.Error(ErrorCode.MonoApiError, num, "ControlTransfer Failed", this);
			lengthTransferred = 0;
			return false;
		}
		lengthTransferred = num;
		return true;
	}

	public override bool GetDescriptor(byte descriptorType, byte index, short langId, IntPtr buffer, int bufferLength, out int transferLength)
	{
		transferLength = 0;
		bool result = false;
		bool isOpen = base.IsOpen;
		if (!isOpen)
		{
			Open();
		}
		if (!base.IsOpen)
		{
			return false;
		}
		int descriptor = MonoUsbApi.GetDescriptor((MonoUsbDeviceHandle)mUsbHandle, descriptorType, index, langId, buffer, (ushort)bufferLength);
		if (descriptor < 0)
		{
			UsbError.Error(ErrorCode.MonoApiError, descriptor, "GetDescriptor Failed", this);
		}
		else
		{
			result = true;
			transferLength = descriptor;
		}
		if (!isOpen && base.IsOpen)
		{
			Close();
		}
		return result;
	}

	public override bool Open()
	{
		if (base.IsOpen)
		{
			return true;
		}
		MonoUsbDeviceHandle monoUsbDeviceHandle = new MonoUsbDeviceHandle(mMonoUSBProfile.ProfileHandle);
		if (monoUsbDeviceHandle.IsInvalid)
		{
			UsbError.Error(ErrorCode.MonoApiError, (int)MonoUsbDeviceHandle.LastErrorCode, "MonoUsbDevice.Open Failed", this);
			mUsbHandle = null;
			return false;
		}
		mUsbHandle = monoUsbDeviceHandle;
		if (base.IsOpen)
		{
			return true;
		}
		mUsbHandle.Dispose();
		return false;
	}

	public override UsbEndpointReader OpenEndpointReader(ReadEndpointID readEndpointID, int readBufferSize, EndpointType endpointType)
	{
		foreach (UsbEndpointBase mActiveEndpoint in mActiveEndpoints)
		{
			if ((uint)mActiveEndpoint.EpNum == (uint)readEndpointID)
			{
				return (UsbEndpointReader)mActiveEndpoint;
			}
		}
		byte alternateInterfaceID = ((mClaimedInterfaces.Count == 0) ? UsbAltInterfaceSettings[0] : UsbAltInterfaceSettings[mClaimedInterfaces[mClaimedInterfaces.Count - 1]]);
		UsbEndpointReader item = new MonoUsbEndpointReader(this, readBufferSize, alternateInterfaceID, readEndpointID, endpointType);
		return (UsbEndpointReader)base.ActiveEndpoints.Add(item);
	}

	public override UsbEndpointWriter OpenEndpointWriter(WriteEndpointID writeEndpointID, EndpointType endpointType)
	{
		foreach (UsbEndpointBase activeEndpoint in base.ActiveEndpoints)
		{
			if ((uint)activeEndpoint.EpNum == (uint)writeEndpointID)
			{
				return (UsbEndpointWriter)activeEndpoint;
			}
		}
		byte alternateInterfaceID = ((mClaimedInterfaces.Count == 0) ? UsbAltInterfaceSettings[0] : UsbAltInterfaceSettings[mClaimedInterfaces[mClaimedInterfaces.Count - 1]]);
		UsbEndpointWriter item = new MonoUsbEndpointWriter(this, alternateInterfaceID, writeEndpointID, endpointType);
		return (UsbEndpointWriter)mActiveEndpoints.Add(item);
	}

	public bool SetConfiguration(byte config)
	{
		int num = MonoUsbApi.SetConfiguration((MonoUsbDeviceHandle)mUsbHandle, config);
		if (num != 0)
		{
			UsbError.Error(ErrorCode.MonoApiError, num, "SetConfiguration Failed", this);
			return false;
		}
		mCurrentConfigValue = config;
		return true;
	}

	public override bool GetConfiguration(out byte config)
	{
		config = 0;
		int configuration = 0;
		int configuration2 = MonoUsbApi.GetConfiguration((MonoUsbDeviceHandle)mUsbHandle, ref configuration);
		if (configuration2 != 0)
		{
			UsbError.Error(ErrorCode.MonoApiError, configuration2, "GetConfiguration Failed", this);
			return false;
		}
		config = (byte)configuration;
		mCurrentConfigValue = config;
		return true;
	}

	public bool ClaimInterface(int interfaceID)
	{
		if (mClaimedInterfaces.Contains(interfaceID))
		{
			return true;
		}
		int num = MonoUsbApi.ClaimInterface((MonoUsbDeviceHandle)mUsbHandle, interfaceID);
		if (num != 0)
		{
			UsbError.Error(ErrorCode.MonoApiError, num, "ClaimInterface Failed", this);
			return false;
		}
		mClaimedInterfaces.Add(interfaceID);
		return true;
	}

	public bool GetAltInterface(out int alternateID)
	{
		int interfaceID = ((mClaimedInterfaces.Count != 0) ? mClaimedInterfaces[mClaimedInterfaces.Count - 1] : 0);
		return GetAltInterface(interfaceID, out alternateID);
	}

	public bool GetAltInterface(int interfaceID, out int alternateID)
	{
		alternateID = UsbAltInterfaceSettings[interfaceID & 0xFF];
		return true;
	}

	public bool ReleaseInterface(int interfaceID)
	{
		int num = MonoUsbApi.ReleaseInterface((MonoUsbDeviceHandle)mUsbHandle, interfaceID);
		if (!mClaimedInterfaces.Remove(interfaceID))
		{
			return true;
		}
		if (num != 0)
		{
			UsbError.Error(ErrorCode.MonoApiError, num, "ReleaseInterface Failed", this);
			return false;
		}
		return true;
	}

	public bool SetAltInterface(int interfaceID, int alternateID)
	{
		int num = MonoUsbApi.SetInterfaceAltSetting((MonoUsbDeviceHandle)mUsbHandle, interfaceID, alternateID);
		if (num != 0)
		{
			UsbError.Error(ErrorCode.MonoApiError, num, "SetAltInterface Failed", this);
			return false;
		}
		UsbAltInterfaceSettings[interfaceID & 0xFF] = (byte)alternateID;
		return true;
	}

	public bool SetAltInterface(int alternateID)
	{
		if (mClaimedInterfaces.Count == 0)
		{
			throw new UsbException(this, "You must claim an interface before setting an alternate interface.");
		}
		return SetAltInterface(mClaimedInterfaces[mClaimedInterfaces.Count - 1], alternateID);
	}

	private static ErrorCode GetConfigs(MonoUsbDevice usbDevice, out List<UsbConfigInfo> configInfoListRtn)
	{
		configInfoListRtn = new List<UsbConfigInfo>();
		new List<MonoUsbConfigDescriptor>();
		int configurationCount = usbDevice.Info.Descriptor.ConfigurationCount;
		for (int i = 0; i < configurationCount; i++)
		{
			MonoUsbConfigHandle configHandle;
			int configDescriptor = MonoUsbApi.GetConfigDescriptor(usbDevice.mMonoUSBProfile.ProfileHandle, (byte)i, out configHandle);
			if (configDescriptor != 0 || configHandle.IsInvalid)
			{
				return UsbError.Error(ErrorCode.MonoApiError, configDescriptor, $"GetConfigDescriptor Failed at index:{i}", usbDevice).ErrorCode;
			}
			try
			{
				MonoUsbConfigDescriptor monoUsbConfigDescriptor = new MonoUsbConfigDescriptor();
				Marshal.PtrToStructure(configHandle.DangerousGetHandle(), monoUsbConfigDescriptor);
				UsbConfigInfo item = new UsbConfigInfo(usbDevice, monoUsbConfigDescriptor);
				configInfoListRtn.Add(item);
			}
			catch (Exception ex)
			{
				UsbError.Error(ErrorCode.InvalidConfig, Marshal.GetLastWin32Error(), ex.ToString(), usbDevice);
			}
			finally
			{
				if (!configHandle.IsInvalid)
				{
					configHandle.Dispose();
				}
			}
		}
		return ErrorCode.None;
	}

	internal static int RefreshProfileList()
	{
		lock (OLockDeviceList)
		{
			MonoUsbApi.InitAndStart();
			if (mMonoUSBProfileList == null)
			{
				mMonoUSBProfileList = new MonoUsbProfileList();
			}
			return mMonoUSBProfileList.Refresh(MonoUsbEventHandler.SessionHandle);
		}
	}

	public static void Init()
	{
		MonoUsbApi.InitAndStart();
	}

	public bool DetachKernelDriver(int interfaceID)
	{
		int num = MonoUsbApi.DetachKernelDriver((MonoUsbDeviceHandle)mUsbHandle, interfaceID);
		if (num != 0)
		{
			UsbError.Error(ErrorCode.MonoApiError, num, "Detach Kernel Driver Failed", this);
			return false;
		}
		return true;
	}

	public bool SetAutoDetachKernelDriver(bool autoDetach)
	{
		int num = MonoUsbApi.SetAutoDetachKernelDriver((MonoUsbDeviceHandle)mUsbHandle, autoDetach ? 1 : 0);
		if (num != 0)
		{
			UsbError.Error(ErrorCode.MonoApiError, num, "Set Auto Detach Kernel Driver Failed", this);
			return false;
		}
		return true;
	}
}
