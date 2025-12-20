using System.Collections.Generic;
using System.Reflection;
using AMDaemon;
using AquaMai.Config.Attributes;
using AquaMai.Config.Types;
using AquaMai.Core.Attributes;
using HarmonyLib;
using UnityEngine;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "按键映射",
    en: """
        Enable or disable IO4 and DebugInput. Configure the key mapping for DebugInput.
        DebugInput works independently of IO4 (IO4-compatible board / segatools IO4 emulation).
        (You should enable at least one input source, unless you use other input solutions like AdxHidInput.)
        """,
    zh: """
        启用或禁用 IO4 和 DebugInput。配置 DebugInput 的按键映射。
        DebugInput 与 IO4（兼容 IO4 板 / segatools IO4 模拟）独立工作。
        （你应该至少启用一个输入源，除非你使用其他输入方案如 AdxHidInput。）
        """,
    defaultOn: true)]
public class KeyMap
{
    [ConfigEntry(
        name: "禁用 IO4（1P）",
        en: """
            Disable IO4 (IO4-compatible board / segatools IO4 emulation) input.
            With IO4 input disabled, your IO4-compatible board or segatools IO4 emulation is ignored.
            """,
        zh: """
            禁用 IO4（兼容 IO4 板 / segatools IO4 模拟）输入。
            在禁用 IO4 输入后，你的兼容 IO4 板或 segatools IO4 模拟将被忽略。
            """)]
    private static readonly bool disableIO4_1P = false;
    [ConfigEntry("禁用 IO4（2P）")]
    private static readonly bool disableIO4_2P = false;

    [ConfigEntry(
        name: "禁用 IO4（系统按键）",
        en: """
            System buttons (test, service) input.
            """,
        zh: """
            禁用系统按键的 IO4 输入，输入源同上。
            """)]
    private static readonly bool disableIO4System = false;

    [ConfigEntry(
        name: "禁用 DebugInput",
        en: """
            Disable DebugInput. The key mapping below will not work.
            With DebugInput disabled, you'll need a IO4-compatible board, segatools IO4 emulation or other custom input solutions to play.
            You may want to configure IO4 emulation key mapping in segatools.ini's [io4] and [button] section.
            """,
        zh: """
            禁用 DebugInput，下列按键映射将不起作用。
            在禁用 DebugInput 后，你需要兼容 IO4 板、segatools IO4 模拟或其他自定义输入方案才能游玩。
            如果使用 IO4 模拟，你可以在 segatools.ini 的 [io4] 和 [button] 部分配置按键映射。
            """)]
    public static readonly bool disableDebugInput = false; // Implemented in AquaMai.Mods/Fix/Common.cs

    [ConfigEntry(
        name: "禁用调试快捷键",
        en: """
            Disable DebugFeature Polyfill hotkeys, like Enter to pause, Left/Right to seek, etc.
            """,
        zh: """
            禁用 DebugFeature Polyfill 的快捷键，比如说回车暂停，左右键快进快退等。
            """)]
    public static readonly bool disableDebugFeatureHotkeys = false; // Implemented in DebugFeature

    private static bool DisableIO4 => disableIO4_1P || disableIO4_2P || disableIO4System;
    private static List<SwitchInput> disabledSwitchInputs = [];

    [EnableIf(nameof(DisableIO4))]
    [HarmonyPatch("IO.Jvs+JvsSwitch", ".ctor", MethodType.Constructor, [typeof(int), typeof(string), typeof(KeyCode), typeof(bool), typeof(bool)])]
    [HarmonyPostfix]
    public static void PostJvsSwitchConstructor(ref bool ____invert, int playerNo, bool systemButton, SwitchInput ____switchInput)
    {
        if ((systemButton && disableIO4System) || (playerNo == 0 && disableIO4_1P) || (playerNo == 1 && disableIO4_2P))
        {
            ____invert = false;
            disabledSwitchInputs.Add(____switchInput);
        }
    }

    [EnableIf(nameof(DisableIO4))]
    [HarmonyPatch(typeof(SwitchInput), nameof(SwitchInput.IsOn), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool PreGetIsOn(ref bool __result, SwitchInput __instance)
    {
        if (disabledSwitchInputs.Contains(__instance))
        {
            __result = false;
            return false;
        }
        return true;
    }

    [EnableIf(nameof(DisableIO4))]
    [HarmonyPatch(typeof(SwitchInput), nameof(SwitchInput.HasOnNow), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool PreGetHasOnNow(ref bool __result, SwitchInput __instance)
    {
        if (disabledSwitchInputs.Contains(__instance))
        {
            __result = false;
            return false;
        }
        return true;
    }

    [ConfigEntry]
    public static readonly KeyCodeID Test = (KeyCodeID)115;

    [ConfigEntry]
    public static readonly KeyCodeID Service = (KeyCodeID)5;

    [ConfigEntry]
    public static readonly KeyCodeID Button1_1P = (KeyCodeID)67;

    [ConfigEntry]
    public static readonly KeyCodeID Button2_1P = (KeyCodeID)49;

    [ConfigEntry]
    public static readonly KeyCodeID Button3_1P = (KeyCodeID)48;

    [ConfigEntry]
    public static readonly KeyCodeID Button4_1P = (KeyCodeID)47;

    [ConfigEntry]
    public static readonly KeyCodeID Button5_1P = (KeyCodeID)68;

    [ConfigEntry]
    public static readonly KeyCodeID Button6_1P = (KeyCodeID)70;

    [ConfigEntry]
    public static readonly KeyCodeID Button7_1P = (KeyCodeID)45;

    [ConfigEntry]
    public static readonly KeyCodeID Button8_1P = (KeyCodeID)61;

    [ConfigEntry]
    public static readonly KeyCodeID Select_1P = (KeyCodeID)25;

    [ConfigEntry]
    public static readonly KeyCodeID Button1_2P = (KeyCodeID)80;

    [ConfigEntry]
    public static readonly KeyCodeID Button2_2P = (KeyCodeID)81;

    [ConfigEntry]
    public static readonly KeyCodeID Button3_2P = (KeyCodeID)78;

    [ConfigEntry]
    public static readonly KeyCodeID Button4_2P = (KeyCodeID)75;

    [ConfigEntry]
    public static readonly KeyCodeID Button5_2P = (KeyCodeID)74;

    [ConfigEntry]
    public static readonly KeyCodeID Button6_2P = (KeyCodeID)73;

    [ConfigEntry]
    public static readonly KeyCodeID Button7_2P = (KeyCodeID)76;

    [ConfigEntry]
    public static readonly KeyCodeID Button8_2P = (KeyCodeID)79;

    [ConfigEntry]
    public static readonly KeyCodeID Select_2P = (KeyCodeID)84;

    [ConfigEntry]
    public static readonly KeyCodeID Autoplay = (KeyCodeID)94;

    public static string GetAutoplay() {return Autoplay.ToString();}

    [HarmonyPatch(typeof(DB.JvsButtonTableRecord), MethodType.Constructor, typeof(int), typeof(string), typeof(string), typeof(int), typeof(string), typeof(int), typeof(int), typeof(int))]
    [HarmonyPostfix]
    public static void JvsButtonTableRecordConstructor(DB.JvsButtonTableRecord __instance, string Name)
    {
        var prop = (DB.KeyCodeID)typeof(KeyMap).GetField(Name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).GetValue(null);
        __instance.SubstituteKey = prop;
    }
}
