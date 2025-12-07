using System;
using System.Runtime.InteropServices;
using LibUsbDotNet.Main;
using MonoLibUsb.Transfer.Internal;

namespace MonoLibUsb.Transfer;

public struct MonoUsbTransfer
{
	private static readonly int OfsActualLength = Marshal.OffsetOf(typeof(libusb_transfer), "actual_length").ToInt32();

	private static readonly int OfsEndpoint = Marshal.OffsetOf(typeof(libusb_transfer), "endpoint").ToInt32();

	private static readonly int OfsFlags = Marshal.OffsetOf(typeof(libusb_transfer), "flags").ToInt32();

	private static readonly int OfsLength = Marshal.OffsetOf(typeof(libusb_transfer), "length").ToInt32();

	private static readonly int OfsPtrBuffer = Marshal.OffsetOf(typeof(libusb_transfer), "pBuffer").ToInt32();

	private static readonly int OfsPtrCallbackFn = Marshal.OffsetOf(typeof(libusb_transfer), "pCallbackFn").ToInt32();

	private static readonly int OfsPtrDeviceHandle = Marshal.OffsetOf(typeof(libusb_transfer), "deviceHandle").ToInt32();

	private static readonly int OfsPtrUserData = Marshal.OffsetOf(typeof(libusb_transfer), "pUserData").ToInt32();

	private static readonly int OfsStatus = Marshal.OffsetOf(typeof(libusb_transfer), "status").ToInt32();

	private static readonly int OfsTimeout = Marshal.OffsetOf(typeof(libusb_transfer), "timeout").ToInt32();

	private static readonly int OfsType = Marshal.OffsetOf(typeof(libusb_transfer), "type").ToInt32();

	private static readonly int OfsNumIsoPackets = Marshal.OffsetOf(typeof(libusb_transfer), "num_iso_packets").ToInt32();

	private static readonly int OfsIsoPackets = Marshal.OffsetOf(typeof(libusb_transfer), "iso_packets").ToInt32();

	private IntPtr handle;

	public IntPtr PtrBuffer
	{
		get
		{
			return Marshal.ReadIntPtr(handle, OfsPtrBuffer);
		}
		set
		{
			Marshal.WriteIntPtr(handle, OfsPtrBuffer, value);
		}
	}

	public IntPtr PtrUserData
	{
		get
		{
			return Marshal.ReadIntPtr(handle, OfsPtrUserData);
		}
		set
		{
			Marshal.WriteIntPtr(handle, OfsPtrUserData, value);
		}
	}

	public IntPtr PtrCallbackFn
	{
		get
		{
			return Marshal.ReadIntPtr(handle, OfsPtrCallbackFn);
		}
		set
		{
			Marshal.WriteIntPtr(handle, OfsPtrCallbackFn, value);
		}
	}

	public int ActualLength
	{
		get
		{
			return Marshal.ReadInt32(handle, OfsActualLength);
		}
		set
		{
			Marshal.WriteInt32(handle, OfsActualLength, value);
		}
	}

	public int Length
	{
		get
		{
			return Marshal.ReadInt32(handle, OfsLength);
		}
		set
		{
			Marshal.WriteInt32(handle, OfsLength, value);
		}
	}

	public MonoUsbTansferStatus Status
	{
		get
		{
			return (MonoUsbTansferStatus)Marshal.ReadInt32(handle, OfsStatus);
		}
		set
		{
			Marshal.WriteInt32(handle, OfsStatus, (int)value);
		}
	}

	public int Timeout
	{
		get
		{
			return Marshal.ReadInt32(handle, OfsTimeout);
		}
		set
		{
			Marshal.WriteInt32(handle, OfsTimeout, value);
		}
	}

	public EndpointType Type
	{
		get
		{
			return (EndpointType)Marshal.ReadByte(handle, OfsType);
		}
		set
		{
			Marshal.WriteByte(handle, OfsType, (byte)value);
		}
	}

	public byte Endpoint
	{
		get
		{
			return Marshal.ReadByte(handle, OfsEndpoint);
		}
		set
		{
			Marshal.WriteByte(handle, OfsEndpoint, value);
		}
	}

	public MonoUsbTransferFlags Flags
	{
		get
		{
			return (MonoUsbTransferFlags)Marshal.ReadByte(handle, OfsFlags);
		}
		set
		{
			Marshal.WriteByte(handle, OfsFlags, (byte)value);
		}
	}

	public IntPtr PtrDeviceHandle
	{
		get
		{
			return Marshal.ReadIntPtr(handle, OfsPtrDeviceHandle);
		}
		set
		{
			Marshal.WriteIntPtr(handle, OfsPtrDeviceHandle, value);
		}
	}

	public int NumIsoPackets
	{
		get
		{
			return Marshal.ReadInt32(handle, OfsNumIsoPackets);
		}
		set
		{
			Marshal.WriteInt32(handle, OfsNumIsoPackets, value);
		}
	}

	public bool IsInvalid => handle == IntPtr.Zero;

	public MonoUsbTransfer(int numIsoPackets)
	{
		handle = MonoUsbApi.AllocTransfer(numIsoPackets);
	}

	internal MonoUsbTransfer(IntPtr pTransfer)
	{
		handle = pTransfer;
	}

	public void Free()
	{
		if (handle != IntPtr.Zero)
		{
			MonoUsbApi.FreeTransfer(handle);
			handle = IntPtr.Zero;
		}
	}

	public string UniqueName()
	{
		return $"_-EP[{handle}]EP-_";
	}

	public MonoUsbIsoPacket IsoPacket(int packetNumber)
	{
		if (packetNumber > NumIsoPackets)
		{
			throw new ArgumentOutOfRangeException("packetNumber");
		}
		return new MonoUsbIsoPacket(new IntPtr(handle.ToInt64() + OfsIsoPackets + packetNumber * Marshal.SizeOf(typeof(libusb_iso_packet_descriptor))));
	}

	public MonoUsbError Cancel()
	{
		if (IsInvalid)
		{
			return MonoUsbError.ErrorNoMem;
		}
		return (MonoUsbError)MonoUsbApi.CancelTransfer(handle);
	}

	public void FillBulk(MonoUsbDeviceHandle devHandle, byte endpoint, IntPtr buffer, int length, Delegate callback, IntPtr userData, int timeout)
	{
		PtrDeviceHandle = devHandle.DangerousGetHandle();
		Endpoint = endpoint;
		PtrBuffer = buffer;
		Length = length;
		PtrCallbackFn = Marshal.GetFunctionPointerForDelegate(callback);
		PtrUserData = userData;
		Timeout = timeout;
		Type = EndpointType.Bulk;
		Flags = MonoUsbTransferFlags.None;
		NumIsoPackets = 0;
		ActualLength = 0;
	}

	public void FillInterrupt(MonoUsbDeviceHandle devHandle, byte endpoint, IntPtr buffer, int length, Delegate callback, IntPtr userData, int timeout)
	{
		PtrDeviceHandle = devHandle.DangerousGetHandle();
		Endpoint = endpoint;
		PtrBuffer = buffer;
		Length = length;
		PtrCallbackFn = Marshal.GetFunctionPointerForDelegate(callback);
		PtrUserData = userData;
		Timeout = timeout;
		Type = EndpointType.Interrupt;
		Flags = MonoUsbTransferFlags.None;
	}

	public void FillIsochronous(MonoUsbDeviceHandle devHandle, byte endpoint, IntPtr buffer, int length, int numIsoPackets, Delegate callback, IntPtr userData, int timeout)
	{
		PtrDeviceHandle = devHandle.DangerousGetHandle();
		Endpoint = endpoint;
		PtrBuffer = buffer;
		Length = length;
		PtrCallbackFn = Marshal.GetFunctionPointerForDelegate(callback);
		PtrUserData = userData;
		Timeout = timeout;
		Type = EndpointType.Isochronous;
		Flags = MonoUsbTransferFlags.None;
		NumIsoPackets = numIsoPackets;
	}

	public IntPtr GetIsoPacketBuffer(int packet)
	{
		if (packet >= NumIsoPackets)
		{
			throw new ArgumentOutOfRangeException("packet", "GetIsoPacketBuffer: packet must be < NumIsoPackets");
		}
		long num = PtrBuffer.ToInt64();
		for (int i = 0; i < packet; i++)
		{
			num += IsoPacket(i).Length;
		}
		return new IntPtr(num);
	}

	public IntPtr GetIsoPacketBufferSimple(int packet)
	{
		if (packet >= NumIsoPackets)
		{
			throw new ArgumentOutOfRangeException("packet", "GetIsoPacketBufferSimple: packet must be < NumIsoPackets");
		}
		return new IntPtr(PtrBuffer.ToInt64() + IsoPacket(0).Length * packet);
	}

	public void SetIsoPacketLengths(int length)
	{
		int numIsoPackets = NumIsoPackets;
		for (int i = 0; i < numIsoPackets; i++)
		{
			IsoPacket(i).Length = length;
		}
	}

	public MonoUsbError Submit()
	{
		if (IsInvalid)
		{
			return MonoUsbError.ErrorNoMem;
		}
		return (MonoUsbError)MonoUsbApi.SubmitTransfer(handle);
	}

	public static MonoUsbTransfer Alloc(int numIsoPackets)
	{
		IntPtr intPtr = MonoUsbApi.AllocTransfer(numIsoPackets);
		if (intPtr == IntPtr.Zero)
		{
			throw new OutOfMemoryException("AllocTransfer");
		}
		return new MonoUsbTransfer(intPtr);
	}

	public void FillControl(MonoUsbDeviceHandle devHandle, MonoUsbControlSetupHandle controlSetupHandle, Delegate callback, IntPtr userData, int timeout)
	{
		PtrDeviceHandle = devHandle.DangerousGetHandle();
		Endpoint = 0;
		PtrCallbackFn = Marshal.GetFunctionPointerForDelegate(callback);
		PtrUserData = userData;
		Timeout = timeout;
		Type = EndpointType.Control;
		Flags = MonoUsbTransferFlags.None;
		IntPtr pControlSetup = (PtrBuffer = controlSetupHandle.DangerousGetHandle());
		MonoUsbControlSetup monoUsbControlSetup = new MonoUsbControlSetup(pControlSetup);
		Length = MonoUsbControlSetup.SETUP_PACKET_SIZE + monoUsbControlSetup.Length;
	}
}
