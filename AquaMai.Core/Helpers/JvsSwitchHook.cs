using System.Collections.Generic;
using System.Reflection;
using AMDaemon;
using UnityEngine;
using HarmonyLib;

namespace AquaMai.Core.Helpers;

public struct AuxiliaryState
{
    public bool select1P;
    public bool select2P;
    public bool service;
    public bool test;
}

public struct CustomFnState
{
    public bool CustomFn1;
    public bool CustomFn2;
    public bool CustomFn3;
    public bool CustomFn4;
}
public static class JvsSwitchHook
{
    public delegate bool ButtonChecker(int playerNo, int buttonIndex1To8);

    public delegate AuxiliaryState AuxiliaryStateProvider();
    public delegate CustomFnState CustomFnStateProvider();

    private static readonly List<ButtonChecker> _buttonCheckers = [];
    private static readonly List<AuxiliaryStateProvider> _auxiliaryStateProviders = [];
    private static readonly List<CustomFnStateProvider> _customFnStateProviders = [];

    public static void RegisterButtonChecker(ButtonChecker buttonChecker)
    {
        EnsurePatched();
        _buttonCheckers.Add(buttonChecker);
    }
    public static void RegisterAuxiliaryStateProvider(AuxiliaryStateProvider auxiliaryStateProvider)
    {
        EnsurePatched();
        _auxiliaryStateProviders.Add(auxiliaryStateProvider);
    }
    public static void RegisterCustomFnStateProvider(CustomFnStateProvider customFnStateProvider)
    {
        EnsurePatched();
        _customFnStateProviders.Add(customFnStateProvider);
    }

    private static bool isPatched = false;
    private static bool EnsurePatched()
    {
        if (isPatched) return false;
        isPatched = true;
        Startup.ApplyPatch(typeof(Hook));
        return true;
    }

    private static bool IsInputPushed(int playerNo, InputId inputId)
    {
        if (inputId.Value.StartsWith("button_"))
        {
            int buttonIndex = int.Parse(inputId.Value.Substring("button_0".Length));
            foreach (var checker in _buttonCheckers)
                if (checker(playerNo, buttonIndex))
                    return true;
            return false;
        }
        else
        {
            bool wantSelect1P = inputId.Value == "select" && playerNo == 0;
            bool wantSelect2P = inputId.Value == "select" && playerNo == 1;
            bool wantService = inputId.Value == "service";
            bool wantTest = inputId.Value == "test";
            foreach (var provider in _auxiliaryStateProviders)
            {
                var auxiliaryState = provider();
                if (wantSelect1P && auxiliaryState.select1P ||
                    wantSelect2P && auxiliaryState.select2P ||
                    wantService && auxiliaryState.service ||
                    wantTest && auxiliaryState.test) return true;
            }
            return false;
        }
    }

    public static bool[] GetCustomFnState() // 返回的数组长度必定为4，对应FN1到FN4
    {
        var result = new bool[4];
        foreach (var provider in _customFnStateProviders)
        {
            var state = provider();
            result[0] |= state.CustomFn1;
            result[1] |= state.CustomFn2;
            result[2] |= state.CustomFn3;
            result[3] |= state.CustomFn4;
        }
        return result;
    }

    [HarmonyPatch]
    public static class Hook
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var jvsSwitch = typeof(IO.Jvs).GetNestedType("JvsSwitch", BindingFlags.NonPublic | BindingFlags.Public);
            return [jvsSwitch.GetMethod("Execute")];
        }

        public static bool Prefix(
            int ____playerNo,
            InputId ____inputId,
            KeyCode ____subKey,
            SwitchInput ____switchInput,
            ref bool ____invert,
            ref bool ____isStateOnOld,
            ref bool ____isStateOnOld2,
            ref bool ____isStateOn,
            ref bool ____isTriggerOn,
            ref bool ____isTriggerOff)
        {
            ____isStateOnOld2 = ____isStateOnOld;
            ____isStateOnOld = ____isStateOn;

            bool flag = false;
            flag |= IsInputPushed(____playerNo, ____inputId);
            flag |= DebugInput.GetKey(____subKey);
            flag |= ____invert
                ? (!____switchInput.IsOn || ____switchInput.HasOffNow)
                : (____switchInput.IsOn || ____switchInput.HasOnNow);

            if (____isStateOnOld2 && !____isStateOnOld && flag)
            {
                flag = false;
            }
            ____isStateOn = flag;
            ____isTriggerOn = flag && (flag ^ ____isStateOnOld);
            ____isTriggerOff = !flag && (flag ^ ____isStateOnOld);
            return false;
        }
    }
}