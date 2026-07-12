using System;
using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using AquaMai.Core.Types;
using DB;
using HarmonyLib;
using Manager;
using Manager.UserDatas;
using MelonLoader;
using Monitor;
using Process;
using UnityEngine;
using static Monitor.SlideJudge;

namespace AquaMai.Mods.UX.JudgeDisplayPro;

[ConfigSection]
[ConfigCollapseNamespace]
public class JudgeDisplayPro
{
    // 有些地方有 4P
    public static UserSettings[] userSettings = [new UserSettings(), new UserSettings(), new UserSettings(), new UserSettings()];
    public static IPersistentStorage storage = new PlayerPrefsStorage();

    public static void OnBeforePatch()
    {
        GameSettingsManager.RegisterSetting(new OnOffSettingsEntry());
        GameSettingsManager.RegisterSetting(new CriticalSettingsEntry());
        GameSettingsManager.RegisterSetting(new NormalSettingsEntry(NormalSettingsType.Perfect));
        GameSettingsManager.RegisterSetting(new NormalSettingsEntry(NormalSettingsType.Great));
        GameSettingsManager.RegisterSetting(new NormalSettingsEntry(NormalSettingsType.Good));
    }

    // Touch 的 JudgeGrade（JudgeTouchGrade）不会被 SetLedSetting，_monitorIndex 一直是 -1
    // 由上一层 note 的 EndNote 在调用 Initialize 前把自己的 MonitorId 暂存到这里补上
    [ThreadStatic]
    private static int touchMonitorIndex;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TouchNoteB), "EndNote")]
    public static void PreTouchNoteBEndNote(NoteBase __instance)
    {
        touchMonitorIndex = __instance.MonitorId;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TouchHoldC), "EndNote")]
    public static void PreTouchHoldCEndNote(NoteBase __instance)
    {
        touchMonitorIndex = __instance.MonitorId;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(JudgeGrade), nameof(JudgeGrade.Initialize))]
    public static void PostJudgeGradeInitialize(JudgeGrade __instance, NoteJudge.ETiming judge, int ____monitorIndex, SpriteRenderer ___SpriteRender, SpriteRenderer ___SpriteRenderFastLate)
    {
        // Touch 的 monitor index 会是 -1，用上一层 note 暂存的补上
        if (____monitorIndex < 0) ____monitorIndex = touchMonitorIndex;
        if (____monitorIndex < 0) return;
        if (!userSettings[____monitorIndex].IsEnable) return;
        __instance.gameObject.SetActive(true);
        switch (judge)
        {
            case NoteJudge.ETiming.FastGood:
                switch (userSettings[____monitorIndex].GoodDisplayMode)
                {
                    case NormalDisplayMode.JudgeOnly:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeGood;
                        break;
                    case NormalDisplayMode.All:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeGood;
                        if (___SpriteRenderFastLate != null)
                        {
                            ___SpriteRenderFastLate.sprite = GameNoteImageContainer.JudgeFast;
                            ___SpriteRenderFastLate.gameObject.SetActive(true);
                        }
                        break;
                    case NormalDisplayMode.TimingOnly:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeFast;
                        break;
                    case NormalDisplayMode.ColoredJudge:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeFastGood;
                        break;
                    case NormalDisplayMode.None:
                        __instance.gameObject.SetActive(false);
                        break;
                }
                break;
            case NoteJudge.ETiming.LateGood:
                switch (userSettings[____monitorIndex].GoodDisplayMode)
                {
                    case NormalDisplayMode.JudgeOnly:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeGood;
                        break;
                    case NormalDisplayMode.All:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeGood;
                        if (___SpriteRenderFastLate != null)
                        {
                            ___SpriteRenderFastLate.sprite = GameNoteImageContainer.JudgeLate;
                            ___SpriteRenderFastLate.gameObject.SetActive(true);
                        }
                        break;
                    case NormalDisplayMode.TimingOnly:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeLate;
                        break;
                    case NormalDisplayMode.ColoredJudge:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeLateGood;
                        break;
                    case NormalDisplayMode.None:
                        __instance.gameObject.SetActive(false);
                        break;
                }
                break;
            case NoteJudge.ETiming.FastGreat3rd:
            case NoteJudge.ETiming.FastGreat2nd:
            case NoteJudge.ETiming.FastGreat:
                switch (userSettings[____monitorIndex].GreatDisplayMode)
                {
                    case NormalDisplayMode.JudgeOnly:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeGreat;
                        break;
                    case NormalDisplayMode.All:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeGreat;
                        if (___SpriteRenderFastLate != null)
                        {
                            ___SpriteRenderFastLate.sprite = GameNoteImageContainer.JudgeFast;
                            ___SpriteRenderFastLate.gameObject.SetActive(true);
                        }
                        break;
                    case NormalDisplayMode.TimingOnly:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeFast;
                        break;
                    case NormalDisplayMode.ColoredJudge:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeFastGreat;
                        break;
                    case NormalDisplayMode.None:
                        __instance.gameObject.SetActive(false);
                        break;
                }
                break;
            case NoteJudge.ETiming.LateGreat3rd:
            case NoteJudge.ETiming.LateGreat2nd:
            case NoteJudge.ETiming.LateGreat:
                switch (userSettings[____monitorIndex].GreatDisplayMode)
                {
                    case NormalDisplayMode.JudgeOnly:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeGreat;
                        break;
                    case NormalDisplayMode.All:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeGreat;
                        if (___SpriteRenderFastLate != null)
                        {
                            ___SpriteRenderFastLate.sprite = GameNoteImageContainer.JudgeLate;
                            ___SpriteRenderFastLate.gameObject.SetActive(true);
                        }
                        break;
                    case NormalDisplayMode.TimingOnly:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeLate;
                        break;
                    case NormalDisplayMode.ColoredJudge:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeLateGreat;
                        break;
                    case NormalDisplayMode.None:
                        __instance.gameObject.SetActive(false);
                        break;
                }
                break;
            case NoteJudge.ETiming.FastPerfect2nd:
            case NoteJudge.ETiming.FastPerfect:
                switch (userSettings[____monitorIndex].PerfectDisplayMode)
                {
                    case NormalDisplayMode.JudgeOnly:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgePerfect;
                        break;
                    case NormalDisplayMode.All:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgePerfect;
                        if (___SpriteRenderFastLate != null)
                        {
                            ___SpriteRenderFastLate.sprite = GameNoteImageContainer.JudgeFast;
                            ___SpriteRenderFastLate.gameObject.SetActive(true);
                        }
                        break;
                    case NormalDisplayMode.TimingOnly:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeFast;
                        break;
                    case NormalDisplayMode.ColoredJudge:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeFastPerfect;
                        break;
                    case NormalDisplayMode.None:
                        __instance.gameObject.SetActive(false);
                        break;
                }
                break;
            case NoteJudge.ETiming.LatePerfect2nd:
            case NoteJudge.ETiming.LatePerfect:
                switch (userSettings[____monitorIndex].PerfectDisplayMode)
                {
                    case NormalDisplayMode.JudgeOnly:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgePerfect;
                        break;
                    case NormalDisplayMode.All:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgePerfect;
                        if (___SpriteRenderFastLate != null)
                        {
                            ___SpriteRenderFastLate.sprite = GameNoteImageContainer.JudgeLate;
                            ___SpriteRenderFastLate.gameObject.SetActive(true);
                        }
                        break;
                    case NormalDisplayMode.TimingOnly:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeLate;
                        break;
                    case NormalDisplayMode.ColoredJudge:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeLatePerfect;
                        break;
                    case NormalDisplayMode.None:
                        __instance.gameObject.SetActive(false);
                        break;
                }
                break;
            case NoteJudge.ETiming.Critical:
                // 这里处理的是绝赞以外的，绝赞的在下一个方法里面处理
                switch (userSettings[____monitorIndex].CriticalDisplayMode)
                {
                    case CriticalDisplayMode.None:
                    case CriticalDisplayMode.OnBreak:
                    case CriticalDisplayMode.OffBreak:
                        switch (userSettings[____monitorIndex].PerfectDisplayMode)
                        {
                            case NormalDisplayMode.JudgeOnly:
                            case NormalDisplayMode.All:
                                ___SpriteRender.sprite = GameNoteImageContainer.JudgePerfect;
                                break;
                            case NormalDisplayMode.TimingOnly:
                            case NormalDisplayMode.ColoredJudge:
                            case NormalDisplayMode.None:
                                __instance.gameObject.SetActive(false);
                                break;
                        }
                        break;
                    case CriticalDisplayMode.OnAll:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeCritical;
                        break;
                    case CriticalDisplayMode.OnAllShowBreak:
                    case CriticalDisplayMode.OffAll:
                        __instance.gameObject.SetActive(false);
                        break;
                }
                break;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(JudgeGrade), nameof(JudgeGrade.InitializeBreak))]
    public static void PostJudgeGradeInitializeBreak(JudgeGrade __instance, NoteJudge.ETiming judge, int ____monitorIndex, SpriteRenderer ___SpriteRender, SpriteRenderer ___SpriteRenderAdd)
    {
        if (!userSettings[____monitorIndex].IsEnable) return;
        if (judge != NoteJudge.ETiming.Critical) return;
        switch (userSettings[____monitorIndex].CriticalDisplayMode)
        {
            case CriticalDisplayMode.None:
                switch (userSettings[____monitorIndex].PerfectDisplayMode)
                {
                    case NormalDisplayMode.JudgeOnly:
                    case NormalDisplayMode.All:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgePerfect;
                        ___SpriteRenderAdd.sprite = GameNoteImageContainer.JudgePerfectBreak;
                        break;
                    case NormalDisplayMode.TimingOnly:
                    case NormalDisplayMode.ColoredJudge:
                    case NormalDisplayMode.None:
                        __instance.gameObject.SetActive(false);
                        ___SpriteRenderAdd.gameObject.SetActive(false);
                        break;
                }
                break;
            case CriticalDisplayMode.OnBreak:
            case CriticalDisplayMode.OnAll:
            case CriticalDisplayMode.OnAllShowBreak:
                ___SpriteRender.sprite = GameNoteImageContainer.JudgeCritical;
                ___SpriteRenderAdd.sprite = GameNoteImageContainer.JudgeCriticalBreak;
                __instance.gameObject.SetActive(true);
                ___SpriteRenderAdd.gameObject.SetActive(true);
                break;
            case CriticalDisplayMode.OffBreak:
            case CriticalDisplayMode.OffAll:
                __instance.gameObject.SetActive(false);
                ___SpriteRenderAdd.gameObject.SetActive(false);
                break;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SlideJudge), nameof(SlideJudge.Initialize))]
    public static void PostSlideJudgeInitialize(SlideJudge __instance, bool isBreak, NoteJudge.ETiming judge, int ____monitorIndex, SpriteRenderer ___SpriteRender, SlideJudgeType ____judgeType, SlideAngle ____angle, SpriteRenderer ___SpriteRenderAdd)
    {
        if (!userSettings[____monitorIndex].IsEnable) return;
        switch (judge)
        {
            case NoteJudge.ETiming.FastGood:
                switch (userSettings[____monitorIndex].GoodDisplayMode)
                {
                    case NormalDisplayMode.JudgeOnly:
                    case NormalDisplayMode.All:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeSlideFastGood[(int)____judgeType, (int)____angle];
                        break;
                    case NormalDisplayMode.TimingOnly:
                    case NormalDisplayMode.ColoredJudge:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeSlideFastGoodCol[(int)____judgeType, (int)____angle];
                        break;
                    case NormalDisplayMode.None:
                        __instance.gameObject.SetActive(false);
                        break;
                }
                break;
            case NoteJudge.ETiming.LateGood:
                switch (userSettings[____monitorIndex].GoodDisplayMode)
                {
                    case NormalDisplayMode.JudgeOnly:
                    case NormalDisplayMode.All:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeSlideLateGood[(int)____judgeType, (int)____angle];
                        break;
                    case NormalDisplayMode.TimingOnly:
                    case NormalDisplayMode.ColoredJudge:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeSlideLateGoodCol[(int)____judgeType, (int)____angle];
                        break;
                    case NormalDisplayMode.None:
                        __instance.gameObject.SetActive(false);
                        break;
                }
                break;
            case NoteJudge.ETiming.FastGreat3rd:
            case NoteJudge.ETiming.FastGreat2nd:
            case NoteJudge.ETiming.FastGreat:
                switch (userSettings[____monitorIndex].GreatDisplayMode)
                {
                    case NormalDisplayMode.JudgeOnly:
                    case NormalDisplayMode.All:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeSlideFastGreat[(int)____judgeType, (int)____angle];
                        break;
                    case NormalDisplayMode.TimingOnly:
                    case NormalDisplayMode.ColoredJudge:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeSlideFastGreatCol[(int)____judgeType, (int)____angle];
                        break;
                    case NormalDisplayMode.None:
                        __instance.gameObject.SetActive(false);
                        break;
                }
                break;
            case NoteJudge.ETiming.LateGreat3rd:
            case NoteJudge.ETiming.LateGreat2nd:
            case NoteJudge.ETiming.LateGreat:
                switch (userSettings[____monitorIndex].GreatDisplayMode)
                {
                    case NormalDisplayMode.JudgeOnly:
                    case NormalDisplayMode.All:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeSlideLateGreat[(int)____judgeType, (int)____angle];
                        break;
                    case NormalDisplayMode.TimingOnly:
                    case NormalDisplayMode.ColoredJudge:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeSlideLateGreatCol[(int)____judgeType, (int)____angle];
                        break;
                    case NormalDisplayMode.None:
                        __instance.gameObject.SetActive(false);
                        break;
                }
                break;
            case NoteJudge.ETiming.FastPerfect2nd:
            case NoteJudge.ETiming.FastPerfect:
                switch (userSettings[____monitorIndex].PerfectDisplayMode)
                {
                    case NormalDisplayMode.JudgeOnly:
                    case NormalDisplayMode.All:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeSlideFastPerfect[(int)____judgeType, (int)____angle];
                        break;
                    case NormalDisplayMode.TimingOnly:
                    case NormalDisplayMode.ColoredJudge:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeSlideFastPerfectCol[(int)____judgeType, (int)____angle];
                        break;
                    case NormalDisplayMode.None:
                        __instance.gameObject.SetActive(false);
                        break;
                }
                break;
            case NoteJudge.ETiming.LatePerfect2nd:
            case NoteJudge.ETiming.LatePerfect:
                switch (userSettings[____monitorIndex].PerfectDisplayMode)
                {
                    case NormalDisplayMode.JudgeOnly:
                    case NormalDisplayMode.All:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeSlideLatePerfect[(int)____judgeType, (int)____angle];
                        break;
                    case NormalDisplayMode.TimingOnly:
                    case NormalDisplayMode.ColoredJudge:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeSlideLatePerfectCol[(int)____judgeType, (int)____angle];
                        break;
                    case NormalDisplayMode.None:
                        __instance.gameObject.SetActive(false);
                        break;
                }
                break;
            case NoteJudge.ETiming.Critical:
                switch (userSettings[____monitorIndex].CriticalDisplayMode)
                {
                    case CriticalDisplayMode.None:
                        switch (userSettings[____monitorIndex].PerfectDisplayMode)
                        {
                            case NormalDisplayMode.JudgeOnly:
                            case NormalDisplayMode.All:
                                ___SpriteRender.sprite = GameNoteImageContainer.JudgeSlidePerfect[(int)____judgeType, (int)____angle];
                                ___SpriteRenderAdd.sprite = GameNoteImageContainer.JudgeSlidePerfectBreak[(int)____judgeType, (int)____angle];
                                if (isBreak)
                                {
                                    ___SpriteRenderAdd.gameObject.SetActive(true);
                                }
                                break;
                            case NormalDisplayMode.TimingOnly:
                            case NormalDisplayMode.ColoredJudge:
                            case NormalDisplayMode.None:
                                __instance.gameObject.SetActive(false);
                                break;
                        }
                        break;
                    case CriticalDisplayMode.OnBreak:
                    case CriticalDisplayMode.OnAll:
                    case CriticalDisplayMode.OnAllShowBreak:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeSlideCritical[(int)____judgeType, (int)____angle];
                        ___SpriteRenderAdd.sprite = GameNoteImageContainer.JudgeSlideCriticalBreak[(int)____judgeType, (int)____angle];
                        if (isBreak)
                        {
                            ___SpriteRenderAdd.gameObject.SetActive(true);
                        }
                        break;
                    case CriticalDisplayMode.OffBreak:
                    case CriticalDisplayMode.OffAll:
                        __instance.gameObject.SetActive(false);
                        ___SpriteRenderAdd.gameObject.SetActive(false);
                        break;
                }
                break;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameScoreList), nameof(GameScoreList.Initialize))]
    public static void PostGameScoreListInitialize(int monitorIndex, UserOption ___UserOption)
    {
        if (!userSettings[monitorIndex].IsEnable) return;
        switch (userSettings[monitorIndex].CriticalDisplayMode)
        {
            case CriticalDisplayMode.None:
                ___UserOption.DispJudge = OptionDispjudgeID.Type1A;
                break;
            case CriticalDisplayMode.OnBreak:
            case CriticalDisplayMode.OffBreak:
                ___UserOption.DispJudge = OptionDispjudgeID.Type1E;
                break;
            case CriticalDisplayMode.OnAll:
            case CriticalDisplayMode.OnAllShowBreak:
            case CriticalDisplayMode.OffAll:
                ___UserOption.DispJudge = OptionDispjudgeID.Type3B;
                break;
        }
    }

}
