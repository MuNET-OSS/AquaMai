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
        { // 大P是不分fast和late的，因此与其他类型判定有较大区别，故单独分一个函数处理
            ApplyCriticalJudgeGradeDisplay(__instance, settings, false, ___SpriteRender, null);
            return;
        }

        if (!TryGetNormalJudgeSprites(judge, out var judgeSprite, out var coloredSprite)) return;
        // 上面这个函数对 MISS 和 大P 会返回false，所以下面的不执行，自然就不触发我们的Mod
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
                        if (isBreak && spriteRenderAdd != null) spriteRenderAdd.sprite = GameNoteImageContainer.JudgePerfectBreak;
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
                if (isBreak && spriteRenderAdd != null)
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
            // 尽管JudgeGrade.InitializeBreak里已经会调用JudgeGrade.Initialize了，
            // 但是，（受到游戏原始代码所限、缺乏一个机制稳定地在Initialize中获知当前Note是不是绝赞），PostJudgeGradeInitialize中是假定音符一定是非绝赞来处理的。
            // 那么，如果用户的普通音符小P和绝赞音符小P配置不一致，PostJudgeGradeInitialize中ApplyNormalJudgeGradeDisplay所处理出的内容就会是错的。
            // 因此，我们在这里必须对小P的情况，重新调用ApplyNormalJudgeGradeDisplay(isBreak: true)处理一次，才能保证行为是对的。
            // Great和Good的情况不需要考虑，本质是我们并没有给用户提供“绝赞Great”的单独配置项，因此绝赞Great和非绝赞Great的行为是相同的，这部分在PostJudgeGradeInitialize中已经处理过了。
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
                ApplyCriticalJudgeGradeDisplay(__instance, settings, true, ___SpriteRender, ___SpriteRenderAdd);
                return;
            default:
                return;
        }
    }
}
