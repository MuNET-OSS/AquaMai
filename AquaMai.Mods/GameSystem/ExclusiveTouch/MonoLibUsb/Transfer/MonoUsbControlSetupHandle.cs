using System;
using System.Runtime.InteropServices;
using LibUsbDotNet.Main;

namespace MonoLibUsb.Transfer;

public class MonoUsbControlSetupHandle : SafeContextHandle
{
	private MonoUsbControlSetup mSetupPacket;

	public MonoUsbControlSetup ControlSetup => mSetupPacket;

	public MonoUsbControlSetupHandle(byte requestType, byte request, short value, short index, object data, int length)
		: this(requestType, request, value, index, (short)(ushort)length)
	{
		if (data != null)
		{
			mSetupPacket.SetData(data, 0, length);
		}
	}

	public MonoUsbControlSetupHandle(byte requestType, byte request, short value, short index, short length)
		: base(IntPtr.Zero, ownsHandle: true)
	{
		ushort num = (ushort)length;
		int num2 = ((num <= 0) ? MonoUsbControlSetup.SETUP_PACKET_SIZE : (MonoUsbControlSetup.SETUP_PACKET_SIZE + num + (IntPtr.Size - num % IntPtr.Size)));
		IntPtr intPtr = Marshal.AllocHGlobal(num2);
		if (intPtr == IntPtr.Zero)
		{
			throw new OutOfMemoryException($"Marshal.AllocHGlobal failed allocating {num2} bytes");
		}
		SetHandle(intPtr);
		mSetupPacket = new MonoUsbControlSetup(intPtr);
		mSetupPacket.RequestType = requestType;
		mSetupPacket.Request = request;
		mSetupPacket.Value = value;
		mSetupPacket.Index = index;
		mSetupPacket.Length = (short)num;
	}

	protected override bool ReleaseHandle()
	{
		if (!IsInvalid)
		{
			Marshal.FreeHGlobal(handle);
			SetHandleAsInvalid();
		}
		return true;
	}
}
