using System;
using LibUsbDotNet.LudnMonoLibUsb.Internal;
using LibUsbDotNet.Main;
using MonoLibUsb;

namespace LibUsbDotNet.LudnMonoLibUsb;

public class MonoUsbEndpointWriter : UsbEndpointWriter
{
	private MonoUsbTransferContext mMonoTransferContext;

	internal MonoUsbEndpointWriter(UsbDevice usbDevice, byte alternateInterfaceID, WriteEndpointID writeEndpointID, EndpointType endpointType)
		: base(usbDevice, alternateInterfaceID, writeEndpointID, endpointType)
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
		return true;
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
