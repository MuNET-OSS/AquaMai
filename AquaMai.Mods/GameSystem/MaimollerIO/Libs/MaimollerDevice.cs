#nullable enable

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AquaMai.Mods.GameSystem.MaimollerIO.Libs;

public class MaimollerDevice(int player)
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

    public readonly MaimollerInputReport input = new();
    public readonly MaimollerOutputReport output = new();

    public void Open()
    {
        if (_dev != IntPtr.Zero) throw new InvalidOperationException($"MaimollerDevice {player + 1}P already opened");

        // To align with the driver from manufacturer, 1P = 0 and 2P = 2.
        // But in IDA the devices array is {p1, p2, p1} and devices[0] == devices[2], so why?
        // 经过测试，2P是1
        _dev = adx_open(player == 0 ? 0 : 1);
    }

    public void Update()
    {
        if (_dev == IntPtr.Zero) throw new InvalidOperationException($"MaimollerDevice {player + 1}P not opened");

		adx_read(_dev, input);
		adx_write(_dev, output);

		if (adx_status(_dev) != Status.RUNNING)
		{
			float timeSinceLevelLoad = Time.timeSinceLevelLoad;
			if (timeSinceLevelLoad - _reconnectTimer > 1f)
			{
				adx_open(player);
				_reconnectTimer = timeSinceLevelLoad;
			}
		}
    }
}

