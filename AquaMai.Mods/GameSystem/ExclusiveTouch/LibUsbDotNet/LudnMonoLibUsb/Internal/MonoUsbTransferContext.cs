using System;
using System.Runtime.InteropServices;
using System.Threading;
using LibUsbDotNet.Main;
using MonoLibUsb;
using MonoLibUsb.Transfer;

namespace LibUsbDotNet.LudnMonoLibUsb.Internal;

internal class MonoUsbTransferContext : UsbTransfer, IDisposable
{
	private bool mOwnsTransfer;

	private static readonly MonoUsbTransferDelegate mMonoUsbTransferCallbackDelegate = TransferCallback;

	private GCHandle mCompleteEventHandle;

	private MonoUsbTransfer mTransfer;

	private bool mbDisposed2;

	public MonoUsbTransferContext(UsbEndpointBase endpointBase)
		: base(endpointBase)
	{
	}

	public new virtual void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	protected override void Dispose(bool disposing)
	{
		if (!mbDisposed2)
		{
			mbDisposed2 = true;
			freeTransfer();
			base.Dispose();
		}
	}

	~MonoUsbTransferContext()
	{
		Dispose(disposing: false);
	}

	private void allocTransfer(UsbEndpointBase endpointBase, bool ownsTransfer, int isoPacketSize, int count)
	{
		int num = 0;
		EndpointType endpointType = endpointBase.Type;
		if (UsbDevice.IsLinux)
		{
			if (isoPacketSize > 0)
			{
				num = count / isoPacketSize;
			}
		}
		else if (endpointType == EndpointType.Isochronous)
		{
			endpointType = EndpointType.Bulk;
		}
		freeTransfer();
		mTransfer = MonoUsbTransfer.Alloc(num);
		mOwnsTransfer = ownsTransfer;
		mTransfer.Type = endpointType;
		mTransfer.Endpoint = endpointBase.EpNum;
		mTransfer.NumIsoPackets = num;
		if (!mCompleteEventHandle.IsAllocated)
		{
			mCompleteEventHandle = GCHandle.Alloc(mTransferCompleteEvent);
		}
		mTransfer.PtrUserData = GCHandle.ToIntPtr(mCompleteEventHandle);
		if (num > 0)
		{
			mTransfer.SetIsoPacketLengths(isoPacketSize);
		}
	}

	private void freeTransfer()
	{
		if (!mTransfer.IsInvalid && mOwnsTransfer)
		{
			mTransferCancelEvent.Set();
			mTransferCompleteEvent.WaitOne(200);
			mTransfer.Free();
			if (mCompleteEventHandle.IsAllocated)
			{
				mCompleteEventHandle.Free();
			}
		}
	}

	public override void Fill(IntPtr buffer, int offset, int count, int timeout)
	{
		allocTransfer(base.EndpointBase, ownsTransfer: true, 0, count);
		base.Fill(buffer, offset, count, timeout);
		mTransfer.Timeout = timeout;
		mTransfer.PtrDeviceHandle = base.EndpointBase.Handle.DangerousGetHandle();
		mTransfer.PtrCallbackFn = Marshal.GetFunctionPointerForDelegate(mMonoUsbTransferCallbackDelegate);
		mTransfer.ActualLength = 0;
		mTransfer.Status = MonoUsbTansferStatus.TransferCompleted;
		mTransfer.Flags = MonoUsbTransferFlags.None;
	}

	public override void Fill(IntPtr buffer, int offset, int count, int timeout, int isoPacketSize)
	{
		allocTransfer(base.EndpointBase, ownsTransfer: true, isoPacketSize, count);
		base.Fill(buffer, offset, count, timeout, isoPacketSize);
		mTransfer.Timeout = timeout;
		mTransfer.PtrDeviceHandle = base.EndpointBase.Handle.DangerousGetHandle();
		mTransfer.PtrCallbackFn = Marshal.GetFunctionPointerForDelegate(mMonoUsbTransferCallbackDelegate);
		mTransfer.ActualLength = 0;
		mTransfer.Status = MonoUsbTansferStatus.TransferCompleted;
		mTransfer.Flags = MonoUsbTransferFlags.None;
	}

	public override ErrorCode Submit()
	{
		if (mTransferCancelEvent.WaitOne(0))
		{
			return ErrorCode.IoCancelled;
		}
		if (!mTransferCompleteEvent.WaitOne(0))
		{
			return ErrorCode.ResourceBusy;
		}
		mTransfer.PtrBuffer = base.NextBufPtr;
		mTransfer.Length = base.RequestCount;
		mTransferCompleteEvent.Reset();
		int num = (int)mTransfer.Submit();
		if (num < 0)
		{
			mTransferCompleteEvent.Set();
			return UsbError.Error(ErrorCode.MonoApiError, num, "SubmitTransfer", base.EndpointBase).ErrorCode;
		}
		return ErrorCode.None;
	}

	public override ErrorCode Wait(out int transferredCount, bool cancel)
	{
		transferredCount = 0;
		int ret = 0;
		switch (WaitHandle.WaitAny(new WaitHandle[2] { mTransferCompleteEvent, mTransferCancelEvent }, -1))
		{
		case 0:
		{
			if (mTransfer.Status == MonoUsbTansferStatus.TransferCompleted)
			{
				transferredCount = mTransfer.ActualLength;
				return ErrorCode.None;
			}
			MonoUsbError ret2 = MonoUsbApi.MonoLibUsbErrorFromTransferStatus(mTransfer.Status);
			string description;
			ErrorCode result = MonoUsbApi.ErrorCodeFromLibUsbError((int)ret2, out description);
			UsbError.Error(ErrorCode.MonoApiError, (int)ret2, "Wait:" + description, base.EndpointBase);
			return result;
		}
		case 1:
		{
			ret = (int)mTransfer.Cancel();
			bool flag = mTransferCompleteEvent.WaitOne(100);
			mTransferCompleteEvent.Set();
			if (ret != 0 || !flag)
			{
				int num2 = ((ret == 0) ? (-16373) : (-16353));
				UsbError.Error((ErrorCode)num2, ret, $"Wait:Unable to cancel transfer or the transfer did not return after it was cancelled. Cancelled:{(MonoUsbError)ret} TransferCompleted:{flag}", base.EndpointBase);
				return (ErrorCode)num2;
			}
			return ErrorCode.IoCancelled;
		}
		default:
		{
			mTransfer.Cancel();
			int num = (((base.EndpointBase.mEpNum & 0x80) > 0) ? (-16375) : (-16376));
			mTransferCompleteEvent.Set();
			UsbError.Error((ErrorCode)num, ret, $"Wait:Critical timeout failure! The transfer callback function was not called within the allotted time.", base.EndpointBase);
			return (ErrorCode)num;
		}
		}
	}

	private static void TransferCallback(MonoUsbTransfer pTransfer)
	{
		(GCHandle.FromIntPtr(pTransfer.PtrUserData).Target as ManualResetEvent).Set();
	}
}
