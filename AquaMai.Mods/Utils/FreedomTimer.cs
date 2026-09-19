using System;
using AquaMai.Config.Attributes;
using AquaMai.Config.Types;
using AquaMai.Core.Attributes;
using AquaMai.Core.Helpers;
using HarmonyLib;
using Main;
using Manager;
using MelonLoader;
using Monitor;
using Process;
using UnityEngine;

namespace AquaMai.Mods.Utils;

[ConfigSection("自由模式时间修改", "Freedom Timer Modification")]
public static class FreedomTimer
{
    [ConfigEntry("秒数")]
    public static long seconds = 600;

    [ConfigEntry("无限时间")]
    public static bool infinityTime = false;

    [ConfigEntry(
        name: "追加时间按键",
        en: "Key to add time in Freedom Mode. Set to None to disable.",
        zh: "可通过指定按键，随时为自由模式追加时间。设为 None 则禁用")]
    public static readonly KeyCodeOrName addTimeKey = KeyCodeOrName.None;

    [ConfigEntry(name: "追加时间长按", en: "Require long press to add time", zh: "上述“追加时间按键”是否需要长按")]
    public static readonly bool addTimeLongPress = false;

    [ConfigEntry(
        name: "每次追加秒数",
        en: "Seconds added each time the key is pressed",
        zh: "每次按上述“追加时间按键”，所追加的秒数")]
    public static readonly long addTimeSeconds = 120;
    
    private static bool IsAddTimeEnabled => addTimeKey != KeyCodeOrName.None;
    private static bool _pendingUiRefresh;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.GetFreedomStartTime))]
    public static bool GetFreedomStartTime(ref long __result)
    {
        __result = seconds * 1000;
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.IsFreedomTimerPause), MethodType.Getter)]
    [EnableIf(nameof(infinityTime))]
    public static void IsFreedomTimerPause(ref bool __result)
    {
        __result = true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameMainObject), "Update")]
    [EnableIf(nameof(IsAddTimeEnabled))]
    public static void OnGameMainObjectUpdate()
    {
        if (!GameManager.IsFreedomMode) return;
        if (!KeyListener.GetKeyDownOrLongPress(addTimeKey, addTimeLongPress)) return;
        if (addTimeSeconds <= 0) return;

        // 设置GameManager._freedomTime（私有属性）
        var FreedomTimeField = AccessTools.Field(typeof(GameManager), "_freedomTime");
        var current = (long)FreedomTimeField.GetValue(null);
        if (current < 0) current = 0;
        FreedomTimeField.SetValue(null, current + addTimeSeconds * 1000L);

        // 时间归零会停止倒计时；重新启动以便追加的时间能继续倒数。
        if (!GameManager.IsFreedomCountDown) AccessTools.Property(typeof(GameManager), "IsFreedomCountDown").SetValue(null, true); // private set，所以需要反射
        GameManager.IsFreedomTimeUp = false;
        _pendingUiRefresh = true;
        MelonLogger.Msg($"[FreedomTimer] 已将自由模式的时间增加{addTimeSeconds}秒");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PleaseWaitProcess), "OnUpdate")]
    [EnableIf(nameof(IsAddTimeEnabled))]
    public static void ResetFreedomTimerState(PleaseWaitProcess __instance)
    {
        if (!_pendingUiRefresh) return;
        _pendingUiRefresh = false;
        
        // PleaseWaitProcess 私有枚举的底层数值
        const byte FreedomModeStateCountDown = 1;
        const byte FreedomModeStateTimeUp = 2;
        const int RemainingStateNormal = 600;
        const int RemainingStateOneMinute = 60;
        const int RemainingStateTenSecond = 11;

        var traverse = Traverse.Create(__instance);
        var state = traverse.Field("_freedomModeState");
        var wasTimeUp = Convert.ToByte(state.GetValue()) == FreedomModeStateTimeUp;
        var totalSeconds = GameManager.GetFreedomModeMSec() * 0.001;

        // 退出 TimeUp，让本帧原版 OnUpdate 重新走倒计时与 SetTime
        state.SetValue(Enum.ToObject(state.GetValueType(), FreedomModeStateCountDown));
        var remaining = traverse.Field("_remaining");
        var remainingState = totalSeconds > 60.0 ? RemainingStateNormal :
            totalSeconds > 11.0 ? RemainingStateOneMinute : RemainingStateTenSecond;
        remaining.SetValue(Enum.ToObject(remaining.GetValueType(), remainingState));
        traverse.Field("_beforeSeconds").SetValue(-1);
        traverse.Field("_beforeMinutes").SetValue(-1);

        foreach (var monitor in traverse.Field("_monitors").GetValue<PleaseWaitMonitor[]>())
        {
            if (monitor == null || !monitor.IsVisibleFreedomMode()) continue;
            monitor.SetOneMinute(totalSeconds <= 60.0);

            // TimeUp 动画/协程会挡住计时器，清掉后恢复正常外观
            if (!wasTimeUp) continue;
            monitor.StopAllCoroutines();
            var animator = Traverse.Create(monitor).Field("_freedomModeAnimator").GetValue<Animator>();
            if (animator == null) continue;
            animator.enabled = true;
            animator.Play(Animator.StringToHash("In"), 0, 1f);
            animator.Update(0f);
            animator.enabled = false;
        }
    }
}
