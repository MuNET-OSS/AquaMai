using System;
using System.Linq;
using System.Reflection;
using AquaMai.Config.Attributes;
using AquaMai.Config.Types;
using AquaMai.Core.Attributes;
using AquaMai.Core.Helpers;
using AquaMai.Mods.GameSystem.MaimollerIO.Libs;
using HarmonyLib;
using Main;
using Mecha;
using UnityEngine;

namespace AquaMai.Mods.GameSystem.MaimollerIO;

[ConfigCollapseNamespace]
[ConfigSection(
    name: "Maimoller IO V2",
    en: """
        Input (buttons and touch) and output (LEDs) for Maimoller controllers, replacing the stock ADXHIDIOMod.dll.
        Don't enable this if you're not using Maimoller controllers.
        Please remove ADXHIDIOMod.dll (if any) and disable DummyTouchpanel in mai2.ini.
        """,
    zh: """
        适配 Maimoller 控制器的输入（按钮和触屏）和输出（LED），替代厂商提供的 ADXHIDIOMod.dll。
        如果你没有使用 Maimoller 控制器，请勿启用。
        请删除 ADXHIDIOMod.dll（如果有）并关闭 mai2.ini 中的 DummyTouchpanel。
        """)]
public class MaimollerIO
{
    [ConfigEntry(
        name: "启用 1P 触屏",
        en: "Enable 1P TouchScreen (If you mix Maimoller with other protocols, please disable for the side that is not Maimoller)",
        zh: "如果混用 Maimoller 与其他协议，请对不是 Maimoller 的一侧禁用")]
    private static readonly bool touch1p = true;

    [ConfigEntry(
        name: "启用 1P 按键",
        en: "Enable 1P Buttons")]
    private static readonly bool button1p = true;

    [ConfigEntry(
        name: "启用 1P 灯光",
        en: "Enable 1P LEDs")]
    private static readonly bool led1p = true;

    [ConfigEntry(
        name: "启用 2P 触屏",
        en: "Enable 2P")]
    private static readonly bool touch2p = true;

    [ConfigEntry(
        name: "启用 2P 按键",
        en: "Enable 2P Buttons")]
    private static readonly bool button2p = true;

    [ConfigEntry(
        name: "启用 2P 灯光",
        en: "Enable 2P LEDs")]
    private static readonly bool led2p = true;

    [ConfigEntry("启用兼容模式",
       en: """
       Use legacy implementation based on vendor DLLs
       You must have libadxhid.dll and hidapi.dll in your Sinmai_Data/Plugins folder. But please do not place ADXHIDIOMod.dll
       """,
       zh: """
       使用基于厂商 DLL 的旧版实现
       请在 Sinmai_Data/Plugins 文件夹下放置 libadxhid.dll 和 hidapi.dll。但请不要放置 ADXHIDIOMod.dll
       """)]
    private static readonly bool useLegacy = false;

    [ConfigEntry("兼容性 2P 模式",
       en: "When mixing Maimoller with other protocols and Maimoller is on 2P, and Maimoller is not working properly, enable this (only works for legacy implementation)",
       zh: "当混用 Maimoller 与其他协议并且 Maimoller 在 2P 上，而且 Maimoller 无法正常使用时开启（仅对旧版实现有效）")]
    private static readonly bool alternative2p = false;

    private static bool ShouldInitForPlayer(int playerNo) => playerNo switch
    {
        0 => touch1p || button1p || led1p,
        1 => touch2p || button2p || led2p,
        _ => false,
    };

    private static bool IsTouchEnabledForPlayer(int playerNo) => playerNo switch
    {
        0 => touch1p,
        1 => touch2p,
        _ => false,
    };

    private static bool IsButtonEnabledForPlayer(int playerNo) => playerNo switch
    {
        0 => button1p,
        1 => button2p,
        _ => false,
    };

    private static bool IsLedEnabledForPlayer(int playerNo) => playerNo switch
    {
        0 => led1p,
        1 => led2p,
        _ => false,
    };

    private static bool IsAnyLedEnabled => led1p || led2p;

    [ConfigEntry(name: "按钮 1（三角形）")]
    private static readonly IOKeyMap button1 = IOKeyMap.Select1P;

    [ConfigEntry(name: "按钮 2（圆形）")]
    private static readonly IOKeyMap button2 = IOKeyMap.Test;

    [ConfigEntry(name: "按钮 3（圆形）")]
    private static readonly IOKeyMap button3 = IOKeyMap.Service;

    [ConfigEntry(name: "按钮 4（圆形）")]
    private static readonly IOKeyMap button4 = IOKeyMap.Select2P;

    private static readonly SystemButton[] auxiliaryButtonMap =
    [
        /* button1 */ SystemButton.Select,
        /* button2 */ SystemButton.Service,
        /* button3 */ SystemButton.Test,
        /* button4 */ SystemButton.Coin,
    ];


    private static IMaimollerDevice[] _devices;

    public static void OnBeforePatch()
    {
        _devices = [.. Enumerable.Range(0, 2).Select(i => (IMaimollerDevice)(useLegacy ? new MaimollerDeviceLegacy(i, alternative2p) : new MaimollerDeviceNative(i)))];
        for (int i = 0; i < 2; i++)
        {
            if (!ShouldInitForPlayer(i)) continue;
            _devices[i].Open();

            if (IsTouchEnabledForPlayer(i))
            {
                TouchStatusProvider.RegisterTouchStatusProvider(i, GetTouchState);
            }
        }

        if (button1p || button2p)
        {
            JvsSwitchHook.RegisterButtonChecker(IsButtonPushed);
            JvsSwitchHook.RegisterAuxiliaryStateProvider(GetAuxiliaryState);
            JvsSwitchHook.RegisterCustomFnStateProvider(GetCustomFnState);
        }
    }

    #region Button

    private static bool IsButtonPushed(int playerNo, int buttonIndex1To8)
    {
        if (!IsButtonEnabledForPlayer(playerNo)) return false;
        return _devices[playerNo].IsButtonPressed(buttonIndex1To8);
    }

    // NOTE: Coin button is not supported yet. AquaMai recommands setting fixed number of credits directly in the configuration.

    private static AuxiliaryState GetAuxiliaryState()
    {
        var auxiliaryState = new AuxiliaryState();
        IOKeyMap[] keyMaps = [button1, button2, button3, button4];
        for (int i = 0; i < 4; i++)
        {
            var is1PPushed = button1p && _devices[0].IsSystemButtonPressed(auxiliaryButtonMap[i]);
            var is2PPushed = button2p && _devices[1].IsSystemButtonPressed(auxiliaryButtonMap[i]);
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
    
    private static CustomFnState GetCustomFnState()
    {
        var result = new CustomFnState();
        IOKeyMap[] keyMaps = [button1, button2, button3, button4];
        for (int i = 0; i < 4; i++)
        {
            var is1PPushed = button1p && _devices[0].IsSystemButtonPressed(auxiliaryButtonMap[i]);
            var is2PPushed = button2p && _devices[1].IsSystemButtonPressed(auxiliaryButtonMap[i]);
            var isPushed = is1PPushed || is2PPushed;
            switch (keyMaps[i])
            {
                case IOKeyMap.CustomFn1:
                    result.CustomFn1 |= isPushed;
                    break;
                case IOKeyMap.CustomFn2:
                    result.CustomFn2 |= isPushed;
                    break;
                case IOKeyMap.CustomFn3:
                    result.CustomFn3 |= isPushed;
                    break;
                case IOKeyMap.CustomFn4:
                    result.CustomFn4 |= isPushed;
                    break;
            }
        }

        return result;
    }

    #endregion

    private static ulong GetTouchState(int i)
    {
        return _devices[i].GetTouchState();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameMain), "Update")]
    public static void PreGameMainUpdate(bool ____isInitialize)
    {
        if (!____isInitialize) return;
        for (int i = 0; i < 2; i++)
        {
            if (!ShouldInitForPlayer(i)) continue;
            _devices[i].Update();
        }
    }

    [HarmonyPatch]
    [EnableIf(typeof(MaimollerIO), nameof(IsAnyLedEnabled))]
    public static class JvsOutputPwmPatch
    {
        public static MethodInfo TargetMethod() => typeof(IO.Jvs).GetNestedType("JvsOutputPwm", BindingFlags.NonPublic | BindingFlags.Public).GetMethod("Set");

        public static void Prefix(byte index, Color32 color)
        {
            if (!IsLedEnabledForPlayer(index)) return;
            _devices[index].SetBillboardColor(color);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Bd15070_4IF), "PreExecute")]
    [EnableIf(nameof(IsAnyLedEnabled))]
    public static void PostPreExecute(Bd15070_4IF.InitParam ____initParam)
    {
        if (!IsLedEnabledForPlayer(____initParam.index)) return;
        _devices[____initParam.index].LedPreExecute();
    }


    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), "_setColor")]
    [EnableIf(nameof(IsAnyLedEnabled))]
    public static void Pre_setColor(byte ledPos, Color32 color, Bd15070_4IF.InitParam ____initParam)
    {
        if (!IsLedEnabledForPlayer(____initParam.index)) return;
        _devices[____initParam.index].SetButtonColor(ledPos, color);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), "_setColorMulti")]
    [EnableIf(nameof(IsAnyLedEnabled))]
    public static void Pre_setColorMulti(Color32 color, byte speed, Bd15070_4IF.InitParam ____initParam)
    {
        if (!IsLedEnabledForPlayer(____initParam.index)) return;
        _devices[____initParam.index].SetButtonColor(-1, color);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), "_setColorMultiFade")]
    [EnableIf(nameof(IsAnyLedEnabled))]
    public static void Pre_setColorMultiFade(Color32 color, byte speed, Bd15070_4IF.InitParam ____initParam)
    {
        if (!IsLedEnabledForPlayer(____initParam.index)) return;
        _devices[____initParam.index].SetButtonColorFade(-1, color, GetByte2Msec(speed));
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), "_setColorMultiFet")]
    [EnableIf(nameof(IsAnyLedEnabled))]
    public static void Pre_setColorMultiFet(Color32 color, Bd15070_4IF.InitParam ____initParam)
    {
        if (!IsLedEnabledForPlayer(____initParam.index)) return;
        _devices[____initParam.index].SetBodyIntensity(8, color.r);
        _devices[____initParam.index].SetBodyIntensity(9, color.g);
        _devices[____initParam.index].SetBodyIntensity(10, color.b);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), "_setColorFet")]
    [EnableIf(nameof(IsAnyLedEnabled))]
    public static void Pre_setColorFet(byte ledPos, byte color, Bd15070_4IF.InitParam ____initParam)
    {
        if (!IsLedEnabledForPlayer(____initParam.index)) return;
        _devices[____initParam.index].SetBodyIntensity(ledPos, color);
    }


    [HarmonyPrefix]
    [HarmonyPatch(typeof(Bd15070_4IF), "_setLedAllOff")]
    [EnableIf(nameof(IsAnyLedEnabled))]
    public static void Pre_setLedAllOff(Bd15070_4IF.InitParam ____initParam)
    {
        if (!IsLedEnabledForPlayer(____initParam.index)) return;
        _devices[____initParam.index].SetBodyIntensity(-1, 0);
        _devices[____initParam.index].SetButtonColor(-1, new Color32(0, 0, 0, byte.MaxValue));
    }

    private static long GetByte2Msec(byte speed) => (long)Math.Round(4095.0 / speed * 8.0);
}
