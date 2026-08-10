using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using DB;
using HarmonyLib;
using Manager;
using Manager.UserDatas;
using Process;

namespace AquaMai.Mods.UX.JudgeDisplayPro;

public partial class JudgeDisplayPro
{
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

    private static readonly ScoreCounterSetter setFast = CreateScoreCounterSetter(nameof(GameScoreList.Fast));
    private static readonly ScoreCounterSetter setLate = CreateScoreCounterSetter(nameof(GameScoreList.Late));

    private static ScoreCounterSetter CreateScoreCounterSetter(string propertyName)
    {
        var setter = AccessTools.PropertySetter(typeof(GameScoreList), propertyName);
        return (ScoreCounterSetter)Delegate.CreateDelegate(typeof(ScoreCounterSetter), setter);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameScoreList), nameof(GameScoreList.Initialize))]
    public static void PostGameScoreListInitialize(int monitorIndex, UserOption ___UserOption)
    {
        if ((uint)monitorIndex >= userSettings.Length) return;
        if (!userSettings[monitorIndex].IsEnable) return;
        ___UserOption.DispJudge = GetGameDispJudge(userSettings[monitorIndex].CriticalDisplayMode);
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
        var isBreak = false;
        if (__instance.IsEnable && !__instance.IsTrackSkip)
        {
            var note = NotesManager.Instance(____monitorIndex).getReader().GetNoteList()[index];
            __state.WasJudged = note.isJudged;
            isBreak = note.type.isBreak() || note.type.isExBreak();
        }
        __state.ShouldCount = Logic.ShouldCountFastLate(settings, timing, isBreak);
        __state.IsFast = Logic.IsFastTiming(timing);
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

    private static OptionDispjudgeID GetGameDispJudge(CriticalDisplayMode mode)
    {
        return mode switch
        {
            CriticalDisplayMode.None => OptionDispjudgeID.Type1A,
            CriticalDisplayMode.OnBreak or CriticalDisplayMode.OffBreak => OptionDispjudgeID.Type1E,
            CriticalDisplayMode.OnAll or CriticalDisplayMode.OnAllShowBreak or CriticalDisplayMode.OffAll => OptionDispjudgeID.Type3B,
            _ => OptionDispjudgeID.Type1A,
        };
    }

}
