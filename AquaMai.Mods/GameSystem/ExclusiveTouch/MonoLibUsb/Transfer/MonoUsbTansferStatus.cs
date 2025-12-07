namespace MonoLibUsb.Transfer;

public enum MonoUsbTansferStatus
{
	TransferCompleted,
	TransferError,
	TransferTimedOut,
	TransferCancelled,
	TransferStall,
	TransferNoDevice,
	TransferOverflow
}
