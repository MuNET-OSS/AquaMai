using System.Runtime.CompilerServices;
using HarmonyLib;
using Monitor;
using Process;
using UnityEngine;
using static Monitor.SlideJudge;

namespace AquaMai.Mods.UX.JudgeDisplayPro;

public partial class JudgeDisplayPro
{
    private sealed class SlideJudgeBinding
    {
        public int MonitorIndex;
    }

    private static readonly ConditionalWeakTable<SlideJudge, SlideJudgeBinding> slideJudgeBindings = new();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SlideRoot), nameof(SlideRoot.SetJudgeObject), [typeof(SlideJudge)])]
    public static void PostSlideRootSetJudgeObject(SlideRoot __instance, SlideJudge slideJudge)
    {
        slideJudgeBindings.GetValue(slideJudge, _ => new SlideJudgeBinding()).MonitorIndex = __instance.MonitorId;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SlideJudge), nameof(SlideJudge.Initialize))]
    public static void PostSlideJudgeInitialize(SlideJudge __instance, bool isBreak, NoteJudge.ETiming judge, SpriteRenderer ___SpriteRender, SlideJudgeType ____judgeType, SlideAngle ____angle, SpriteRenderer ___SpriteRenderAdd)
    {
        if (!slideJudgeBindings.TryGetValue(__instance, out var binding)) return;
        var monitorIndex = binding.MonitorIndex;
        if ((uint)monitorIndex >= userSettings.Length) return;
        if (!userSettings[monitorIndex].IsEnable) return;
        var settings = userSettings[monitorIndex];
        __instance.gameObject.SetActive(true);
        ___SpriteRenderAdd.gameObject.SetActive(false);
        switch (judge)
        {
            case NoteJudge.ETiming.FastGood:
                ApplySlideDisplayMode(
                    __instance,
                    Logic.GetNormalDisplayMode(settings, judge, isBreak),
                    GameNoteImageContainer.JudgeSlideFastGood[(int)____judgeType, (int)____angle],
                    GameNoteImageContainer.JudgeSlideFastGoodCol[(int)____judgeType, (int)____angle],
                    ___SpriteRender);
                break;
            case NoteJudge.ETiming.LateGood:
                ApplySlideDisplayMode(
                    __instance,
                    Logic.GetNormalDisplayMode(settings, judge, isBreak),
                    GameNoteImageContainer.JudgeSlideLateGood[(int)____judgeType, (int)____angle],
                    GameNoteImageContainer.JudgeSlideLateGoodCol[(int)____judgeType, (int)____angle],
                    ___SpriteRender);
                break;
            case NoteJudge.ETiming.FastGreat3rd:
            case NoteJudge.ETiming.FastGreat2nd:
            case NoteJudge.ETiming.FastGreat:
                ApplySlideDisplayMode(
                    __instance,
                    Logic.GetNormalDisplayMode(settings, judge, isBreak),
                    GameNoteImageContainer.JudgeSlideFastGreat[(int)____judgeType, (int)____angle],
                    GameNoteImageContainer.JudgeSlideFastGreatCol[(int)____judgeType, (int)____angle],
                    ___SpriteRender);
                break;
            case NoteJudge.ETiming.LateGreat3rd:
            case NoteJudge.ETiming.LateGreat2nd:
            case NoteJudge.ETiming.LateGreat:
                ApplySlideDisplayMode(
                    __instance,
                    Logic.GetNormalDisplayMode(settings, judge, isBreak),
                    GameNoteImageContainer.JudgeSlideLateGreat[(int)____judgeType, (int)____angle],
                    GameNoteImageContainer.JudgeSlideLateGreatCol[(int)____judgeType, (int)____angle],
                    ___SpriteRender);
                break;
            case NoteJudge.ETiming.FastPerfect2nd:
            case NoteJudge.ETiming.FastPerfect:
                ApplySlideDisplayMode(
                    __instance,
                    Logic.GetNormalDisplayMode(settings, judge, isBreak),
                    GameNoteImageContainer.JudgeSlidePerfect[(int)____judgeType, (int)____angle],
                    GameNoteImageContainer.JudgeSlideFastPerfectCol[(int)____judgeType, (int)____angle],
                    ___SpriteRender, 
                    showTimingForAll: true);
                break;
            case NoteJudge.ETiming.LatePerfect2nd:
            case NoteJudge.ETiming.LatePerfect:
                ApplySlideDisplayMode(
                    __instance,
                    Logic.GetNormalDisplayMode(settings, judge, isBreak),
                    GameNoteImageContainer.JudgeSlidePerfect[(int)____judgeType, (int)____angle],
                    GameNoteImageContainer.JudgeSlideLatePerfectCol[(int)____judgeType, (int)____angle],
                    ___SpriteRender, 
                    showTimingForAll: true);
                break;
            case NoteJudge.ETiming.Critical:
                switch (Logic.GetCriticalDisplayAction(settings.CriticalDisplayMode, isBreak))
                {
                    case CriticalDisplayAction.AsPerfect:
                        switch (settings.GetPerfectDisplayMode(isBreak))
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
                    case CriticalDisplayAction.Critical:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeSlideCritical[(int)____judgeType, (int)____angle];
                        ___SpriteRenderAdd.sprite = GameNoteImageContainer.JudgeSlideCriticalBreak[(int)____judgeType, (int)____angle];
                        if (isBreak)
                        {
                            ___SpriteRenderAdd.gameObject.SetActive(true);
                        }
                        break;
                    case CriticalDisplayAction.Hidden:
                        __instance.gameObject.SetActive(false);
                        ___SpriteRenderAdd.gameObject.SetActive(false);
                        break;
                }
                break;
        }
    }

    private static void ApplySlideDisplayMode(SlideJudge instance,
        NormalDisplayMode mode,
        Sprite judgeSprite,
        Sprite timingSprite,
        SpriteRenderer spriteRender,
        bool showTimingForAll = false // 如果为true，则 NormalDisplayMode.All 也显示蓝红的 timingSprite 而不是黄粉绿的 judgeSprite 。 这大概对应着某种“小P星星”的更好的处理，但现阶段这个游戏根本没有小P星星，所以并不重要。
        )
    {
        switch (mode)
        {
            case NormalDisplayMode.JudgeOnly:
                spriteRender.sprite = judgeSprite;
                break;
            case NormalDisplayMode.All:
                spriteRender.sprite = showTimingForAll ? timingSprite : judgeSprite;
                break;
            case NormalDisplayMode.TimingOnly:
            case NormalDisplayMode.ColoredJudge:
                spriteRender.sprite = timingSprite;
                break;
            case NormalDisplayMode.None:
                instance.gameObject.SetActive(false);
                break;
        }
    }

}
