using System;
using System.Linq;
using System.Reflection;
using AquaMai.Config.Attributes;
using AquaMai.Config.Types;
using AquaMai.Core.Helpers;
using AquaMai.Mods.GameSystem.MaimollerIO.Libs;
using HarmonyLib;
using IO;
using Main;
using Manager;
using Mecha;
using MelonLoader;
using UnityEngine;

namespace AquaMai.Mods.GameSystem.MaimollerIO;

[ConfigCollapseNamespace]
[ConfigSection(
    name: "Maimoller IO 协议",
    en: """
        Input (buttons and touch) and output (LEDs) for Maimoller controllers, replacing the stock ADXHIDIOMod.dll.
        Don't enable this if you're not using Maimoller controllers.
        You must have libadxhid.dll and hidapi.dll in your Sinmai_Data/Plugins folder.
        Please remove ADXHIDIOMod.dll (if any) and disable DummyTouchpanel in mai2.ini.
        """,
    zh: """
        适配 Maimoller 控制器的输入（按钮和触屏）和输出（LED），替代厂商提供的 ADXHIDIOMod.dll。
        如果你没有使用 Maimoller 控制器，请勿启用。
        请在 Sinmai_Data/Plugins 文件夹下放置 libadxhid.dll 和 hidapi.dll。
        请删除 ADXHIDIOMod.dll（如果有）并关闭 mai2.ini 中的 DummyTouchpanel
        """)]
public class MaimollerIO
{
    [ConfigEntry(
        name: "启用 1P",
        en: "Enable 1P (If you mix Maimoller with other protocols, please disable for the side that is not Maimoller)",
        zh: "启用 1P（如果混用 Maimoller 与其他协议，请对不是 Maimoller 的一侧禁用）")]
    private static readonly bool p1 = true;

    [ConfigEntry(
        name: "启用 2P",
        en: "Enable 2P (If you mix Maimoller with other protocols, please disable for the side that is not Maimoller)",
        zh: "启用 2P（如果混用 Maimoller 与其他协议，请对不是 Maimoller 的一侧禁用）")]
    private static readonly bool p2 = true;

    [ConfigEntry(name: "按钮 1（三角形）")]
    private static readonly IOKeyMap button1 = IOKeyMap.Select1P;

    [ConfigEntry(name: "按钮 2（圆形）")]
    private static readonly IOKeyMap button2 = IOKeyMap.Test;

    [ConfigEntry(name: "按钮 3（圆形）")]
    private static readonly IOKeyMap button3 = IOKeyMap.Service;

    [ConfigEntry(name: "按钮 4（圆形）")]
    private static readonly IOKeyMap button4 = IOKeyMap.Select2P;

    private static readonly MaimollerInputReport.ButtonMask[] auxiliaryMaskMap =
    [
        /* button1 */ MaimollerInputReport.ButtonMask.SELECT,
        /* button2 */ MaimollerInputReport.ButtonMask.SERVICE,
        /* button3 */ MaimollerInputReport.ButtonMask.TEST,
        /* button4 */ MaimollerInputReport.ButtonMask.COIN,
    ];

    private static bool ShouldEnableForPlayer(int playerNo) => playerNo switch
    {
        0 => p1,
        1 => p2,
        _ => false,
    };

    private static readonly MaimollerDevice[] _devices = [.. Enumerable.Range(0, 2).Select(i => new MaimollerDevice(i))];
    private static readonly MaimollerLedManager[] _ledManagers = [.. Enumerable.Range(0, 2).Select(i => new MaimollerLedManager(_devices[i].output))];

    public static void OnBeforePatch()
    {
        if (p1)
        {
            _devices[0].Open();
            TouchStatusProvider.RegisterTouchStatusProvider(0, GetTouchState);
        }
        if (p2)
        {
            _devices[1].Open();
            TouchStatusProvider.RegisterTouchStatusProvider(1, GetTouchState);
        }
        JvsSwitchHook.RegisterButtonChecker(IsButtonPushed);
        JvsSwitchHook.RegisterAuxiliaryStateProvider(GetAuxiliaryState);
    }

    private static bool IsButtonPushed(int playerNo, int buttonIndex1To8) => buttonIndex1To8 switch
    {
        1 => _devices[playerNo].input.GetSwitchState(MaimollerInputReport.SwitchClass.BUTTON, MaimollerInputReport.ButtonMask.BTN_1) != 0,
        2 => _devices[playerNo].input.GetSwitchState(MaimollerInputReport.SwitchClass.BUTTON, MaimollerInputReport.ButtonMask.BTN_2) != 0,
        3 => _devices[playerNo].input.GetSwitchState(MaimollerInputReport.SwitchClass.BUTTON, MaimollerInputReport.ButtonMask.BTN_3) != 0,
        4 => _devices[playerNo].input.GetSwitchState(MaimollerInputReport.SwitchClass.BUTTON, MaimollerInputReport.ButtonMask.BTN_4) != 0,
        5 => _devices[playerNo].input.GetSwitchState(MaimollerInputReport.SwitchClass.BUTTON, MaimollerInputReport.ButtonMask.BTN_5) != 0,
        6 => _devices[playerNo].input.GetSwitchState(MaimollerInputReport.SwitchClass.BUTTON, MaimollerInputReport.ButtonMask.BTN_6) != 0,
        7 => _devices[playerNo].input.GetSwitchState(MaimollerInputReport.SwitchClass.BUTTON, MaimollerInputReport.ButtonMask.BTN_7) != 0,
        8 => _devices[playerNo].input.GetSwitchState(MaimollerInputReport.SwitchClass.BUTTON, MaimollerInputReport.ButtonMask.BTN_8) != 0,
        _ => false,
    };

    // NOTE: Coin button is not supported yet. AquaMai recommands setting fixed number of credits directly in the configuration.
    private static AuxiliaryState GetAuxiliaryState()
    {
        var auxiliaryState = new AuxiliaryState();
        IOKeyMap[] keyMaps = [button1, button2, button3, button4];
        for (int i = 0; i < 4; i++)
        {
            var is1PPushed = p1 && _devices[0].input.GetSwitchState(MaimollerInputReport.SwitchClass.SYSTEM, auxiliaryMaskMap[i]) != 0;
            var is2PPushed = p2 && _devices[1].input.GetSwitchState(MaimollerInputReport.SwitchClass.SYSTEM, auxiliaryMaskMap[i]) != 0;
            switch (keyMaps[i])
            {
            case IOKeyMap.Select1P:
                auxiliaryState.select1P |= is1PPushed || is2PPushed;
                break;
            case IOKeyMap.Select2P:
                auxiliaryState.select2P |= is1PPushed || is2PPushed;
                break;
            case IOKeyMap.Select:
                auxiliaryState.select1P |= is1PPushed;
                auxiliaryState.select2P |= is2PPushed;
                break;
            case IOKeyMap.Service:
                auxiliaryState.service = is1PPushed || is2PPushed;
                break;
            case IOKeyMap.Test:
                auxiliaryState.test = is1PPushed || is2PPushed;
                break;
            }
        }
        return auxiliaryState;
    }

    private static ulong GetTouchState(int i)
    {
        ulong s = 0;
        s |= (ulong)_devices[i].input.GetSwitchState(MaimollerInputReport.SwitchClass.TOUCH_A, MaimollerInputReport.ButtonMask.ANY_PLAYER);
        s |= (ulong)_devices[i].input.GetSwitchState(MaimollerInputReport.SwitchClass.TOUCH_B, MaimollerInputReport.ButtonMask.ANY_PLAYER) << 8;
        s |= (ulong)_devices[i].input.GetSwitchState(MaimollerInputReport.SwitchClass.TOUCH_C, MaimollerInputReport.ButtonMask.ANY_TOUCH_C) << 16;
        s |= (ulong)_devices[i].input.GetSwitchState(MaimollerInputReport.SwitchClass.TOUCH_D, MaimollerInputReport.ButtonMask.ANY_PLAYER) << 18;
        s |= (ulong)_devices[i].input.GetSwitchState(MaimollerInputReport.SwitchClass.TOUCH_E, MaimollerInputReport.ButtonMask.ANY_PLAYER) << 26;
        return s;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameMain), "Update")]
    public static void PreGameMainUpdate(bool ____isInitialize)
    {
        if (!____isInitialize) return;
        for (int i = 0; i < 2; i++)
        {
            if (!ShouldEnableForPlayer(i)) continue;
            _devices[i].Update();
        }
    }

    [HarmonyPatch]
    public static class JvsOutputPwmPatch
    {
        public static MethodInfo TargetMethod() => typeof(IO.Jvs).GetNestedType("JvsOutputPwm", BindingFlags.NonPublic | BindingFlags.Public).GetMethod("Set");

        public static void Prefix(byte index, Color32 color) => _ledManagers[index].SetBillboardColor(color);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Bd15070_4IF), "PreExecute")]
    public static void PostPreExecute(Bd15070_4IF.InitParam ____initParam) =>
        _ledManagers[____initParam.index].PreExecute();

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), "_setColor")]
    public static void Pre_setColor(byte ledPos, Color32 color, Bd15070_4IF.InitParam ____initParam) =>
        _ledManagers[____initParam.index].SetButtonColor(ledPos, color);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), "_setColorMulti")]
    public static void Pre_setColorMulti(Color32 color, byte speed, Bd15070_4IF.InitParam ____initParam) =>
        _ledManagers[____initParam.index].SetButtonColor(-1, color);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), "_setColorMultiFade")]
    public static void Pre_setColorMultiFade(Color32 color, byte speed, Bd15070_4IF.InitParam ____initParam) =>
        _ledManagers[____initParam.index].SetButtonColorFade(-1, color, GetByte2Msec(speed));

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), "_setColorMultiFet")]
    public static void Pre_setColorMultiFet(Color32 color, Bd15070_4IF.InitParam ____initParam)
    {
        _ledManagers[____initParam.index].SetBodyIntensity(8, color.r);
        _ledManagers[____initParam.index].SetBodyIntensity(9, color.g);
        _ledManagers[____initParam.index].SetBodyIntensity(10, color.b);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), "_setColorFet")]
    public static void Pre_setColorFet(byte ledPos, byte color, Bd15070_4IF.InitParam ____initParam) =>
        _ledManagers[____initParam.index].SetBodyIntensity(ledPos, color);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), "_setLedAllOff")]
    public static void Pre_setLedAllOff(Bd15070_4IF.InitParam ____initParam)
    {
        _ledManagers[____initParam.index].SetBodyIntensity(-1, 0);
        _ledManagers[____initParam.index].SetButtonColor(-1, new Color32(0, 0, 0, byte.MaxValue));
    }

    private static long GetByte2Msec(byte speed) => (long)Math.Round(4095.0 / speed * 8.0);
}
