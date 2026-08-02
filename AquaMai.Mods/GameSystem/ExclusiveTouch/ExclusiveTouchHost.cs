using System;
using AquaMai.Mods.GameSettings;
using MelonLoader;

namespace AquaMai.Mods.GameSystem.ExclusiveTouch;

public static class ExclusiveTouchHost
{
    public static void StartDevices(string tag, string path1p, string path2p,
        params Func<int, string, ExclusiveTouchBase>[] factories)
    {
        var devices = new ExclusiveTouchBase[2];

        if (string.IsNullOrWhiteSpace(path1p) && string.IsNullOrWhiteSpace(path2p))
        {
            devices[0] = StartDevice(0, null, factories);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(path1p))
            {
                devices[0] = StartDevice(0, path1p, factories);
            }
            if (!string.IsNullOrWhiteSpace(path2p))
            {
                devices[1] = StartDevice(1, path2p, factories);
            }
        }

        for (int i = 0; i < 2; i++)
        {
            if (devices[i] == null || !devices[i].IsConnected) continue;

            JudgeAdjust.shouldEnableImplicitly = true;
            if (i == 0) JudgeAdjust.b_1P += 1.0;
            else JudgeAdjust.b_2P += 1.0;
            MelonLogger.Msg($"[{tag}] {i + 1}P connected");
        }
    }

    private static ExclusiveTouchBase StartDevice(int playerNo, string locationPath,
        Func<int, string, ExclusiveTouchBase>[] factories)
    {
        foreach (var factory in factories)
        {
            var device = factory(playerNo, locationPath);
            device.Start();
            if (device.IsConnected) return device;
        }

        return null;
    }
}
