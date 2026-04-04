#nullable enable

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AquaMai.Mods.GameSystem.MaimollerIO.Libs;

public class MaimollerDeviceLegacy : IMaimollerDevice
{
#region P/Invoke
	private enum Status
	{
		DISCONNECTED,
		RUNNING,
		ERROR
	}


    [DllImport("libadxhid")]
    private static extern int adx_read(IntPtr dev, [Out] MaimollerInputReport rpt);

    [DllImport("libadxhid")]
    private static extern int adx_write(IntPtr dev, [In] MaimollerOutputReport rpt);

    [DllImport("libadxhid")]
    private static extern IntPtr adx_open(int player);

    [DllImport("libadxhid")]
    private static extern Status adx_status(IntPtr dev);
#endregion

    private IntPtr _dev = IntPtr.Zero;
    private float _reconnectTimer = 0f;
    private readonly int _player;
    private readonly bool _alt2p;
    private readonly MaimollerInputReport _input = new();
    private readonly MaimollerOutputReport _output = new();
    private readonly MaimollerLedManager _ledManager;
    public MaimollerDeviceLegacy(int player, bool alt2p)
    {
        _player = player;
        _alt2p = alt2p;
        _ledManager = new MaimollerLedManager(_output);
    }

    public void Open()
    {
        if (_dev != IntPtr.Zero) throw new InvalidOperationException($"MaimollerDevice {_player + 1}P already opened");

        // To align with the driver from manufacturer, 1P = 0 and 2P = 2.
        // But in IDA the devices array is {p1, p2, p1} and devices[0] == devices[2], so why?
        // 左右脑互搏这块，现在实际表现是有些情况 2p 在 1 上，有些时候 2p 在 2 上
        if (_player == 0) {
            _dev = adx_open(0);
        } else {
            _dev = adx_open(_alt2p ? 1 : 2);
        }
    }

    public void Update()
    {
        if (_dev == IntPtr.Zero) throw new InvalidOperationException($"MaimollerDevice {_player + 1}P not opened");

		adx_read(_dev, _input);
		adx_write(_dev, _output);

		if (adx_status(_dev) != Status.RUNNING)
		{
			float timeSinceLevelLoad = Time.timeSinceLevelLoad;
			if (timeSinceLevelLoad - _reconnectTimer > 1f)
			{
				adx_open(_player);
				_reconnectTimer = timeSinceLevelLoad;
			}
		}
    }

    public bool IsButtonPressed(int buttonIndex1To8)
    {
        if (buttonIndex1To8 < 1 || buttonIndex1To8 > 8) return false;
        return (_input.playerBtn & (1 << (buttonIndex1To8 - 1))) != 0;
    }

    public bool IsSystemButtonPressed(SystemButton button)
    {
        return (_input.systemBtn & (1 << (int)button)) != 0;
    }

    public ulong GetTouchState()
    {
        ulong s = 0;
        s |= _input.touches[0];                          // Touch A [0:7]
        s |= (ulong)_input.touches[1] << 8;              // Touch B [8:15]
        s |= (ulong)(_input.touches[2] & 0x03) << 16;    // Touch C [16:17]
        s |= (ulong)_input.touches[3] << 18;              // Touch D [18:25]
        s |= (ulong)_input.touches[4] << 26;              // Touch E [26:33]
        return s;
    }
    #region LED
    public void LedPreExecute() => _ledManager.PreExecute();
    public void SetButtonColor(int index, Color32 color) => _ledManager.SetButtonColor(index, color);
    public void SetButtonColorFade(int index, Color32 color, long duration) => _ledManager.SetButtonColorFade(index, color, duration);
    public void SetBodyIntensity(int index, byte intensity) => _ledManager.SetBodyIntensity(index, intensity);
    public void SetBillboardColor(Color32 color) => _ledManager.SetBillboardColor(color);
    #endregion
}
