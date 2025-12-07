using System.Runtime.InteropServices;
using MonoLibUsb.Transfer;

namespace MonoLibUsb;

[UnmanagedFunctionPointer((CallingConvention)0)]
public delegate void MonoUsbTransferDelegate(MonoUsbTransfer transfer);
