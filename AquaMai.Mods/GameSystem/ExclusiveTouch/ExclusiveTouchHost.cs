using System;
using System.Threading;
using AquaMai.Core.Helpers;
using AquaMai.Mods.GameSettings;
using MelonLoader;
using UnityEngine;

namespace AquaMai.Mods.GameSystem.ExclusiveTouch;

public static class ExclusiveTouchHost
{
    private static volatile bool quitting;

    private sealed class DeviceSlot(string tag, int playerNo, string locationPath,
        Func<int, string, ExclusiveTouchBase>[] factories)
    {
        private ExclusiveTouchBase device;

        public ulong GetTouchState(int requestedPlayerNo)
        {
            var currentDevice = Volatile.Read(ref device);
            return currentDevice?.GetTouchState(requestedPlayerNo) ?? 0;
        }

        public void Start()
        {
            var connectedDevice = StartDevice(playerNo, locationPath, factories, logConnectionFailure: true);
            if (connectedDevice != null)
            {
                Connect(connectedDevice);
                return;
            }

            MelonLogger.Msg($"[{tag}] {playerNo + 1}P waiting for device");
            Thread retryThread = new(Retry);
            retryThread.IsBackground = true;
            retryThread.Start();
        }

        private void Retry()
        {
            while (!quitting)
            {
                Thread.Sleep(1000);
                if (quitting) return;

                var connectedDevice = StartDevice(playerNo, locationPath, factories,
                    logConnectionFailure: false);
                if (connectedDevice == null) continue;

                Connect(connectedDevice);
                return;
            }
        }

        private void Connect(ExclusiveTouchBase connectedDevice)
        {
            Volatile.Write(ref device, connectedDevice);
            OnDeviceConnected(tag, playerNo);
        }
    }

    public static void StartDevices(string tag, string path1p, string path2p,
        params Func<int, string, ExclusiveTouchBase>[] factories)
    {
        quitting = false;
        Application.quitting += () => quitting = true;

        if (string.IsNullOrWhiteSpace(path1p) && string.IsNullOrWhiteSpace(path2p))
        {
            StartDeviceSlot(tag, 0, null, factories);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(path1p))
            {
                StartDeviceSlot(tag, 0, path1p, factories);
            }
            if (!string.IsNullOrWhiteSpace(path2p))
            {
                StartDeviceSlot(tag, 1, path2p, factories);
            }
        }
    }

    private static void StartDeviceSlot(string tag, int playerNo, string locationPath,
        Func<int, string, ExclusiveTouchBase>[] factories)
    {
        var slot = new DeviceSlot(tag, playerNo, locationPath, factories);
        TouchStatusProvider.RegisterTouchStatusProvider(playerNo, slot.GetTouchState);
        slot.Start();
    }

    private static void OnDeviceConnected(string tag, int playerNo)
    {
        JudgeAdjust.shouldEnableImplicitly = true;
        if (playerNo == 0) JudgeAdjust.b_1P += 1.0;
        else JudgeAdjust.b_2P += 1.0;
        MelonLogger.Msg($"[{tag}] {playerNo + 1}P connected");
    }

    private static ExclusiveTouchBase StartDevice(int playerNo, string locationPath,
        Func<int, string, ExclusiveTouchBase>[] factories, bool logConnectionFailure)
    {
        foreach (var factory in factories)
        {
            var device = factory(playerNo, locationPath);
            if (device.Start(logConnectionFailure)) return device;
        }

        return null;
    }
}
