using System;
using System.Collections.Generic;
using HarmonyLib;
using IO;
using Manager;
using MelonLoader;

namespace AquaMai.Core.Helpers;

public class TouchStatusProvider
{
    public delegate ulong TouchStatusProviderDelegate(int playerNo);

    private static readonly List<TouchStatusProviderDelegate>[] _touchStatusProviders = [new List<TouchStatusProviderDelegate>(), new List<TouchStatusProviderDelegate>()];

    public static void RegisterTouchStatusProvider(int playerNo, TouchStatusProviderDelegate touchStatusProvider)
    {
        if (playerNo is < 0 or > 1) throw new ArgumentException("Invalid player");
        EnsurePatched();
        _touchStatusProviders[playerNo].Add(touchStatusProvider);
    }
    private static bool isPatched = false;
    private static bool EnsurePatched()
    {
        if (isPatched) return false;
        isPatched = true;
        Startup.ApplyPatch(typeof(TouchStatusProvider));
        return true;
    }

    private static bool ShouldEnableForPlayer(int playerNo) => playerNo switch
    {
        0 => _touchStatusProviders[0].Count > 0,
        1 => _touchStatusProviders[1].Count > 0,
        _ => false,
    };

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NewTouchPanel), "Start")]
    public static bool PreNewTouchPanelStart(uint ____monitorIndex, ref NewTouchPanel.StatusEnum ___Status, ref bool ____isRunning)
    {
        if (!ShouldEnableForPlayer((int)____monitorIndex)) return true;
        ___Status = NewTouchPanel.StatusEnum.Drive;
        ____isRunning = true;
        MelonLogger.Msg($"[TouchStatusProvider] NewTouchPanel Start {____monitorIndex + 1}P");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NewTouchPanel), "Execute")]
    public static bool PreNewTouchPanelExecute(uint ____monitorIndex, ref uint ____dataCounter)
    {
        if (!ShouldEnableForPlayer((int)____monitorIndex)) return true;
        ulong currentTouchData = 0;
        foreach (var touchStatusProvider in _touchStatusProviders[(int)____monitorIndex])
        {
            currentTouchData |= touchStatusProvider((int)____monitorIndex);
        }

        InputManager.SetNewTouchPanel(____monitorIndex, currentTouchData, ++____dataCounter);
        return false;
    }
}
