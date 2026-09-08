using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace AquaMai.Core.Helpers;

/// <summary>
/// 解决 Harmony + Mono JIT「内联导致 patch 失效」问题的工具。
/// </summary>
/// <remarks>
/// <para><b>背景（研究结论）</b></para>
/// <para>
/// 在 Unity Mono 环境下，若<b>外层（caller）方法</b>在 JIT 编译时，其调用的<b>内层（callee）方法</b>尚未被 Harmony patch，
/// JIT 可能将内层调用内联进外层。
/// 这样即便之后 patch了内层方法，但外层已编译好的函数仍然会调用老的未patch版本的内层函数，从而表现为「某些 patch 不生效 / 钩子像没打上一样」。
/// 参见：https://github.com/BepInEx/HarmonyX/issues/71#issuecomment-1495402151
/// </para>
/// <para>
/// 已知案例：
/// <list type="bullet">
/// <item>若<c>GameMainObject.Update</c> 先于 <c>DebugInput.GetKeyDown</c> 被patch，导致ESC按键无法退出游戏 (https://github.com/MuNET-OSS/AquaMai/pull/143)</item>
/// <item>若<c>GameMain.Update</c> 先于 <c>InputManager.GetSystemInputDown</c> 被patch，导致在mml官方IO Mod下，TestProof功能不生效 (https://github.com/MuNET-OSS/AquaMai/blob/983f887be48d6c5c85a81460fa3c45d2d35c3852/AquaMai.Mods/GameSystem/TestProof.cs#L78-L88)</item>
/// <item>若<c>MouseTouchPanel.Start</c> 先于 <c>InputManager.RegisterMouseTouchPanel</c> 被patch，导致DisplayTouchInGame异常 (https://github.com/MuNET-OSS/AquaMai/pull/55)</item>
/// </list>
/// </para>
/// <para><b>原理和用法</b></para>
/// <para>
/// 若出现上述情况时，则手动确保在内层函数patch之后，对外层函数做一次「临时 patch → 立即 unpatch」，这样可强制 Mono 重新编译外层，
/// 此时内层已处于 patched 状态，JIT 不会再内联旧实现，调用链恢复正常。
/// </para>
/// <para>
/// 推荐的具体做法是，在内层函数所对应Mod类的 onAfterPatch 方法中，对外层函数调用一下本Helper提供的RecompileMethod 即可。
/// 参考实现见 AquaMai.Mods/Utils/DisplayTouchInGame.cs 中的代码。
/// </para>
/// </remarks>
public static class HarmonyPatchRecompile
{
    /// <summary>
    /// 对指定方法执行一次 patch → unpatch 操作，从而强制 Mono 重新编译该方法。
    /// </summary>
    /// <param name="method">需要触发重新编译的方法的MethodBase对象</param>
    public static void RecompileMethod(MethodBase method)
    {
        try
        {
            harmony.Patch(method, prefix: DummyPrefix);
            harmony.Unpatch(method, HarmonyPatchType.Prefix, harmony.Id);
        }
        catch (Exception ex)
        {
            MelonLogger.Warning(
                $"重编译 {method?.DeclaringType?.FullName}.{method?.Name} 方法失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 对指定方法执行一次 patch → unpatch 操作，从而强制 Mono 重新编译该方法。
    /// </summary>
    /// <param name="type">和 methodName、argumentTypes 参数一起，指定需要触发重新编译的方法</param>
    public static void RecompileMethod(Type type, string methodName, Type[] argumentTypes = null)
        => RecompileMethod(AccessTools.Method(type, methodName, argumentTypes));

    private static void DummyPrefixImpl()
    {
        // 仅在 RecompileMethod 中被用作那个先patch再立刻unpatch的临时函数，不会残留在运行时。
    }
    private static readonly HarmonyMethod DummyPrefix = new(typeof(HarmonyPatchRecompile), nameof(DummyPrefixImpl));
    
    // 专用于本类中的临时 patch → unpatch操作，与 AquaMai的主 Harmony 实例隔离，避免 unpatch 误删业务 prefix
    private static readonly HarmonyLib.Harmony harmony = new("AquaMai.Core.Helpers.HarmonyPatchRecompile");
}
