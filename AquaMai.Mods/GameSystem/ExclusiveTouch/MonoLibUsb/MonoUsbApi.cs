using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using LibUsbDotNet.LudnMonoLibUsb;
using LibUsbDotNet.Main;
using MonoLibUsb.Descriptors;
using MonoLibUsb.Profile;
using MonoLibUsb.Transfer;

namespace MonoLibUsb;

public static class MonoUsbApi
{
	internal const CallingConvention CC = (CallingConvention)0;

	internal const string LIBUSB_DLL = "libusb-1.0";

	internal const int LIBUSB_PACK = 0;

	private static readonly MonoUsbTransferDelegate DefaultAsyncDelegate = DefaultAsyncCB;

	[DllImport("libusb-1.0", EntryPoint = "libusb_init")]
	internal static extern int Init(ref IntPtr pContext);

	[DllImport("libusb-1.0", EntryPoint = "libusb_exit")]
	internal static extern void Exit(IntPtr pContext);

	[DllImport("libusb-1.0", EntryPoint = "libusb_set_debug")]
	public static extern void SetDebug([In] MonoUsbSessionHandle sessionHandle, int level);

	[DllImport("libusb-1.0", EntryPoint = "libusb_get_version")]
	internal static extern IntPtr GetVersion();

	[DllImport("libusb-1.0", EntryPoint = "libusb_has_capability")]
	internal static extern int HasCapability(MonoUsbCapability capability);

	[DllImport("libusb-1.0", EntryPoint = "libusb_error_name")]
	internal static extern string ErrorName(int errcode);

	[DllImport("libusb-1.0", EntryPoint = "libusb_setlocale")]
	internal static extern int SetLocale(string locale);

	[DllImport("libusb-1.0", EntryPoint = "libusb_strerror")]
	private static extern IntPtr StrError(int errcode);

	[DllImport("libusb-1.0", EntryPoint = "libusb_get_device_list")]
	public static extern int GetDeviceList([In] MonoUsbSessionHandle sessionHandle, out MonoUsbProfileListHandle monoUSBProfileListHandle);

	[DllImport("libusb-1.0", EntryPoint = "libusb_free_device_list")]
	internal static extern void FreeDeviceList(IntPtr pHandleList, int unrefDevices);

	[DllImport("libusb-1.0", EntryPoint = "libusb_ref_device")]
	internal static extern IntPtr RefDevice(IntPtr pDeviceProfileHandle);

	[DllImport("libusb-1.0", EntryPoint = "libusb_unref_device")]
	internal static extern IntPtr UnrefDevice(IntPtr pDeviceProfileHandle);

	[DllImport("libusb-1.0", EntryPoint = "libusb_get_configuration")]
	public static extern int GetConfiguration([In] MonoUsbDeviceHandle deviceHandle, ref int configuration);

	[DllImport("libusb-1.0", EntryPoint = "libusb_get_device_descriptor")]
	public static extern int GetDeviceDescriptor([In] MonoUsbProfileHandle deviceProfileHandle, [Out] MonoUsbDeviceDescriptor deviceDescriptor);

	[DllImport("libusb-1.0", EntryPoint = "libusb_get_active_config_descriptor")]
	public static extern int GetActiveConfigDescriptor([In] MonoUsbProfileHandle deviceProfileHandle, out MonoUsbConfigHandle configHandle);

	[DllImport("libusb-1.0", EntryPoint = "libusb_get_config_descriptor")]
	public static extern int GetConfigDescriptor([In] MonoUsbProfileHandle deviceProfileHandle, byte configIndex, out MonoUsbConfigHandle configHandle);

	[DllImport("libusb-1.0", EntryPoint = "libusb_get_config_descriptor_by_value")]
	public static extern int GetConfigDescriptorByValue([In] MonoUsbProfileHandle deviceProfileHandle, byte bConfigurationValue, out MonoUsbConfigHandle configHandle);

	[DllImport("libusb-1.0", EntryPoint = "libusb_free_config_descriptor")]
	internal static extern void FreeConfigDescriptor(IntPtr pConfigDescriptor);

	[DllImport("libusb-1.0", EntryPoint = "libusb_get_bus_number")]
	public static extern byte GetBusNumber([In] MonoUsbProfileHandle deviceProfileHandle);

	[DllImport("libusb-1.0", EntryPoint = "libusb_get_device_address")]
	public static extern byte GetDeviceAddress([In] MonoUsbProfileHandle deviceProfileHandle);

	[DllImport("libusb-1.0", EntryPoint = "libusb_get_max_packet_size")]
	public static extern int GetMaxPacketSize([In] MonoUsbProfileHandle deviceProfileHandle, byte endpoint);

	[DllImport("libusb-1.0", EntryPoint = "libusb_get_max_iso_packet_size")]
	public static extern int GetMaxIsoPacketSize([In] MonoUsbProfileHandle deviceProfileHandle, byte endpoint);

	[DllImport("libusb-1.0", EntryPoint = "libusb_open")]
	internal static extern int Open([In] MonoUsbProfileHandle deviceProfileHandle, ref IntPtr deviceHandle);

	[DllImport("libusb-1.0", EntryPoint = "libusb_close")]
	internal static extern void Close(IntPtr deviceHandle);

	[DllImport("libusb-1.0", EntryPoint = "libusb_get_device")]
	private static extern IntPtr GetDeviceInternal([In] MonoUsbDeviceHandle devicehandle);

	[DllImport("libusb-1.0", EntryPoint = "libusb_set_configuration")]
	public static extern int SetConfiguration([In] MonoUsbDeviceHandle deviceHandle, int configuration);

	[DllImport("libusb-1.0", EntryPoint = "libusb_claim_interface")]
	public static extern int ClaimInterface([In] MonoUsbDeviceHandle deviceHandle, int interfaceNumber);

	[DllImport("libusb-1.0", EntryPoint = "libusb_release_interface")]
	public static extern int ReleaseInterface([In] MonoUsbDeviceHandle deviceHandle, int interfaceNumber);

	[DllImport("libusb-1.0", EntryPoint = "libusb_open_device_with_vid_pid")]
	private static extern IntPtr OpenDeviceWithVidPidInternal([In] MonoUsbSessionHandle sessionHandle, ushort vendorID, ushort productID);

	[DllImport("libusb-1.0", EntryPoint = "libusb_set_interface_alt_setting")]
	public static extern int SetInterfaceAltSetting([In] MonoUsbDeviceHandle deviceHandle, int interfaceNumber, int alternateSetting);

	[DllImport("libusb-1.0", EntryPoint = "libusb_clear_halt")]
	public static extern int ClearHalt([In] MonoUsbDeviceHandle deviceHandle, byte endpoint);

	[DllImport("libusb-1.0", EntryPoint = "libusb_reset_device")]
	public static extern int ResetDevice([In] MonoUsbDeviceHandle deviceHandle);

	[DllImport("libusb-1.0", EntryPoint = "libusb_kernel_driver_active")]
	public static extern int KernelDriverActive([In] MonoUsbDeviceHandle deviceHandle, int interfaceNumber);

	[DllImport("libusb-1.0", EntryPoint = "libusb_detach_kernel_driver")]
	public static extern int DetachKernelDriver([In] MonoUsbDeviceHandle deviceHandle, int interfaceNumber);

	[DllImport("libusb-1.0", EntryPoint = "libusb_set_auto_detach_kernel_driver")]
	public static extern int SetAutoDetachKernelDriver([In] MonoUsbDeviceHandle deviceHandle, int enable);

	[DllImport("libusb-1.0", EntryPoint = "libusb_attach_kernel_driver")]
	public static extern int AttachKernelDriver([In] MonoUsbDeviceHandle deviceHandle, int interfaceNumber);

	[DllImport("libusb-1.0", EntryPoint = "libusb_alloc_transfer")]
	internal static extern IntPtr AllocTransfer(int isoPackets);

	[DllImport("libusb-1.0", EntryPoint = "libusb_submit_transfer")]
	internal static extern int SubmitTransfer(IntPtr pTransfer);

	[DllImport("libusb-1.0", EntryPoint = "libusb_cancel_transfer")]
	internal static extern int CancelTransfer(IntPtr pTransfer);

	[DllImport("libusb-1.0", EntryPoint = "libusb_free_transfer")]
	internal static extern void FreeTransfer(IntPtr pTransfer);

	[DllImport("libusb-1.0", EntryPoint = "libusb_control_transfer")]
	public static extern int ControlTransfer([In] MonoUsbDeviceHandle deviceHandle, byte requestType, byte request, short value, short index, IntPtr pData, short dataLength, int timeout);

	[DllImport("libusb-1.0", EntryPoint = "libusb_bulk_transfer")]
	public static extern int BulkTransfer([In] MonoUsbDeviceHandle deviceHandle, byte endpoint, IntPtr pData, int length, out int actualLength, int timeout);

	[DllImport("libusb-1.0", EntryPoint = "libusb_interrupt_transfer")]
	public static extern int InterruptTransfer([In] MonoUsbDeviceHandle deviceHandle, byte endpoint, IntPtr pData, int length, out int actualLength, int timeout);

	[DllImport("libusb-1.0", EntryPoint = "libusb_try_lock_events")]
	public static extern int TryLockEvents([In] MonoUsbSessionHandle sessionHandle);

	[DllImport("libusb-1.0", EntryPoint = "libusb_lock_events")]
	public static extern void LockEvents([In] MonoUsbSessionHandle sessionHandle);

	[DllImport("libusb-1.0", EntryPoint = "libusb_unlock_events")]
	public static extern void UnlockEvents([In] MonoUsbSessionHandle sessionHandle);

	[DllImport("libusb-1.0", EntryPoint = "libusb_event_handling_ok")]
	public static extern int EventHandlingOk([In] MonoUsbSessionHandle sessionHandle);

	[DllImport("libusb-1.0", EntryPoint = "libusb_event_handler_active")]
	public static extern int EventHandlerActive([In] MonoUsbSessionHandle sessionHandle);

	[DllImport("libusb-1.0", EntryPoint = "libusb_lock_event_waiters")]
	public static extern void LockEventWaiters([In] MonoUsbSessionHandle sessionHandle);

	[DllImport("libusb-1.0", EntryPoint = "libusb_unlock_event_waiters")]
	public static extern void UnlockEventWaiters([In] MonoUsbSessionHandle sessionHandle);

	[DllImport("libusb-1.0", EntryPoint = "libusb_wait_for_event")]
	public static extern int WaitForEvent([In] MonoUsbSessionHandle sessionHandle, ref UnixNativeTimeval timeval);

	[DllImport("libusb-1.0", EntryPoint = "libusb_handle_events_timeout")]
	public static extern int HandleEventsTimeout([In] MonoUsbSessionHandle sessionHandle, ref UnixNativeTimeval tv);

	[DllImport("libusb-1.0", EntryPoint = "libusb_handle_events")]
	public static extern int HandleEvents([In] MonoUsbSessionHandle sessionHandle);

	[DllImport("libusb-1.0", EntryPoint = "libusb_handle_events")]
	private static extern int HandleEvents(IntPtr pSessionHandle);

	[DllImport("libusb-1.0", EntryPoint = "libusb_handle_events_locked")]
	public static extern int HandleEventsLocked([In] MonoUsbSessionHandle sessionHandle, ref UnixNativeTimeval tv);

	[DllImport("libusb-1.0", EntryPoint = "libusb_pollfds_handle_timeouts")]
	public static extern int PollfdsHandleTimeouts([In] MonoUsbSessionHandle sessionHandle);

	[DllImport("libusb-1.0", EntryPoint = "libusb_get_next_timeout")]
	public static extern int GetNextTimeout([In] MonoUsbSessionHandle sessionHandle, ref UnixNativeTimeval tv);

	[DllImport("libusb-1.0", EntryPoint = "libusb_get_pollfds")]
	private static extern IntPtr GetPollfdsInternal([In] MonoUsbSessionHandle sessionHandle);

	[DllImport("libusb-1.0", EntryPoint = "libusb_set_pollfd_notifiers")]
	public static extern void SetPollfdNotifiers([In] MonoUsbSessionHandle sessionHandle, PollfdAddedDelegate addedDelegate, PollfdRemovedDelegate removedDelegate, IntPtr pUserData);

	private static void DefaultAsyncCB(MonoUsbTransfer transfer)
	{
		(GCHandle.FromIntPtr(transfer.PtrUserData).Target as ManualResetEvent).Set();
	}

	public static string StrError(MonoUsbError errcode)
	{
		return errcode switch
		{
			MonoUsbError.Success => "Success", 
			MonoUsbError.ErrorIO => "Input/output error", 
			MonoUsbError.ErrorInvalidParam => "Invalid parameter", 
			MonoUsbError.ErrorAccess => "Access denied (insufficient permissions)", 
			MonoUsbError.ErrorNoDevice => "No such device (it may have been disconnected)", 
			MonoUsbError.ErrorBusy => "Resource busy", 
			MonoUsbError.ErrorTimeout => "Operation timed out", 
			MonoUsbError.ErrorOverflow => "Overflow", 
			MonoUsbError.ErrorPipe => "Pipe error or endpoint halted", 
			MonoUsbError.ErrorInterrupted => "System call interrupted (perhaps due to signal)", 
			MonoUsbError.ErrorNoMem => "Insufficient memory", 
			MonoUsbError.ErrorIOCancelled => "Transfer was canceled", 
			MonoUsbError.ErrorNotSupported => "Operation not supported or unimplemented on this platform", 
			_ => "Unknown error:" + errcode, 
		};
	}

	public static int GetDescriptor(MonoUsbDeviceHandle deviceHandle, byte descType, byte descIndex, short langId, IntPtr pData, int length)
	{
		return ControlTransfer(deviceHandle, 128, 6, (short)((descType << 8) | descIndex), langId, pData, (short)length, 1000);
	}

	public static int GetDescriptor(MonoUsbDeviceHandle deviceHandle, byte descType, byte descIndex, object data, int length)
	{
		PinnedHandle pinnedHandle = new PinnedHandle(data);
		return GetDescriptor(deviceHandle, descType, descIndex, pinnedHandle.Handle, length);
	}

	public static MonoUsbDeviceHandle OpenDeviceWithVidPid([In] MonoUsbSessionHandle sessionHandle, ushort vendorID, ushort productID)
	{
		IntPtr intPtr = OpenDeviceWithVidPidInternal(sessionHandle, vendorID, productID);
		if (intPtr == IntPtr.Zero)
		{
			return null;
		}
		return new MonoUsbDeviceHandle(intPtr);
	}

	public static MonoUsbProfileHandle GetDevice(MonoUsbDeviceHandle devicehandle)
	{
		return new MonoUsbProfileHandle(GetDeviceInternal(devicehandle));
	}

	public static int BulkTransfer([In] MonoUsbDeviceHandle deviceHandle, byte endpoint, object data, int length, out int actualLength, int timeout)
	{
		PinnedHandle pinnedHandle = new PinnedHandle(data);
		int result = BulkTransfer(deviceHandle, endpoint, pinnedHandle.Handle, length, out actualLength, timeout);
		pinnedHandle.Dispose();
		return result;
	}

	public static int InterruptTransfer([In] MonoUsbDeviceHandle deviceHandle, byte endpoint, object data, int length, out int actualLength, int timeout)
	{
		PinnedHandle pinnedHandle = new PinnedHandle(data);
		int result = InterruptTransfer(deviceHandle, endpoint, pinnedHandle.Handle, length, out actualLength, timeout);
		pinnedHandle.Dispose();
		return result;
	}

	public static int ControlTransferAsync([In] MonoUsbDeviceHandle deviceHandle, byte requestType, byte request, short value, short index, IntPtr pData, short dataLength, int timeout)
	{
		MonoUsbControlSetupHandle monoUsbControlSetupHandle = new MonoUsbControlSetupHandle(requestType, request, value, index, pData, dataLength);
		MonoUsbTransfer monoUsbTransfer = new MonoUsbTransfer(0);
		ManualResetEvent manualResetEvent = new ManualResetEvent(initialState: false);
		GCHandle value2 = GCHandle.Alloc(manualResetEvent);
		monoUsbTransfer.FillControl(deviceHandle, monoUsbControlSetupHandle, DefaultAsyncDelegate, GCHandle.ToIntPtr(value2), timeout);
		int num = (int)monoUsbTransfer.Submit();
		if (num < 0)
		{
			monoUsbTransfer.Free();
			value2.Free();
			return num;
		}
		IntPtr pSessionHandle = MonoUsbEventHandler.SessionHandle?.DangerousGetHandle() ?? IntPtr.Zero;
		if (MonoUsbEventHandler.IsStopped)
		{
			while (!manualResetEvent.WaitOne(0))
			{
				num = HandleEvents(pSessionHandle);
				if (num < 0 && num != -10)
				{
					monoUsbTransfer.Cancel();
					while (!manualResetEvent.WaitOne(0) && HandleEvents(pSessionHandle) >= 0)
					{
					}
					monoUsbTransfer.Free();
					value2.Free();
					return num;
				}
			}
		}
		else
		{
			manualResetEvent.WaitOne(-1);
		}
		if (monoUsbTransfer.Status == MonoUsbTansferStatus.TransferCompleted)
		{
			num = monoUsbTransfer.ActualLength;
			if (num > 0)
			{
				byte[] data = monoUsbControlSetupHandle.ControlSetup.GetData(num);
				Marshal.Copy(data, 0, pData, Math.Min(data.Length, dataLength));
			}
		}
		else
		{
			num = (int)MonoLibUsbErrorFromTransferStatus(monoUsbTransfer.Status);
		}
		monoUsbTransfer.Free();
		value2.Free();
		return num;
	}

	public static int ControlTransfer([In] MonoUsbDeviceHandle deviceHandle, byte requestType, byte request, short value, short index, object data, short dataLength, int timeout)
	{
		PinnedHandle pinnedHandle = new PinnedHandle(data);
		int result = ControlTransfer(deviceHandle, requestType, request, value, index, pinnedHandle.Handle, dataLength, timeout);
		pinnedHandle.Dispose();
		return result;
	}

	public static List<PollfdItem> GetPollfds(MonoUsbSessionHandle sessionHandle)
	{
		List<PollfdItem> list = new List<PollfdItem>();
		IntPtr pollfdsInternal = GetPollfdsInternal(sessionHandle);
		if (pollfdsInternal == IntPtr.Zero)
		{
			return null;
		}
		IntPtr intPtr = pollfdsInternal;
		IntPtr pPollfd;
		while (intPtr != IntPtr.Zero && (pPollfd = Marshal.ReadIntPtr(intPtr)) != IntPtr.Zero)
		{
			PollfdItem item = new PollfdItem(pPollfd);
			list.Add(item);
			intPtr = new IntPtr(intPtr.ToInt64() + IntPtr.Size);
		}
		Marshal.FreeHGlobal(pollfdsInternal);
		return list;
	}

	public static MonoUsbError MonoLibUsbErrorFromTransferStatus(MonoUsbTansferStatus status)
	{
		return status switch
		{
			MonoUsbTansferStatus.TransferCompleted => MonoUsbError.Success, 
			MonoUsbTansferStatus.TransferError => MonoUsbError.ErrorPipe, 
			MonoUsbTansferStatus.TransferTimedOut => MonoUsbError.ErrorTimeout, 
			MonoUsbTansferStatus.TransferCancelled => MonoUsbError.ErrorIOCancelled, 
			MonoUsbTansferStatus.TransferStall => MonoUsbError.ErrorPipe, 
			MonoUsbTansferStatus.TransferNoDevice => MonoUsbError.ErrorNoDevice, 
			MonoUsbTansferStatus.TransferOverflow => MonoUsbError.ErrorOverflow, 
			_ => MonoUsbError.ErrorOther, 
		};
	}

	internal static void InitAndStart()
	{
		if (MonoUsbEventHandler.IsStopped)
		{
			MonoUsbEventHandler.Init();
			MonoUsbEventHandler.Start();
		}
	}

	internal static void StopAndExit()
	{
		if (MonoUsbDevice.mMonoUSBProfileList != null)
		{
			MonoUsbDevice.mMonoUSBProfileList.Close();
		}
		MonoUsbDevice.mMonoUSBProfileList = null;
		MonoUsbEventHandler.Stop(bWait: true);
		MonoUsbEventHandler.Exit();
	}

	internal static ErrorCode ErrorCodeFromLibUsbError(int ret, out string description)
	{
		description = string.Empty;
		if (ret == 0)
		{
			return ErrorCode.None;
		}
		switch ((MonoUsbError)ret)
		{
		case MonoUsbError.Success:
			description += "Success";
			return ErrorCode.None;
		case MonoUsbError.ErrorIO:
			description += "Input/output error";
			return ErrorCode.IoSyncFailed;
		case MonoUsbError.ErrorInvalidParam:
			description += "Invalid parameter";
			return ErrorCode.InvalidParam;
		case MonoUsbError.ErrorAccess:
			description += "Access denied (insufficient permissions)";
			return ErrorCode.AccessDenied;
		case MonoUsbError.ErrorNoDevice:
			description += "No such device (it may have been disconnected)";
			return ErrorCode.DeviceNotFound;
		case MonoUsbError.ErrorBusy:
			description += "Resource busy";
			return ErrorCode.ResourceBusy;
		case MonoUsbError.ErrorTimeout:
			description += "Operation timed out";
			return ErrorCode.IoTimedOut;
		case MonoUsbError.ErrorOverflow:
			description += "Overflow";
			return ErrorCode.Overflow;
		case MonoUsbError.ErrorPipe:
			description += "Pipe error or endpoint halted";
			return ErrorCode.PipeError;
		case MonoUsbError.ErrorInterrupted:
			description += "System call interrupted (perhaps due to signal)";
			return ErrorCode.Interrupted;
		case MonoUsbError.ErrorNoMem:
			description += "Insufficient memory";
			return ErrorCode.InsufficientMemory;
		case MonoUsbError.ErrorIOCancelled:
			description += "Transfer was canceled";
			return ErrorCode.IoCancelled;
		case MonoUsbError.ErrorNotSupported:
			description += "Operation not supported or unimplemented on this platform";
			return ErrorCode.NotSupported;
		default:
			description = description + "Unknown error:" + ret;
			return ErrorCode.UnknownError;
		}
	}
}
