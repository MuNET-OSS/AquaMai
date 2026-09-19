using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AquaMai.Core;
using AquaMai.Core.Attributes;
using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using AquaMai.Mods.Fancy.GamePlay;
using AquaMai.Mods.Utils;
using HarmonyLib;
using MAI2.Util;
using Manager;
using MelonLoader;
using Monitor;
using Monitor.Common;
using Monitor.Entry;
using Monitor.Entry.Parts.Screens;
using UnityEngine;
using Fx;
using Process;

namespace AquaMai.Mods.GameSystem;

// Hides the 2p (right hand side) UI.
// Note: this is not my original work. I simply interpreted the code and rewrote it as a mod.
[ConfigSection(
    name: "单人模式",
    en: """
        Single player: Show 1P only, at the center of the screen.
        """,
    zh: """
        单人模式，不显示 2P
        """)]
public partial class SinglePlayer
{
    [ConfigEntry(
        name: "隐藏副屏",
        en: "Only show the main area, without the sub-monitor.",
        zh: "只显示主区域，不显示副屏")]
    public static bool HideSubMonitor = false;

    [HarmonyPatch]
    public class WhateverInitialize
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var lateInitialize = AccessTools.Method(typeof(Main.GameMain), "LateInitialize", [typeof(MonoBehaviour), typeof(Transform), typeof(Transform)]);
            if (lateInitialize is not null) return [lateInitialize];
            return [AccessTools.Method(typeof(Main.GameMain), "Initialize", [typeof(MonoBehaviour), typeof(Transform), typeof(Transform)])];
        }

        public static void Prefix(MonoBehaviour gameMainObject, ref Transform left, ref Transform right)
        {
            if (ConfigLoader.Config.GetSectionState(typeof(ScreenPositionAdjust)).Enabled) return;
            Vector3 position = Camera.main.gameObject.transform.position;
            var yOffset = 0f;
            if (HideSubMonitor)
            {
                yOffset = -420f;
                Camera.main.orthographicSize = 540f;
            }
            Camera.main.gameObject.transform.position = new Vector3(position.x - 540f, position.y + yOffset, position.z);
            right.localScale = Vector3.zero;
        }
    }

    [ConfigEntry(
        name: "自动跳过倒计时",
        en: "Automatically skip the countdown when logging in with a card in single-player mode.",
        zh: "单人模式下刷卡登录直接进入下一个界面，无需跳过倒计时")]
    public static bool autoSkip = false;

    [EnableGameVersion(23000, noWarn: true)]
    [EnableIf(nameof(autoSkip))]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(EntryMonitor), "DecideEntry")]
    public static void PostDecideEntry(EntryMonitor __instance)
    {
# if DEBUG
        MelonLogger.Msg("Confirm Entry");
# endif
        TimeManager.MarkGameStartTime();
        Singleton<EventManager>.Instance.UpdateEvent();
        Singleton<ScoreRankingManager>.Instance.UpdateData();
        SharedInstances.GameMainObject.StartCoroutine(LaterDisableCardReader());
        __instance.Process.CreateDownloadProcess();
        __instance.ProcessManager.SendMessage(new Message(ProcessType.CommonProcess, 30001));
        __instance.ProcessManager.SendMessage(new Message(ProcessType.CommonProcess, 40000, 0, OperationInformationController.InformationType.Hide));
        __instance.Process.SetNextProcess();
    }

    private static IEnumerator LaterDisableCardReader()
    {
        yield return new WaitForSeconds(3);
        SingletonStateMachine<AmManager, AmManager.EState>.Instance.AimeReader.EnableRead(flag: false);
    }

    // To prevent the "長押受付終了" overlay from appearing
    [EnableGameVersion(21500, noWarn: true)]
    [EnableIf(nameof(autoSkip))]
    [HarmonyPrefix]
    [HarmonyPatch(typeof(WaitPartner), "Open")]
    public static bool WaitPartnerPreOpen()
    {
        return false;
    }

    [ConfigEntry(
        name: "修复烟花效果",
        en: "Fix hanabi effect under single-player mode (disabled automatically if HideHanabi is enabled).",
        zh: "修复单人模式下的烟花效果（如果启用了 HideHanabi，则会自动禁用）")]
    public static bool fixHanabi = true;

    private static bool fixHanabiDisableImplied = false;
    private static bool FixHanabiEnabled => fixHanabi && !fixHanabiDisableImplied;

    [EnableIf(nameof(FixHanabiEnabled))]
    [HarmonyPatch(typeof(TapCEffect), "SetUpParticle")]
    [HarmonyPostfix]
    public static void PostSetUpParticle(TapCEffect __instance, FX_Mai2_Note_Color ____particleControler)
    {
        var entities = ____particleControler.GetComponentsInChildren<ParticleSystemRenderer>(true);
        foreach (var entity in entities)
        {
            entity.maxParticleSize = 1f;
        }
    }

    public static void OnBeforePatch()
    {
        if (ConfigLoader.Config.GetSectionState(typeof(HideHanabi)).Enabled)
        {
            fixHanabiDisableImplied = true;
        }
    }

    public static void OnAfterPatch()
    {
        Core.Helpers.GuiSizes.SinglePlayer = true;
    }

    [ConfigEntry(
        name: "显示自由模式计时",
        en: "Show the timer in free mode on the sub-monitor.",
        zh: "使用自由模式时，在副屏显示计时器")]
    public static bool showFreedomModeTimer = true;

    [EnableIf(nameof(showFreedomModeTimer))]
    [HarmonyPatch(typeof(PleaseWaitProcess), "OnStart")]
    [HarmonyPostfix]
    public static void FreedomModeTimerHook(PleaseWaitMonitor[] ____monitors)
    {
        var target = ____monitors[0].transform.Find("Canvas/Sub");
        var timer10 = ____monitors[1].transform.Find("Canvas/Main/FreedomMode/UI_Timer10");
        var timer1 = ____monitors[1].transform.Find("Canvas/Main/FreedomMode/UI_Timer1");

        target.gameObject.SetActive(true);
        var go = new GameObject("[AquaMai] 自由模式计时器");
        go.transform.localScale = Vector3.one * 0.4f;
        go.transform.position = new Vector3(390, 50, 0);
        go.transform.SetParent(target, false);
        timer1.transform.SetParent(go.transform, false);
        timer10.transform.SetParent(go.transform, false);
    }

    [EnableIf(nameof(showFreedomModeTimer))]
    [HarmonyPatch(typeof(PleaseWaitMonitor), "Initialize")]
    [HarmonyPostfix]
    public static void PleaseWaitMonitorInitialize(PleaseWaitMonitor.SpriteTimer ____time1, PleaseWaitMonitor.SpriteTimer ____time10)
    {
        ____time1.SetVisible(false);
        ____time10.SetVisible(false);
    }

    [EnableGameVersion(27000, noWarn: true)]
    [HarmonyPatch("ScreenClipSetter", "SetClipX")]
    [HarmonyPrefix]
    public static void ScreenClipSetterSetClipX(ref float minX, ref float maxX)
    {
        if (maxX <= 0.5f)
        {
            minX = 0f;
            maxX = 1f;
        }
    }
}