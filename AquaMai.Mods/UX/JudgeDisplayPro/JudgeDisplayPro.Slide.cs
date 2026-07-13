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
        __instance.gameObject.SetActive(true);
        ___SpriteRenderAdd.gameObject.SetActive(false);
        switch (judge)
        {
            case NoteJudge.ETiming.FastGood:
                switch (userSettings[monitorIndex].GoodDisplayMode)
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
                switch (userSettings[monitorIndex].GoodDisplayMode)
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
                switch (userSettings[monitorIndex].GreatDisplayMode)
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
                switch (userSettings[monitorIndex].GreatDisplayMode)
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
                switch (userSettings[monitorIndex].PerfectDisplayMode)
                {
                    case NormalDisplayMode.JudgeOnly:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeSlidePerfect[(int)____judgeType, (int)____angle];
                        break;
                    case NormalDisplayMode.All:
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
                switch (userSettings[monitorIndex].PerfectDisplayMode)
                {
                    case NormalDisplayMode.JudgeOnly:
                        ___SpriteRender.sprite = GameNoteImageContainer.JudgeSlidePerfect[(int)____judgeType, (int)____angle];
                        break;
                    case NormalDisplayMode.All:
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
                switch (GetCriticalDisplayAction(userSettings[monitorIndex].CriticalDisplayMode, isBreak))
                {
                    case CriticalDisplayAction.AsPerfect:
                        switch (userSettings[monitorIndex].PerfectDisplayMode)
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

    private enum CriticalDisplayAction
    {
        AsPerfect,
        Critical,
        Hidden,
    }

    private static CriticalDisplayAction GetCriticalDisplayAction(CriticalDisplayMode mode, bool isBreak)
    {
        return mode switch
        {
            CriticalDisplayMode.None => CriticalDisplayAction.AsPerfect,
            CriticalDisplayMode.OnBreak => isBreak ? CriticalDisplayAction.Critical : CriticalDisplayAction.AsPerfect,
            CriticalDisplayMode.OffBreak => isBreak ? CriticalDisplayAction.Hidden : CriticalDisplayAction.AsPerfect,
            CriticalDisplayMode.OnAll => CriticalDisplayAction.Critical,
            CriticalDisplayMode.OnAllShowBreak => isBreak ? CriticalDisplayAction.Critical : CriticalDisplayAction.Hidden,
            CriticalDisplayMode.OffAll => CriticalDisplayAction.Hidden,
            _ => CriticalDisplayAction.Hidden,
        };
    }
}
