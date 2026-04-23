using HarmonyLib;
using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using Process;
using UnityEngine;
using UnityEngine.UI;
using System;
using MelonLoader;

namespace AquaMai.Mods.Fancy;

[ConfigSection(
    name: "转场动画",
    en: "Set Fade Animation",
    zh: "修改转场动画为其他变种"
)]

public class SetFade
{
    [ConfigEntry(
        name: "转场类型",
        en: "Type: Non-Plus 0, Plus 1. (If SDEZ 1.60 can choose Festa 2)",
        zh: "类型: Non-Plus 0, Plus 1. (SDEZ 1.60 限定可选 Festa 2)")]
    public static readonly int FadeType = 0;


    private static bool isInitialized = false;
    private static bool isResourcePatchEnabled = false;
    private static Sprite[] subBGs = new Sprite[3];


    [HarmonyPrepare]
    public static bool SetFade_Prepare()
    {
        SetFade_Initialize();
        if (!isInitialized)
            MelonLogger.Msg("[SetFade] Initialization failed, this patch will not be applied.");
        return isInitialized;
    }

    private static void SetFade_Initialize()
    {
        bool areSubBGsValid;
        bool isFadeTypeValid;

        subBGs[0] = Resources.Load<Sprite>("Process/ChangeScreen/Sprites/Sub_01");
        subBGs[1] = Resources.Load<Sprite>("Process/ChangeScreen/Sprites/Sub_02");
        areSubBGsValid = subBGs[0] != null && subBGs[1] != null;
        isFadeTypeValid = FadeType == 0 || FadeType == 1;

        // make it future proof maybe?
        if (GameInfo.GameVersion >= 26000)
        {
            var festaSubBG = Resources.Load<Sprite>("Process/ChangeScreen/Sprites/Sub_03");
            if (festaSubBG != null)
            {
                subBGs[2] = festaSubBG;
                areSubBGsValid = areSubBGsValid && subBGs[2] != null;
                isFadeTypeValid = isFadeTypeValid || FadeType == 2;
            }
        }

        if (!areSubBGsValid)
            MelonLogger.Msg($"[SwitchFade] Couldn't find SubBG sprites.");

        if (!isFadeTypeValid)
            MelonLogger.Msg($"[SwitchFade] Invalid FadeType.");

        isInitialized = areSubBGsValid && isFadeTypeValid;
    }


    // 在显示转场前启用patch
    [HarmonyPrefix]
    [HarmonyPatch(typeof(FadeProcess), "OnStart")]
    public static void FadeProcessOnStartPreFix() { isResourcePatchEnabled = true; }
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AdvertiseProcess), "InitFade")]
    public static void AdvertiseProcessInitFadePreFix() { isResourcePatchEnabled = true; }
    [HarmonyPrefix]
    [HarmonyPatch(typeof(NextTrackProcess), "OnStart")]
    public static void NextTrackProcessOnStartPreFix() { isResourcePatchEnabled = true; }

    // 在显示转场后禁用patch
    [HarmonyPostfix]
    [HarmonyPatch(typeof(FadeProcess), "OnStart")]
    public static void FadeProcessOnStartPostFix(GameObject[] ___fadeObject) { ReplaceSubBG(___fadeObject); }
    [HarmonyPostfix]
    [HarmonyPatch(typeof(AdvertiseProcess), "InitFade")]
    public static void AdvertiseProcessInitFadePostFix(GameObject[] ___fadeObject) { ReplaceSubBG(___fadeObject); }
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NextTrackProcess), "OnStart")]
    public static void NextTrackProcessOnStartPostFix(GameObject[] ___fadeObject) { ReplaceSubBG(___fadeObject); }
    

    private static void ReplaceSubBG(GameObject[] fadeObjects)
    {
        isResourcePatchEnabled = false;
        foreach (var monitor in fadeObjects)
        {
            var subBG = monitor.transform.Find("Canvas/Sub/Sub_ChangeScreen(Clone)/Sub_BG").GetComponent<Image>();
            subBG.sprite = subBGs[FadeType];
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Resources), "Load", new[] { typeof(string), typeof(Type) })]
    public static bool ResourcesLoadPrefix(ref string path, Type systemTypeInstance, ref UnityEngine.Object __result)
    {
        if (isResourcePatchEnabled)
        {
            if (path.StartsWith("Process/ChangeScreen/Prefabs/ChangeScreen_0") &&
                path != $"Process/ChangeScreen/Prefabs/ChangeScreen_0{FadeType + 1}") // 避免无限递归
            {
                __result = Resources.Load($"Process/ChangeScreen/Prefabs/ChangeScreen_0{FadeType + 1}", systemTypeInstance);
                return false;
            }
        }
        return true;
    }
}
