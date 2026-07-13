using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
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

    private sealed class SlideJudgeBinding
    {
        public int MonitorIndex;
    }

    private struct FastLateState
    {
        public bool IsActive;
        public bool WasJudged;
        public bool ShouldCount;
        public bool IsFast;
        public uint Fast;
        public uint Late;
    }

    private delegate void ScoreCounterSetter(GameScoreList instance, uint value);

    private static readonly ConditionalWeakTable<SlideJudge, SlideJudgeBinding> slideJudgeBindings = new();
    private static readonly ScoreCounterSetter setFast = CreateScoreCounterSetter(nameof(GameScoreList.Fast));
    private static readonly ScoreCounterSetter setLate = CreateScoreCounterSetter(nameof(GameScoreList.Late));

    private static ScoreCounterSetter CreateScoreCounterSetter(string propertyName)
    {
        var setter = AccessTools.PropertySetter(typeof(GameScoreList), propertyName);
        return (ScoreCounterSetter)Delegate.CreateDelegate(typeof(ScoreCounterSetter), setter);
    }

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
                switch (JudgeDisplayProLogic.GetCriticalDisplayAction(userSettings[monitorIndex].CriticalDisplayMode, isBreak))
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

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameScoreList), nameof(GameScoreList.Initialize))]
    public static void PostGameScoreListInitialize(int monitorIndex, UserOption ___UserOption)
    {
        if ((uint)monitorIndex >= userSettings.Length) return;
        if (!userSettings[monitorIndex].IsEnable) return;
        ___UserOption.DispJudge = JudgeDisplayProLogic.GetGameDispJudge(userSettings[monitorIndex].CriticalDisplayMode);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameScoreList), nameof(GameScoreList.SetResult))]
    private static void PreGameScoreListSetResult(GameScoreList __instance, int index, NoteJudge.ETiming timing, int ____monitorIndex, out FastLateState __state)
    {
        __state = default;
        if ((uint)____monitorIndex >= userSettings.Length) return;
        var settings = userSettings[____monitorIndex];
        if (!settings.IsEnable) return;

        __state.IsActive = true;
        __state.Fast = __instance.Fast;
        __state.Late = __instance.Late;
        __state.WasJudged = !__instance.IsEnable || __instance.IsTrackSkip;
        if (__instance.IsEnable && !__instance.IsTrackSkip)
        {
            __state.WasJudged = NotesManager.Instance(____monitorIndex).getReader().GetNoteList()[index].isJudged;
        }
        __state.ShouldCount = JudgeDisplayProLogic.ShouldCountFastLate(settings, timing);
        __state.IsFast = timing is NoteJudge.ETiming.FastGood or
            NoteJudge.ETiming.FastGreat3rd or NoteJudge.ETiming.FastGreat2nd or NoteJudge.ETiming.FastGreat or
            NoteJudge.ETiming.FastPerfect2nd or NoteJudge.ETiming.FastPerfect;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameScoreList), nameof(GameScoreList.SetResult))]
    private static void PostGameScoreListSetResult(GameScoreList __instance, FastLateState __state)
    {
        if (!__state.IsActive || __state.WasJudged) return;
        setFast(__instance, __state.Fast + (__state.ShouldCount && __state.IsFast ? 1u : 0u));
        setLate(__instance, __state.Late + (__state.ShouldCount && !__state.IsFast ? 1u : 0u));
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(ResultProcess), "OnStart")]
    public static IEnumerable<CodeInstruction> ResultProcessOnStartTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var userDataField = AccessTools.Field(typeof(ResultProcess), "_userData");
        var gameScoreListsField = AccessTools.Field(typeof(ResultProcess), "_gameScoreLists");
        var userDataOptionGetter = AccessTools.PropertyGetter(typeof(UserData), nameof(UserData.Option));
        var gameScoreUserOptionField = AccessTools.Field(typeof(GameScoreList), nameof(GameScoreList.UserOption));
        var dispJudgeGetter = AccessTools.PropertyGetter(typeof(UserOption), nameof(UserOption.DispJudge));

        var matches = Enumerable.Range(0, codes.Count - 4)
            .Where(index => codes[index].opcode == OpCodes.Ldfld && Equals(codes[index].operand, userDataField)
                && codes[index + 2].opcode == OpCodes.Ldelem_Ref
                && codes[index + 3].Calls(userDataOptionGetter)
                && codes[index + 4].Calls(dispJudgeGetter))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException($"无法唯一定位 ResultProcess.OnStart 的结算判定选项读取，匹配数：{matches.Length}");
        }

        var match = matches[0];
        codes[match].operand = gameScoreListsField;
        codes[match + 3].opcode = OpCodes.Ldfld;
        codes[match + 3].operand = gameScoreUserOptionField;
        return codes;
    }

}
