using System;
using HarmonyLib;
using Monitor;
using Process;
using UnityEngine;

namespace AquaMai.Mods.UX.JudgeDisplayPro;

public partial class JudgeDisplayPro
{
    // Touch 的 JudgeGrade（JudgeTouchGrade）不会被 SetLedSetting，_monitorIndex 一直是 -1
    // 由上一层 note 的 EndNote 在调用 Initialize 前把自己的 MonitorId 暂存到这里补上
    [ThreadStatic]
    private static int? touchMonitorIndex;

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
    public static void PostJudgeGradeInitialize(JudgeGrade __instance, NoteJudge.ETiming judge, int ____monitorIndex, int ____dispPos, SpriteRenderer ___SpriteRender, SpriteRenderer ___SpriteRenderFastLate)
    {
        // Touch 的 monitor index 会是 -1，用上一层 note 暂存的补上
        var monitorIndex = ____monitorIndex;
        if (monitorIndex < 0)
        {
            monitorIndex = touchMonitorIndex ?? -1;
            touchMonitorIndex = null;
        }
        if ((uint)monitorIndex >= userSettings.Length) return;
        if (!userSettings[monitorIndex].IsEnable) return;
        if (____dispPos == 0)
        {
            __instance.gameObject.SetActive(false);
            return;
        }
        __instance.gameObject.SetActive(true);
        if (___SpriteRenderFastLate != null) ___SpriteRenderFastLate.gameObject.SetActive(false);
        switch (judge)
        {
            case NoteJudge.ETiming.FastGood:
                switch (userSettings[monitorIndex].GoodDisplayMode)
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
                switch (userSettings[monitorIndex].GoodDisplayMode)
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
                switch (userSettings[monitorIndex].GreatDisplayMode)
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
                switch (userSettings[monitorIndex].GreatDisplayMode)
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
                switch (userSettings[monitorIndex].PerfectDisplayMode)
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
                switch (userSettings[monitorIndex].PerfectDisplayMode)
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
                switch (userSettings[monitorIndex].CriticalDisplayMode)
                {
                    case CriticalDisplayMode.None:
                    case CriticalDisplayMode.OnBreak:
                    case CriticalDisplayMode.OffBreak:
                        switch (userSettings[monitorIndex].PerfectDisplayMode)
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
    public static void PostJudgeGradeInitializeBreak(JudgeGrade __instance, NoteJudge.ETiming judge, int ____monitorIndex, int ____dispPos, SpriteRenderer ___SpriteRender, SpriteRenderer ___SpriteRenderAdd)
    {
        if ((uint)____monitorIndex >= userSettings.Length) return;
        if (!userSettings[____monitorIndex].IsEnable) return;
        if (____dispPos == 0)
        {
            __instance.gameObject.SetActive(false);
            ___SpriteRenderAdd.gameObject.SetActive(false);
            return;
        }
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
}
