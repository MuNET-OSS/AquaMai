using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using AquaMai.Core.Types;
using HarmonyLib;
using Monitor;
using Process;
using UnityEngine;
using static Monitor.SlideJudge;

namespace AquaMai.Mods.UX.JudgeDisplayPro;

[ConfigSection]
[ConfigCollapseNamespace]
public class JudgeDisplayPro
{
    public static UserSettings[] userSettings = [new UserSettings(), new UserSettings()];
    public static IPersistentStorage storage = new PlayerPrefsStorage();

    public static void OnBeforePatch()
    {
        GameSettingsManager.RegisterSetting(new OnOffSettingsEntry());
        GameSettingsManager.RegisterSetting(new CriticalSettingsEntry());
        GameSettingsManager.RegisterSetting(new NormalSettingsEntry(NormalSettingsType.Perfect));
        GameSettingsManager.RegisterSetting(new NormalSettingsEntry(NormalSettingsType.Great));
        GameSettingsManager.RegisterSetting(new NormalSettingsEntry(NormalSettingsType.Good));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(JudgeGrade), nameof(JudgeGrade.Initialize))]
    public static void PostJudgeGradeInitialize(JudgeGrade __instance, NoteJudge.ETiming judge, int ____monitorIndex, SpriteRenderer ___SpriteRender, SpriteRenderer ___SpriteRenderFastLate)
    {
        if (!userSettings[____monitorIndex].IsEnable) return;
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



}
