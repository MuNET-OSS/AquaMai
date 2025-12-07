using System;
using LibUsbDotNet.LudnMonoLibUsb.Internal;
using LibUsbDotNet.Main;
using MonoLibUsb;

namespace LibUsbDotNet.LudnMonoLibUsb;

public class MonoUsbEndpointReader : UsbEndpointReader
{
	private MonoUsbTransferContext mMonoTransferContext;

	internal MonoUsbEndpointReader(UsbDevice usbDevice, int readBufferSize, byte alternateInterfaceID, ReadEndpointID readEndpointID, EndpointType endpointType)
		: base(usbDevice, readBufferSize, alternateInterfaceID, readEndpointID, endpointType)
	{
	}

	public override void Dispose()
	{
		base.Dispose();
		if (mMonoTransferContext != null)
		{
			mMonoTransferContext.Dispose();
			mMonoTransferContext = null;
		}
	}

	public override bool Flush()
	{
		if (base.IsDisposed)
		{
			throw new ObjectDisposedException(GetType().Name);
		}
		return ReadFlush() == ErrorCode.None;
	}

	public override bool Reset()
	{
		if (base.IsDisposed)
		{
			throw new ObjectDisposedException(GetType().Name);
		}
		Abort();
		int num = MonoUsbApi.ClearHalt((MonoUsbDeviceHandle)base.Device.Handle, base.EpNum);
		if (num < 0)
		{
			UsbError.Error(ErrorCode.MonoApiError, num, "Endpoint Reset Failed", this);
			return false;
		}
		return true;
	}

	internal override UsbTransfer CreateTransferContext()
	{
		return new MonoUsbTransferContext(this);
	}
}
