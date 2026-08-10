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
        var settings = userSettings[monitorIndex];
        if (judge == NoteJudge.ETiming.Critical)
        {
            ApplyCriticalJudgeGradeDisplay(__instance, settings, false, ___SpriteRender, null);
            return;
        }

        if (!TryGetNormalJudgeSprites(judge, out var judgeSprite, out var coloredSprite)) return;
        ApplyNormalJudgeGradeDisplay(
            __instance,
            Logic.GetNormalDisplayMode(settings, judge, false),
            Logic.IsFastTiming(judge),
            judgeSprite,
            coloredSprite,
            ___SpriteRender,
            ___SpriteRenderFastLate);
    }

    private static bool TryGetNormalJudgeSprites(NoteJudge.ETiming timing, out Sprite judgeSprite, out Sprite coloredSprite)
    {
        switch (timing)
        {
            case NoteJudge.ETiming.FastGood:
                judgeSprite = GameNoteImageContainer.JudgeGood;
                coloredSprite = GameNoteImageContainer.JudgeFastGood;
                return true;
            case NoteJudge.ETiming.LateGood:
                judgeSprite = GameNoteImageContainer.JudgeGood;
                coloredSprite = GameNoteImageContainer.JudgeLateGood;
                return true;
            case NoteJudge.ETiming.FastGreat3rd:
            case NoteJudge.ETiming.FastGreat2nd:
            case NoteJudge.ETiming.FastGreat:
                judgeSprite = GameNoteImageContainer.JudgeGreat;
                coloredSprite = GameNoteImageContainer.JudgeFastGreat;
                return true;
            case NoteJudge.ETiming.LateGreat3rd:
            case NoteJudge.ETiming.LateGreat2nd:
            case NoteJudge.ETiming.LateGreat:
                judgeSprite = GameNoteImageContainer.JudgeGreat;
                coloredSprite = GameNoteImageContainer.JudgeLateGreat;
                return true;
            case NoteJudge.ETiming.FastPerfect2nd:
            case NoteJudge.ETiming.FastPerfect:
                judgeSprite = GameNoteImageContainer.JudgePerfect;
                coloredSprite = GameNoteImageContainer.JudgeFastPerfect;
                return true;
            case NoteJudge.ETiming.LatePerfect2nd:
            case NoteJudge.ETiming.LatePerfect:
                judgeSprite = GameNoteImageContainer.JudgePerfect;
                coloredSprite = GameNoteImageContainer.JudgeLatePerfect;
                return true;
            default:
                judgeSprite = null;
                coloredSprite = null;
                return false;
        }
    }

    private static void ApplyNormalJudgeGradeDisplay(
        JudgeGrade instance,
        NormalDisplayMode mode,
        bool isFast,
        Sprite judgeSprite,
        Sprite coloredSprite,
        SpriteRenderer spriteRender,
        SpriteRenderer spriteRenderFastLate,
        SpriteRenderer spriteRenderAdd = null)
    {
        instance.gameObject.SetActive(true);
        if (spriteRenderFastLate != null) spriteRenderFastLate.gameObject.SetActive(false);
        if (spriteRenderAdd != null) spriteRenderAdd.gameObject.SetActive(false);

        var timingSprite = isFast ? GameNoteImageContainer.JudgeFast : GameNoteImageContainer.JudgeLate;
        switch (mode)
        {
            case NormalDisplayMode.JudgeOnly:
                spriteRender.sprite = judgeSprite;
                break;
            case NormalDisplayMode.All:
                spriteRender.sprite = judgeSprite;
                if (spriteRenderFastLate != null)
                {
                    spriteRenderFastLate.sprite = timingSprite;
                    spriteRenderFastLate.gameObject.SetActive(true);
                }
                break;
            case NormalDisplayMode.TimingOnly:
                spriteRender.sprite = timingSprite;
                break;
            case NormalDisplayMode.ColoredJudge:
                spriteRender.sprite = coloredSprite;
                break;
            case NormalDisplayMode.None:
                instance.gameObject.SetActive(false);
                break;
        }
    }

    private static void ApplyCriticalJudgeGradeDisplay(
        JudgeGrade instance,
        UserSettings settings,
        bool isBreak,
        SpriteRenderer spriteRender,
        SpriteRenderer spriteRenderAdd)
    {
        switch (Logic.GetCriticalDisplayAction(settings.CriticalDisplayMode, isBreak))
        {
            case CriticalDisplayAction.AsPerfect:
                switch (settings.GetPerfectDisplayMode(isBreak))
                {
                    case NormalDisplayMode.JudgeOnly:
                    case NormalDisplayMode.All:
                        spriteRender.sprite = GameNoteImageContainer.JudgePerfect;
                        if (spriteRenderAdd != null) spriteRenderAdd.sprite = GameNoteImageContainer.JudgePerfectBreak;
                        break;
                    case NormalDisplayMode.TimingOnly:
                    case NormalDisplayMode.ColoredJudge:
                    case NormalDisplayMode.None:
                        instance.gameObject.SetActive(false);
                        if (spriteRenderAdd != null) spriteRenderAdd.gameObject.SetActive(false);
                        break;
                }
                break;
            case CriticalDisplayAction.Critical:
                spriteRender.sprite = GameNoteImageContainer.JudgeCritical;
                if (spriteRenderAdd != null)
                {
                    instance.gameObject.SetActive(true);
                    spriteRenderAdd.sprite = GameNoteImageContainer.JudgeCriticalBreak;
                    spriteRenderAdd.gameObject.SetActive(true);
                }
                break;
            case CriticalDisplayAction.Hidden:
                instance.gameObject.SetActive(false);
                if (spriteRenderAdd != null) spriteRenderAdd.gameObject.SetActive(false);
                break;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(JudgeGrade), nameof(JudgeGrade.InitializeBreak))]
    public static void PostJudgeGradeInitializeBreak(JudgeGrade __instance, NoteJudge.ETiming judge, int ____monitorIndex, int ____dispPos, SpriteRenderer ___SpriteRender, SpriteRenderer ___SpriteRenderFastLate, SpriteRenderer ___SpriteRenderAdd)
    {
        if ((uint)____monitorIndex >= userSettings.Length) return;
        if (!userSettings[____monitorIndex].IsEnable) return;
        if (____dispPos == 0)
        {
            __instance.gameObject.SetActive(false);
            if (___SpriteRenderFastLate != null) ___SpriteRenderFastLate.gameObject.SetActive(false);
            ___SpriteRenderAdd.gameObject.SetActive(false);
            return;
        }
        var settings = userSettings[____monitorIndex];
        switch (judge)
        {
            case NoteJudge.ETiming.FastPerfect2nd:
            case NoteJudge.ETiming.FastPerfect:
                ApplyNormalJudgeGradeDisplay(
                    __instance,
                    Logic.GetNormalDisplayMode(settings, judge, true),
                    Logic.IsFastTiming(judge),
                    GameNoteImageContainer.JudgePerfect,
                    GameNoteImageContainer.JudgeFastPerfect,
                    ___SpriteRender,
                    ___SpriteRenderFastLate,
                    ___SpriteRenderAdd);
                return;
            case NoteJudge.ETiming.LatePerfect2nd:
            case NoteJudge.ETiming.LatePerfect:
                ApplyNormalJudgeGradeDisplay(
                    __instance,
                    Logic.GetNormalDisplayMode(settings, judge, true),
                    Logic.IsFastTiming(judge),
                    GameNoteImageContainer.JudgePerfect,
                    GameNoteImageContainer.JudgeLatePerfect,
                    ___SpriteRender,
                    ___SpriteRenderFastLate,
                    ___SpriteRenderAdd);
                return;
            case NoteJudge.ETiming.Critical:
                break;
            default:
                return;
        }
        ApplyCriticalJudgeGradeDisplay(__instance, settings, true, ___SpriteRender, ___SpriteRenderAdd);
    }
}
