using AquaMai.Config.Attributes;
using HarmonyLib;
using IO;
using Mecha;
using Process;

namespace AquaMai.Mods.UX;

[ConfigSection(
    name: "闲置时关闭灯光",
    en: "Disable button LED when not playing",
    zh: """
        在游戏闲置时关闭外键和框体的灯光
        “一闪一闪的 闪的我心发慌”
        """)]
public static class DisableLightOutGame
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(AdvDemoProcess), "OnStart")]
    public static void AdvDemoProcessStart()
    {
        MechaManager.SetAllCuOff();
        isInGame = false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AdvertiseProcess), "OnStart")]
    public static void AdvertiseProcessStart()
    {
        MechaManager.SetAllCuOff();
        isInGame = false;
    }

    private static bool isInGame = false;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AdvertiseProcess), "OnStart")]
    public static void AdvertiseProcessPreStart()
    {
        MechaManager.SetAllCuOff();
        isInGame = false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EntryProcess), "OnStart")]
    public static void EntryProcessPreStart()
    {
        isInGame = true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), nameof(Bd15070_4IF.SetColor))]
    [HarmonyPatch(typeof(Bd15070_4IF), nameof(Bd15070_4IF.SetColorFet))]
    [HarmonyPatch(typeof(Bd15070_4IF), nameof(Bd15070_4IF.SetColorFetAutoFade))]
    [HarmonyPatch(typeof(Bd15070_4IF), nameof(Bd15070_4IF.SetColorMultiFet))]
    [HarmonyPatch(typeof(Bd15070_4IF), nameof(Bd15070_4IF.SetColorMulti))]
    [HarmonyPatch(typeof(Bd15070_4IF), nameof(Bd15070_4IF.SetColorMultiFade))]
    [HarmonyPatch(typeof(Bd15070_4IF), nameof(Bd15070_4IF.SetColorMultiAutoFade))]
    [HarmonyPatch(typeof(Bd15070_4IF), nameof(Bd15070_4IF.SetColorSwitch))]
    [HarmonyPatch(typeof(Bd15070_4IF), nameof(Bd15070_4IF.SetColorButton))]
    [HarmonyPatch(typeof(Bd15070_4IF), nameof(Bd15070_4IF.SetColorButtonPressed))]
    public static bool IsNotBlockSetColor()
    {
        return isInGame;
    }
}