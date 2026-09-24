using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using MelonLoader;
using Microsoft.Win32.SafeHandles;

namespace AquaMai.Mods.GameSystem.Lib;

/// <summary>
/// 极简 WinUSB 封装（P/Invoke），用于访问按 WCID 绑定到 winusb.sys 的 vendor 接口。
///
/// 设计目标：
/// - 不依赖 LibUsbDotNet（那套 110 文件/8400 行里有三套后端，这里只需要 WinUSB 一条路）；
/// - 读用 overlapped I/O + 超时，便于"始终挂着一个 pending URB"的读循环；
/// - 设备定位先按 USB 硬件 ID 和接口名称找到具体 devnode，再用该 devnode 的
///   Instance ID 获取 WinUSB 接口路径，同时兼容设备注册表中存在其他接口 GUID 的情况。
/// </summary>
public static class WinUsbIo
{
    private const int ERROR_SUCCESS = 0;
    private const int ERROR_INSUFFICIENT_BUFFER = 122;
    private const int ERROR_MORE_DATA = 234;
    private const int CR_SUCCESS = 0;
    private const uint CM_GET_DEVICE_INTERFACE_LIST_PRESENT = 0x00000000;
    private const int ERROR_IO_PENDING = 997;
    private const uint DIGCF_ALLCLASSES = 0x04;
    private const uint DIGCF_PRESENT = 0x02;
    private const uint DICS_FLAG_GLOBAL = 0x00000001;
    private const uint DIREG_DEV = 0x00000001;
    private const uint KEY_READ = 0x00020019;
    private const uint SPDRP_HARDWAREID = 0x00000001;
    private const uint SPDRP_SERVICE = 0x00000004;
    private const uint SPDRP_FRIENDLYNAME = 0x0000000C;
    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x01;
    private const uint FILE_SHARE_WRITE = 0x02;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_OVERLAPPED = 0x40000000;

    #region SetupAPI / kernel32 / winusb

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public int cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct USB_INTERFACE_DESCRIPTOR
    {
        public byte bLength;
        public byte bDescriptorType;
        public byte bInterfaceNumber;
        public byte bAlternateSetting;
        public byte bNumEndpoints;
        public byte bInterfaceClass;
        public byte bInterfaceSubClass;
        public byte bInterfaceProtocol;
        public byte iInterface;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINUSB_PIPE_INFORMATION
    {
        public int PipeType; // USBD_PIPE_TYPE
        public byte PipeId;
        public ushort MaximumPacketSize;
        public byte Interval;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct OVERLAPPED
    {
        public IntPtr Internal;
        public IntPtr InternalHigh;
        public int Offset;
        public int OffsetHigh;
        public IntPtr hEvent;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern IntPtr SetupDiGetClassDevsW(IntPtr classGuid, string enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, int memberIndex,
        ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern bool SetupDiGetDeviceInstanceIdW(IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData, StringBuilder deviceInstanceId, int deviceInstanceIdSize,
        out int requiredSize);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int CM_Get_Device_Interface_List_SizeW(out int length, ref Guid interfaceClassGuid,
        string deviceInstanceId, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int CM_Get_Device_Interface_ListW(ref Guid interfaceClassGuid, string deviceInstanceId,
        [Out] char[] buffer, int bufferLength, uint flags);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern bool SetupDiGetDeviceRegistryPropertyW(IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData, uint property, out uint propertyRegDataType,
        byte[] propertyBuffer, int propertyBufferSize, out int requiredSize);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern bool SetupDiGetCustomDevicePropertyW(IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData, string propertyName, uint flags, out uint propertyRegDataType,
        byte[] propertyBuffer, int propertyBufferSize, out int requiredSize);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern IntPtr SetupDiOpenDevRegKey(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData,
        uint scope, uint hwProfile, uint keyType, uint samDesired);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern int RegQueryValueExW(IntPtr hKey, string valueName, IntPtr reserved,
        out uint type, byte[] data, ref uint dataSize);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern int RegCloseKey(IntPtr hKey);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern SafeFileHandle CreateFileW(string fileName, uint desiredAccess, uint shareMode,
        IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetOverlappedResult(SafeFileHandle file, IntPtr overlapped,
        out uint bytesTransferred, bool wait);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CancelIoEx(SafeFileHandle file, IntPtr overlapped);

    [DllImport("winusb.dll", SetLastError = true)]
    private static extern bool WinUsb_Initialize(SafeFileHandle deviceHandle, out IntPtr interfaceHandle);

    [DllImport("winusb.dll", SetLastError = true)]
    private static extern bool WinUsb_Free(IntPtr interfaceHandle);

    [DllImport("winusb.dll", SetLastError = true)]
    private static extern bool WinUsb_QueryInterfaceSettings(IntPtr interfaceHandle,
        byte alternateInterfaceNumber, out USB_INTERFACE_DESCRIPTOR descriptor);

    [DllImport("winusb.dll", SetLastError = true)]
    private static extern bool WinUsb_QueryPipe(IntPtr interfaceHandle, byte alternateInterfaceNumber,
        byte pipeIndex, out WINUSB_PIPE_INFORMATION pipeInformation);

    [DllImport("winusb.dll", SetLastError = true)]
    private static extern bool WinUsb_ReadPipe(IntPtr interfaceHandle, byte pipeId, IntPtr buffer,
        uint bufferLength, out uint lengthTransferred, IntPtr overlapped);

    [DllImport("winusb.dll", SetLastError = true)]
    private static extern bool WinUsb_WritePipe(IntPtr interfaceHandle, byte pipeId, IntPtr buffer,
        uint bufferLength, out uint lengthTransferred, IntPtr overlapped);

    [DllImport("winusb.dll", SetLastError = true)]
    private static extern bool WinUsb_AbortPipe(IntPtr interfaceHandle, byte pipeId);

    #endregion

    /// <summary>设备接口路径，形如 \\?\usb#vid_2e3c&amp;pid_5751#...#{guid}。</summary>
    public sealed class DevicePath
    {
        public string Path { get; init; }
        public ushort Vid { get; init; }
        public ushort Pid { get; init; }
    }

    /// <summary>
    /// 按 USB 硬件 ID 找 WinUSB 设备，再枚举固件约定及设备实际注册的接口 GUID。
    ///
    /// 接口 GUID 用于取得 WinUSB 设备路径，硬件 ID 和 Friendly Name 用于确认目标接口。
    /// </summary>
    public static List<DevicePath> EnumerateWinUsbInterfaces(
        ushort vid, ushort pid, byte interfaceId, string expectedInterfaceName, Guid expectedInterfaceGuid)
    {
        var result = new List<DevicePath>();
        var set = SetupDiGetClassDevsW(IntPtr.Zero, "USB", IntPtr.Zero, DIGCF_PRESENT | DIGCF_ALLCLASSES);
        if (set == IntPtr.Zero || set == new IntPtr(-1))
        {
            MelonLogger.Msg($"[WinUsbIo] SetupDiGetClassDevs 失败: {Marshal.GetLastWin32Error()}");
            return result;
        }

        var deviceCount = 0;
        var matchedCount = 0;
        try
        {
            var info = new SP_DEVINFO_DATA();
            info.cbSize = Marshal.SizeOf(typeof(SP_DEVINFO_DATA));

            for (var index = 0; SetupDiEnumDeviceInfo(set, index, ref info); index++)
            {
                deviceCount++;

                var hardwareIds = ReadStringArrayProperty(set, ref info, SPDRP_HARDWAREID);
                var service = ReadStringProperty(set, ref info, SPDRP_SERVICE);
                var deviceName = ReadStringProperty(set, ref info, SPDRP_FRIENDLYNAME);

                if (!MatchesHardwareId(hardwareIds, vid, pid, interfaceId)) continue;
                if (!string.Equals(service, "WINUSB", StringComparison.OrdinalIgnoreCase)) continue;
                if (deviceName?.StartsWith(expectedInterfaceName, StringComparison.OrdinalIgnoreCase) != true) continue;
                var instanceId = ReadDeviceInstanceId(set, ref info);
                if (string.IsNullOrEmpty(instanceId))
                {
                    MelonLogger.Msg($"[WinUsbIo] 设备 {deviceName ?? hardwareIds[0]} 没有 Instance ID");
                    continue;
                }

                matchedCount++;
                var guids = ReadDeviceInterfaceGuids(set, ref info);

                if (!guids.Contains(expectedInterfaceGuid)) guids.Add(expectedInterfaceGuid);

                foreach (var guid in guids)
                {
                    var paths = EnumerateInterfaces(guid, instanceId);
                    foreach (var path in paths)
                    {
                        if (path.Vid != vid || path.Pid != pid) continue;
                        if (result.Exists(it => it.Path == path.Path)) continue;

                        result.Add(path);
                    }
                }
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(set);
        }

        return result;
    }

    private static List<DevicePath> EnumerateInterfaces(Guid interfaceGuid, string instanceId)
    {
        var result = new List<DevicePath>();
        var guid = interfaceGuid;
        if (CM_Get_Device_Interface_List_SizeW(out var length, ref guid, instanceId,
                CM_GET_DEVICE_INTERFACE_LIST_PRESENT) != CR_SUCCESS || length <= 0)
        {
            MelonLogger.Msg($"[WinUsbIo] 获取接口路径长度失败 guid={guid}, instance={instanceId}");
            return result;
        }

        var buffer = new char[length];
        var status = CM_Get_Device_Interface_ListW(ref guid, instanceId, buffer, buffer.Length,
            CM_GET_DEVICE_INTERFACE_LIST_PRESENT);
        if (status != CR_SUCCESS)
        {
            MelonLogger.Msg($"[WinUsbIo] 获取接口路径失败 guid={guid}, instance={instanceId}, status={status}");
            return result;
        }

        for (var start = 0; start < buffer.Length;)
        {
            var end = Array.IndexOf(buffer, '\0', start);
            if (end < 0 || end == start) break;

            var path = new string(buffer, start, end - start);
            if (TryParseVidPid(path, out var pathVid, out var pathPid))
                result.Add(new DevicePath { Path = path, Vid = pathVid, Pid = pathPid });
            start = end + 1;
        }

        return result;
    }

    private static string ReadDeviceInstanceId(IntPtr set, ref SP_DEVINFO_DATA info)
    {
        var instanceId = new StringBuilder(512);
        if (!SetupDiGetDeviceInstanceIdW(set, ref info, instanceId, instanceId.Capacity, out _))
        {
            return null;
        }

        return instanceId.ToString();
    }

    private static bool ContainsVid(string[] hardwareIds, ushort vid)
    {
        if (hardwareIds == null) return false;

        var vidPart = $"VID_{vid:X4}";
        foreach (var hardwareId in hardwareIds)
            if (hardwareId.IndexOf(vidPart, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

        return false;
    }

    private static bool MatchesHardwareId(string[] hardwareIds, ushort vid, ushort pid, byte interfaceId)
    {
        if (hardwareIds == null) return false;

        var prefix = $"USB\\VID_{vid:X4}&PID_{pid:X4}";
        var interfacePart = $"&MI_{interfaceId:X2}";
        foreach (var hardwareId in hardwareIds)
        {
            if (!hardwareId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (hardwareId.IndexOf(interfacePart, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }

        return false;
    }

    private static List<Guid> ReadDeviceInterfaceGuids(IntPtr set, ref SP_DEVINFO_DATA info)
    {
        foreach (var propertyName in new[] { "DeviceInterfaceGUIDs", "DeviceInterfaceGUID" })
        {
            var bytes = ReadCustomDeviceProperty(set, ref info, propertyName);
            if (bytes == null) bytes = ReadDeviceRegistryValue(set, ref info, propertyName);
            if (bytes == null) continue;

            var guids = ParseGuids(Encoding.Unicode.GetString(bytes));
            if (guids.Count > 0) return guids;
        }

        return new List<Guid>();
    }

    private static List<Guid> ParseGuids(string value)
    {
        var result = new List<Guid>();
        for (var start = value.IndexOf('{'); start >= 0; start = value.IndexOf('{', start + 1))
        {
            var end = value.IndexOf('}', start + 1);
            if (end < 0) break;

            if (Guid.TryParse(value.Substring(start, end - start + 1), out var guid))
                result.Add(guid);
            start = end;
        }

        if (result.Count > 0) return result;

        // 某些驱动会写成不带花括号的 GUID 串。
        foreach (var part in value.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries))
            if (Guid.TryParse(part.Trim(), out var guid))
                result.Add(guid);

        return result;
    }

    private static string ReadStringProperty(IntPtr set, ref SP_DEVINFO_DATA info, uint property)
    {
        var bytes = ReadDeviceProperty(set, ref info, property);
        return bytes == null ? null : Encoding.Unicode.GetString(bytes).TrimEnd('\0');
    }

    private static string[] ReadStringArrayProperty(IntPtr set, ref SP_DEVINFO_DATA info, uint property)
    {
        var value = ReadStringProperty(set, ref info, property);
        return value?.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static byte[] ReadDeviceProperty(IntPtr set, ref SP_DEVINFO_DATA info, uint property)
    {
        var buffer = new byte[1024];
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (SetupDiGetDeviceRegistryPropertyW(set, ref info, property, out _, buffer, buffer.Length,
                    out var requiredSize))
            {
                var result = new byte[requiredSize];
                Array.Copy(buffer, result, requiredSize);
                return result;
            }

            var error = Marshal.GetLastWin32Error();
            if (error != ERROR_INSUFFICIENT_BUFFER || requiredSize <= buffer.Length) return null;
            buffer = new byte[requiredSize];
        }

        return null;
    }

    private static byte[] ReadCustomDeviceProperty(IntPtr set, ref SP_DEVINFO_DATA info, string propertyName)
    {
        var buffer = new byte[1024];
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (SetupDiGetCustomDevicePropertyW(set, ref info, propertyName, 0, out _, buffer, buffer.Length,
                    out var requiredSize))
            {
                var result = new byte[requiredSize];
                Array.Copy(buffer, result, requiredSize);
                return result;
            }

            var error = Marshal.GetLastWin32Error();
            if (error != ERROR_INSUFFICIENT_BUFFER || requiredSize <= buffer.Length) return null;
            buffer = new byte[requiredSize];
        }

        return null;
    }

    private static byte[] ReadDeviceRegistryValue(IntPtr set, ref SP_DEVINFO_DATA info, string valueName)
    {
        var key = SetupDiOpenDevRegKey(set, ref info, DICS_FLAG_GLOBAL, 0, DIREG_DEV, KEY_READ);
        if (key == IntPtr.Zero || key == new IntPtr(-1)) return null;

        try
        {
            var buffer = new byte[1024];
            var size = (uint)buffer.Length;
            var result = RegQueryValueExW(key, valueName, IntPtr.Zero, out _, buffer, ref size);
            if (result == ERROR_MORE_DATA)
            {
                buffer = new byte[size];
                result = RegQueryValueExW(key, valueName, IntPtr.Zero, out _, buffer, ref size);
            }

            if (result != ERROR_SUCCESS) return null;
            if (size < buffer.Length) Array.Resize(ref buffer, (int)size);
            return buffer;
        }
        finally
        {
            RegCloseKey(key);
        }
    }

    /// <summary>从设备接口路径中解析 VID/PID（形如 vid_2e3c&amp;pid_5751）。</summary>
    private static bool TryParseVidPid(string path, out ushort vid, out ushort pid)
    {
        vid = 0;
        pid = 0;
        var lower = path.ToLowerInvariant();
        var vi = lower.IndexOf("vid_", StringComparison.Ordinal);
        var pi = lower.IndexOf("pid_", StringComparison.Ordinal);
        if (vi < 0 || pi < 0) return false;
        return ushort.TryParse(lower.Substring(vi + 4, 4), System.Globalization.NumberStyles.HexNumber, null, out vid)
               && ushort.TryParse(lower.Substring(pi + 4, 4), System.Globalization.NumberStyles.HexNumber, null, out pid);
    }

    /// <summary>
    /// 一个已打开的 WinUSB 接口。读操作带超时，可安全地在一个循环里长期挂起。
    /// </summary>
    public sealed class Device : IDisposable
    {
        private readonly SafeFileHandle _file;
        private readonly IntPtr _winUsb;
        private readonly object _readLock = new();
        private readonly object _writeLock = new();
        private int _disposeState;

        public byte InPipeId { get; }
        public byte OutPipeId { get; }
        public ushort InPacketSize { get; }

        public bool IsOpen => Volatile.Read(ref _disposeState) == 0 && !_file.IsInvalid && !_file.IsClosed;

        private Device(SafeFileHandle file, IntPtr winUsb, byte inPipe, byte outPipe, ushort inPacketSize)
        {
            _file = file;
            _winUsb = winUsb;
            InPipeId = inPipe;
            OutPipeId = outPipe;
            InPacketSize = inPacketSize;
        }

        /// <summary>
        /// 打开设备并解析 bulk 端点。失败抛出 <see cref="Win32Exception"/> 风格的信息。
        /// </summary>
        public static Device Open(string devicePath)
        {
            var file = CreateFileW(devicePath, GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
            if (file.IsInvalid)
                throw new InvalidOperationException(
                    $"CreateFile 失败 ({Marshal.GetLastWin32Error()}): {devicePath}");

            if (!WinUsb_Initialize(file, out var winUsb))
            {
                var err = Marshal.GetLastWin32Error();
                file.Dispose();
                throw new InvalidOperationException($"WinUsb_Initialize 失败 ({err}): {devicePath}");
            }

            try
            {
                if (!WinUsb_QueryInterfaceSettings(winUsb, 0, out var desc))
                    throw new InvalidOperationException($"WinUsb_QueryInterfaceSettings 失败 ({Marshal.GetLastWin32Error()})");

                byte inPipe = 0, outPipe = 0;
                ushort inPacket = 0;
                for (byte i = 0; i < desc.bNumEndpoints; i++)
                {
                    if (!WinUsb_QueryPipe(winUsb, 0, i, out var pipe))
                        throw new InvalidOperationException($"WinUsb_QueryPipe 失败 ({Marshal.GetLastWin32Error()})");

                    // PipeType 0 = Control, 1 = Iso, 2 = Bulk, 3 = Interrupt
                    if (pipe.PipeType != 2) continue;
                    if ((pipe.PipeId & 0x80) != 0)
                    {
                        inPipe = pipe.PipeId;
                        inPacket = pipe.MaximumPacketSize;
                    }
                    else
                    {
                        outPipe = pipe.PipeId;
                    }
                }

                if (inPipe == 0 || outPipe == 0)
                    throw new InvalidOperationException("接口上缺少双向 bulk 端点");

                return new Device(file, winUsb, inPipe, outPipe, inPacket);
            }
            catch
            {
                WinUsb_Free(winUsb);
                file.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 读一批数据。超时或断开返回 0 / -1（不抛异常），便于读循环直接 continue。
        /// </summary>
        public int Read(byte[] buffer, int offset, int count, int timeoutMs)
        {
            if (!IsOpen) return -1;

            // 串行化：同一个接口只允许一个挂起的读。
            lock (_readLock)
            {
                if (!IsOpen) return -1;

                using var evt = new ManualResetEvent(false);
                var overlapped = CreateOverlapped(evt.SafeWaitHandle.DangerousGetHandle());

                // pin 住调用方的缓冲区，让 WinUSB 直接写进去，避免二次拷贝。
                var pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try
                {
                    var ptr = IntPtr.Add(pinned.AddrOfPinnedObject(), offset);
                    var ok = WinUsb_ReadPipe(_winUsb, InPipeId, ptr, (uint)count, out var transferred, overlapped);
                    if (!ok && Marshal.GetLastWin32Error() != ERROR_IO_PENDING)
                        return -1;

                    if (ok) return (int)transferred;

                    if (!evt.WaitOne(timeoutMs))
                    {
                        // OVERLAPPED 和缓冲区必须留到取消完成后才能释放。
                        CancelIoEx(_file, overlapped);
                        if (GetOverlappedResult(_file, overlapped, out transferred, true))
                            return (int)transferred;
                        return 0;
                    }

                    if (!GetOverlappedResult(_file, overlapped, out transferred, false))
                        return -1;

                    return (int)transferred;
                }
                finally
                {
                    pinned.Free();
                    Marshal.FreeHGlobal(overlapped);
                }
            }
        }

        /// <summary>
        /// 写一批数据（overlapped，带超时）。返回已写入字节数；失败返回 -1。
        /// </summary>
        public int Write(byte[] buffer, int offset, int count, int timeoutMs)
        {
            if (!IsOpen) return -1;

            lock (_writeLock)
            {
                if (!IsOpen) return -1;

                using var evt = new ManualResetEvent(false);
                var overlapped = CreateOverlapped(evt.SafeWaitHandle.DangerousGetHandle());
                var pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try
                {
                    var ptr = IntPtr.Add(pinned.AddrOfPinnedObject(), offset);
                    var ok = WinUsb_WritePipe(_winUsb, OutPipeId, ptr, (uint)count, out var transferred,
                        overlapped);
                    if (!ok && Marshal.GetLastWin32Error() != ERROR_IO_PENDING) return -1;

                    if (ok) return (int)transferred;

                    if (!evt.WaitOne(timeoutMs))
                    {
                        CancelIoEx(_file, overlapped);
                        GetOverlappedResult(_file, overlapped, out _, true);
                        return -1;
                    }

                    if (!GetOverlappedResult(_file, overlapped, out transferred, false)) return -1;
                    return (int)transferred;
                }
                finally
                {
                    pinned.Free();
                    Marshal.FreeHGlobal(overlapped);
                }
            }
        }

        private static IntPtr CreateOverlapped(IntPtr eventHandle)
        {
            var overlapped = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(OVERLAPPED)));
            Marshal.StructureToPtr(new OVERLAPPED { hEvent = eventHandle }, overlapped, false);
            return overlapped;
        }

        /// <summary>中止挂起的管道 I/O，用于断开时唤醒阻塞中的读循环。</summary>
        public void Abort()
        {
            if (Volatile.Read(ref _disposeState) != 0) return;
            try
            {
                if (InPipeId != 0) WinUsb_AbortPipe(_winUsb, InPipeId);
                if (OutPipeId != 0) WinUsb_AbortPipe(_winUsb, OutPipeId);
            }
            catch
            {
                // ignore
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposeState, 1) != 0) return;

            // 先终止管道，再等待读写方法离开，避免释放仍被异步 I/O 使用的句柄。
            if (InPipeId != 0) WinUsb_AbortPipe(_winUsb, InPipeId);
            if (OutPipeId != 0) WinUsb_AbortPipe(_winUsb, OutPipeId);
            CancelIoEx(_file, IntPtr.Zero);

            lock (_readLock)
            lock (_writeLock)
            {
                if (_winUsb != IntPtr.Zero) WinUsb_Free(_winUsb);
                _file.Dispose();
            }
        }
    }
}
