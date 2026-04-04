using System;
using System.Runtime.InteropServices;
using HidLibrary;

namespace AquaMai.Mods.GameSystem;

internal static class HidRawIO
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(
        IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteFile(
        IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite,
        out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);

    public static bool Read(HidDevice device, byte[] buffer, out int bytesRead)
    {
        bytesRead = 0;
        if (!device.IsOpen) return false;
        var success = ReadFile(device.ReadHandle, buffer, (uint)buffer.Length, out var read, IntPtr.Zero);
        bytesRead = (int)read;
        return success && bytesRead > 0;
    }

    public static bool Write(HidDevice device, byte[] buffer)
    {
        if (!device.IsOpen) return false;
        return WriteFile(device.WriteHandle, buffer, (uint)buffer.Length, out _, IntPtr.Zero);
    }
}