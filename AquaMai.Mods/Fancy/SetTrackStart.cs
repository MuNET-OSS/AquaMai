using HarmonyLib;
using AquaMai.Config.Attributes;
using Process; 
using Manager;
using MAI2.Util;         
using Monitor;
using System.Reflection;
using UnityEngine;
using Mai2.Mai2Cue;
using MelonLoader;

namespace AquaMai.Mods.Fancy;

[ConfigSection(name: "开场动画PLUS（需配合转场动画使用）",
    en: "Set Track Start Animation",
    zh: "同步修改开场动画为其他变种")]
public class SetTrackStart
{
    private static SetFade.CommonFadeEntry GetActiveConfig() => 
        typeof(SetFade).GetField("_activeKldConfig", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as SetFade.CommonFadeEntry;

    private static bool _isModActive = false;
    private static float _timer = 0f;
    private const float MAX_ANIM_TIME = 4.5f; 

    // --- 修改：仅禁用 Active，不销毁对象，防止动画错位 ---
    private static void DisableSpecificUI(TrackStartMonitor monitor)
    {
        if (monitor == null) return;

        // 向上找到当前屏幕对应的根节点 TrackStartProcess(Clone)
        Transform root = monitor.transform;
        while (root.parent != null && !root.name.Contains("TrackStartProcess"))
            root = root.parent;

        // 目标 1: LifeGuage
        Transform lifeGuage = root.Find("Canvas/Main/Null_UI_Kaleid_TS/UI_Kaleid_TS/UI/LifeGuage");
        if (lifeGuage != null)
        {
            lifeGuage.gameObject.SetActive(false);
            MelonLogger.Msg("[V52] 已禁用 LifeGuage (SetActive: false)");
        }

        // 目标 2: TrackStart_head
        Transform head = root.Find("Canvas/Main/Null_UI_Kaleid_TS/UI_Kaleid_TS/UI/TrackStart_head");
        if (head != null)
        {
            head.gameObject.SetActive(false);
            MelonLogger.Msg("[V52] 已禁用 TrackStart_head (SetActive: false)");
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrackStartMonitor), "SetTrackStart", new[] { typeof(TrackStartMonitor.TrackStartType) })]
    public static bool SetTrackStartPrefix(TrackStartMonitor __instance, TrackStartMonitor.TrackStartType type)
    {
        if (GetActiveConfig() == null) return true;

        if (type == TrackStartMonitor.TrackStartType.Normal || type == TrackStartMonitor.TrackStartType.Versus)
        {
            var ctrlField = typeof(TrackStartMonitor).GetField("kaleidxScopeTrackStartController", BindingFlags.NonPublic | BindingFlags.Instance);
            var ctrl = ctrlField?.GetValue(__instance) as KaleidxScopeTrackStartController;

            if (ctrl != null)
            {
                _isModActive = true;
                _timer = 0f;

                // 初始隔离原版 UI
                string[] targetNames = { "_normalObject", "_versusObject", "_objNormal", "_objVersus", "_backGround" };
                foreach (var name in targetNames)
                {
                    var f = typeof(TrackStartMonitor).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                    (f?.GetValue(__instance) as GameObject)?.SetActive(false);
                }

                (typeof(TrackStartMonitor).GetField("_kaleidxScopeBackground", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(__instance) as GameObject)?.SetActive(true);
                ctrl.gameObject.SetActive(true);

                int gateId = 1;
                var config = GetActiveConfig();
                var gField = config.GetType().GetField("Type", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (gField != null) gateId = System.Convert.ToInt32(gField.GetValue(config));

                ctrl.PlayAnimation((KaleidxScopeTrackStartController.AnimState)(gateId % 11));
                ctrl.SetTrackNum(GameManager.MusicTrackNumber);
                
                // 内部数值依然设为0
                ctrl.SetLife(0);

                // --- 修改：调用禁用函数而非删除函数 ---
                DisableSpecificUI(__instance);

                SoundManager.PlaySE(Cue.SE_TRACK_START_KALEID, __instance.MonitorIndex);
                return false; 
            }
        }
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrackStartMonitor), "IsEnd")]
    public static bool IsEndPrefix(TrackStartMonitor __instance, ref bool __result)
    {
        if (!_isModActive) return true;
        _timer += Time.deltaTime;

        var ctrlField = typeof(TrackStartMonitor).GetField("kaleidxScopeTrackStartController", BindingFlags.NonPublic | BindingFlags.Instance);
        var ctrl = ctrlField?.GetValue(__instance) as KaleidxScopeTrackStartController;
        
        if (ctrl != null) 
        {
            if (ctrl.PlayEnded() || _timer >= MAX_ANIM_TIME)
            {
                __result = true;
            }
            else
            {
                __result = false;
            }
            return false;
        }
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TrackStartProcess), "OnUpdate")]
    public static bool ProcessUpdatePrefix(TrackStartProcess __instance)
    {
        if (!_isModActive) return true;

        var stateField = typeof(TrackStartProcess).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
        var currentState = (TrackStartProcess.TrackStartSequence)stateField.GetValue(__instance);

        if (currentState == TrackStartProcess.TrackStartSequence.DispEnd)
        {
            // 这里保持逻辑，动画结束后会自然销毁或释放进程
        }
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TrackStartProcess), "OnRelease")]
    public static void OnReleasePostfix() => _isModActive = false;
}
