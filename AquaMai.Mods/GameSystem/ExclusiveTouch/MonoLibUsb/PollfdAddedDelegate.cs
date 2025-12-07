using System;
using System.Runtime.InteropServices;

namespace MonoLibUsb;

[UnmanagedFunctionPointer((CallingConvention)0)]
public delegate void PollfdAddedDelegate(int fd, short events, IntPtr user_data);
