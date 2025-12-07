namespace MonoLibUsb.Transfer;

public enum MonoUsbTransferFlags : byte
{
	None = 0,
	TransferShortNotOk = 1,
	TransferFreeBuffer = 2,
	TransferFreeTransfer = 4
}
