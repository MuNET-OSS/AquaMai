using System;
using System.Runtime.InteropServices;

namespace MonoLibUsb;

[UnmanagedFunctionPointer((CallingConvention)0)]
public delegate void PollfdRemovedDelegate(int fd, IntPtr user_data);
