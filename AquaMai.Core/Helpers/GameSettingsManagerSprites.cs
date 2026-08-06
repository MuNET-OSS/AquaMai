using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Process;
using UnityEngine;

namespace AquaMai.Core.Helpers;

public class GameSettingsManagerSprites
{
    private static Dictionary<string, AssetBundle> bundleMap = new();
    private static bool isPatched = false;
    private static readonly object patchLock = new();

    public static void RegisterBundle(string prefix, AssetBundle bundle)
    {
        if(bundleMap.ContainsKey(prefix)) return;
        bundleMap.Add(prefix, bundle);
        lock(patchLock)
        {
            if (!isPatched)
            {
                isPatched = true;
                Startup.ApplyPatch(typeof(GameSettingsManagerSprites));
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MusicSelectProcess), nameof(MusicSelectProcess.GetOptionValueSprite))]
    public static void GetOptionValueSprite(ref Sprite __result, string key)
    {
        var bundle = bundleMap.FirstOrDefault(it=>key.StartsWith(it.Key)).Value;
        if(bundle == null) return;
        var bundled = bundle.LoadAsset<Sprite>(key);
        if(bundled == null) return;
        __result = bundled;
    }
}