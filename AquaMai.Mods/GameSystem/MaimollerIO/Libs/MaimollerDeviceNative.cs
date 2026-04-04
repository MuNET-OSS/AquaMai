#nullable enable

using System;

using System.Linq;
using System.Diagnostics;
using System.Threading;
using HidLibrary;
using MelonLoader;
using UnityEngine;

namespace AquaMai.Mods.GameSystem.MaimollerIO.Libs;

public class MaimollerDeviceNative : IMaimollerDevice
{
    private const int VID = 0x0E8F;
    private const int PID = 0x1224;

    private const int ButtonBitOffset = 34;
    private const int SystemBitOffset = 42;
    private const ulong TouchMask = (1UL << 34) - 1; // bits 0-33

    private readonly int _player;

    private volatile HidDevice? _device;
    private byte[]? _readBuffer; // pre-allocated, sized from device capabilities
    private bool _hidThreadRunning;
    private readonly byte[] _reportBuffer = new byte[64];
    private readonly byte[] _lastSentReport = new byte[64];
    private bool _forceNextWrite = true;
    private long _lastWriteTick;
    private volatile bool _connected;

    private readonly InputLatch _inputLatch = new();
    private readonly MaimollerOutputReport _output = new();
    private readonly MaimollerLedManager _ledManager;


    public MaimollerDeviceNative(int player)
    {
        _player = player;
        _ledManager = new MaimollerLedManager(_output);
    }

    public void Open()
    {
        if (_hidThreadRunning) throw new InvalidOperationException($"MaimollerDevice {_player + 1}P already opened");

        if (!TryConnectDevice())
        {
            MelonLogger.Warning($"[MaimollerDevice] {_player + 1}P device not found");
        }

        // 启动 HID 读取线程
        _hidThreadRunning = true;
        var hidThread = new Thread(HidInputThread)
        {
            IsBackground = true
        };
        hidThread.Start();
    }

    public void Update()
    {
        if (!_hidThreadRunning) throw new InvalidOperationException($"MaimollerDevice {_player + 1}P not opened");

        WriteOutputReport();
    }

    #region Input

    public bool IsButtonPressed(int buttonIndex1To8)
    {
        if (buttonIndex1To8 < 1 || buttonIndex1To8 > 8) return false;
        return _inputLatch.ReadBit(ButtonBitOffset + buttonIndex1To8 - 1);
    }

    public bool IsSystemButtonPressed(SystemButton button)
    {
        return _inputLatch.ReadBit(SystemBitOffset + (int)button);
    }

    public ulong GetTouchState()
    {
        return _inputLatch.ReadBits(TouchMask);
    }

    #endregion

    #region Connection

    private bool TryConnectDevice()
    {
        var devices = HidDevices.Enumerate(VID, PID)
            .Where(d => d.DevicePath.Contains("&mi_00#"))
            .ToArray();
        if (devices.Length == 0) return false;
        foreach (var device in devices)
        {
            try
            {
                device.OpenDevice();
            }
            catch
            {
                continue;
            }
            if (!device.ReadFeatureData(out var featureData, 1) || featureData.Length <= 32)
            {
                MelonLogger.Warning($"[MaimollerDevice] Failed to read feature report from {device.DevicePath}");
                device.CloseDevice();
                continue;
            }
            int devicePlayer = featureData[4] == 2 ? 1 : 0;
            if (devicePlayer != _player)
            {
                device.CloseDevice();
                continue;
            }
            _device = device;
            _readBuffer = new byte[device.Capabilities.InputReportByteLength];
            _connected = true;
            MelonLogger.Msg($"[MaimollerDevice] {_player + 1}P connected");
            return true;
        }
        return false;
    }
    private bool IsDeviceConnected()
    {
        return _device != null && _connected;
    }
    private void DisconnectDevice()
    {
        if (_device == null) return;
        try
        {
            _device.CloseDevice();
        }
        catch
        {
            // ignore
        }
        _connected = false;
        _device = null;
        _readBuffer = null;
        _inputLatch.Clear();
        _forceNextWrite = true;
        MelonLogger.Msg($"[MaimollerDevice] {_player + 1}P disconnected");
    }
    #endregion
    #region HID Thread
    private void HidInputThread()
    {
        while (_hidThreadRunning)
        {
            while (_hidThreadRunning && !IsDeviceConnected())
            {
                Thread.Sleep(500);
                TryConnectDevice();
            }
            if (!_hidThreadRunning) break;
            var device = _device;
            if (device == null) continue;
            try
            {
                var buf = _readBuffer;
                if (buf == null) continue;
                if (!HidRawIO.Read(device, buf, out var bytesRead) || bytesRead < 8)
                {
                    DisconnectDevice();
                    continue;
                }
                ulong state = 0;
                state |= (ulong)buf[1];                    // Touch A
                state |= (ulong)buf[2] << 8;              // Touch B
                state |= (ulong)(buf[3] & 0x03) << 16;    // Touch C (2 bits)
                state |= (ulong)buf[4] << 18;              // Touch D
                state |= (ulong)buf[5] << 26;              // Touch E
                state |= (ulong)buf[6] << 34;              // Player buttons
                state |= (ulong)(buf[7] & 0x0F) << 42;    // System buttons (4 bits)
                _inputLatch.Update(state);
            }
            catch
            {
                DisconnectDevice();
            }
        }
    }
    #endregion
    #region Output
    private void WriteOutputReport()
    {
        var device = _device;
        if (device == null || !_connected) return;
        try
        {
            // Serialize into pre-allocated buffer
            _reportBuffer[0] = 1; // report ID
            Array.Copy(_output.buttonColors, 0, _reportBuffer, 1, 24);
            _reportBuffer[25] = _output.circleBrightness;
            _reportBuffer[26] = _output.bodyBrightness;
            _reportBuffer[27] = _output.sideBrightness;
            Array.Copy(_output.billboardColor, 0, _reportBuffer, 28, 3);
            _reportBuffer[31] = (byte)_output.indicators;

            // Skip write if unchanged since last send
            if (!_forceNextWrite && _reportBuffer.SequenceEqual(_lastSentReport) && Stopwatch.GetTimestamp() - _lastWriteTick < Stopwatch.Frequency / 2)
                return;

            HidRawIO.Write(device, _reportBuffer);
            Array.Copy(_reportBuffer, _lastSentReport, 64);
            _lastWriteTick = Stopwatch.GetTimestamp();
            _forceNextWrite = false;
        }
        catch
        {
            // 写入失败视为断线
            DisconnectDevice();
        }
    }
    #endregion
    #region LED
    public void LedPreExecute() => _ledManager.PreExecute();
    public void SetButtonColor(int index, Color32 color) => _ledManager.SetButtonColor(index, color);
    public void SetButtonColorFade(int index, Color32 color, long duration) => _ledManager.SetButtonColorFade(index, color, duration);
    public void SetBodyIntensity(int index, byte intensity) => _ledManager.SetBodyIntensity(index, intensity);
    public void SetBillboardColor(Color32 color) => _ledManager.SetBillboardColor(color);
    #endregion
}