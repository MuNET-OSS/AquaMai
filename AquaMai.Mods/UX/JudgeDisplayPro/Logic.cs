using Manager;

namespace AquaMai.Mods.UX.JudgeDisplayPro;

public enum CriticalDisplayAction
{
    AsPerfect,
    Critical,
    Hidden,
}

public static class Logic
{
    public static NormalDisplayMode GetNormalDisplayMode(UserSettings settings, NoteJudge.ETiming timing, bool isBreak)
    {
        return timing switch
        {
            NoteJudge.ETiming.FastGood or NoteJudge.ETiming.LateGood => settings.GoodDisplayMode,
            NoteJudge.ETiming.FastGreat3rd or NoteJudge.ETiming.FastGreat2nd or NoteJudge.ETiming.FastGreat or
                NoteJudge.ETiming.LateGreat3rd or NoteJudge.ETiming.LateGreat2nd or NoteJudge.ETiming.LateGreat => settings.GreatDisplayMode,
            NoteJudge.ETiming.FastPerfect2nd or NoteJudge.ETiming.FastPerfect or
                NoteJudge.ETiming.LatePerfect2nd or NoteJudge.ETiming.LatePerfect => settings.GetPerfectDisplayMode(isBreak),
            _ => NormalDisplayMode.None,
        };
    }

    public static bool IsFastTiming(NoteJudge.ETiming timing)
    {
        return timing is NoteJudge.ETiming.FastGood or
            NoteJudge.ETiming.FastGreat3rd or NoteJudge.ETiming.FastGreat2nd or NoteJudge.ETiming.FastGreat or
            NoteJudge.ETiming.FastPerfect2nd or NoteJudge.ETiming.FastPerfect;
    }

    public static bool ShouldCountFastLate(UserSettings settings, NoteJudge.ETiming timing, bool isBreak)
    {
        return GetNormalDisplayMode(settings, timing, isBreak) is
            NormalDisplayMode.All or NormalDisplayMode.TimingOnly or NormalDisplayMode.ColoredJudge;
    }

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
}
