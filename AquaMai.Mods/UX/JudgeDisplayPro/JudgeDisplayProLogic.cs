using DB;
using Manager;

namespace AquaMai.Mods.UX.JudgeDisplayPro;

internal enum CriticalDisplayAction
{
    AsPerfect,
    Critical,
    Hidden,
}

internal static class JudgeDisplayProLogic
{
    public static CriticalDisplayAction GetCriticalDisplayAction(CriticalDisplayMode mode, bool isBreak)
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

    public static OptionDispjudgeID GetGameDispJudge(CriticalDisplayMode mode)
    {
        return mode switch
        {
            CriticalDisplayMode.None => OptionDispjudgeID.Type1A,
            CriticalDisplayMode.OnBreak or CriticalDisplayMode.OffBreak => OptionDispjudgeID.Type1E,
            CriticalDisplayMode.OnAll or CriticalDisplayMode.OnAllShowBreak or CriticalDisplayMode.OffAll => OptionDispjudgeID.Type3B,
            _ => OptionDispjudgeID.Type1A,
        };
    }

    public static bool ShouldCountFastLate(UserSettings settings, NoteJudge.ETiming timing)
    {
        var mode = timing switch
        {
            NoteJudge.ETiming.FastGood or NoteJudge.ETiming.LateGood => settings.GoodDisplayMode,
            NoteJudge.ETiming.FastGreat3rd or NoteJudge.ETiming.FastGreat2nd or NoteJudge.ETiming.FastGreat or
                NoteJudge.ETiming.LateGreat3rd or NoteJudge.ETiming.LateGreat2nd or NoteJudge.ETiming.LateGreat => settings.GreatDisplayMode,
            NoteJudge.ETiming.FastPerfect2nd or NoteJudge.ETiming.FastPerfect or
                NoteJudge.ETiming.LatePerfect2nd or NoteJudge.ETiming.LatePerfect => settings.PerfectDisplayMode,
            _ => NormalDisplayMode.None,
        };

        return mode is NormalDisplayMode.All or NormalDisplayMode.TimingOnly or NormalDisplayMode.ColoredJudge;
    }
}
