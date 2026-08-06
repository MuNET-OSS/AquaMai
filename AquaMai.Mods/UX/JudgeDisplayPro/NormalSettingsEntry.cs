using AquaMai.Core.Types;

namespace AquaMai.Mods.UX.JudgeDisplayPro;

public enum NormalSettingsType
{
    Perfect,
    PerfectBreak,
    Great,
    Good,
}

public class NormalSettingsEntry(NormalSettingsType type) : SettingsEntryBase, IPlayerSettingsItem
{
    public int Sort => type switch
    {
        NormalSettingsType.Perfect => 153,
        NormalSettingsType.PerfectBreak => 154,
        NormalSettingsType.Great => 155,
        NormalSettingsType.Good => 156,
        _ => 0,
    };

    public string Name => type switch
    {
        NormalSettingsType.Perfect => "PERFECT",
        NormalSettingsType.PerfectBreak => "PERFECT (BREAK)",
        NormalSettingsType.Great => "GREAT",
        NormalSettingsType.Good => "GOOD",
        _ => "UNKNOWN",
    };

    public string Detail => type switch
    {
        NormalSettingsType.Perfect => "影响小P的显示方式",
        NormalSettingsType.PerfectBreak => "影响绝赞小P的显示方式",
        NormalSettingsType.Great => "影响GREAT的显示方式",
        NormalSettingsType.Good => "影响GOOD的显示方式",
        _ => "未知",
    };

    public const NormalDisplayMode MinValue = NormalDisplayMode.JudgeOnly;
    public const NormalDisplayMode MaxValue = NormalDisplayMode.None;

    public void AddOption(int player)
    {
        if (!GetIsRightButtonActive(player)) return;
        switch (type)
        {
            case NormalSettingsType.Perfect:
                JudgeDisplayPro.userSettings[player].PerfectDisplayMode++;
                break;
            case NormalSettingsType.PerfectBreak:
                JudgeDisplayPro.userSettings[player].BreakPerfectDisplayMode++;
                break;
            case NormalSettingsType.Great:
                JudgeDisplayPro.userSettings[player].GreatDisplayMode++;
                break;
            case NormalSettingsType.Good:
                JudgeDisplayPro.userSettings[player].GoodDisplayMode++;
                break;
            default:
                break;
        }
    }

    public bool GetIsLeftButtonActive(int player)
    {
        return type switch
        {
            NormalSettingsType.Perfect => JudgeDisplayPro.userSettings[player].PerfectDisplayMode > MinValue,
            NormalSettingsType.PerfectBreak => JudgeDisplayPro.userSettings[player].BreakPerfectDisplayMode > MinValue,
            NormalSettingsType.Great => JudgeDisplayPro.userSettings[player].GreatDisplayMode > MinValue,
            NormalSettingsType.Good => JudgeDisplayPro.userSettings[player].GoodDisplayMode > MinValue,
            _ => false,
        };
    }

    public bool GetIsRightButtonActive(int player)
    {
        return type switch
        {
            NormalSettingsType.Perfect => JudgeDisplayPro.userSettings[player].PerfectDisplayMode < MaxValue,
            NormalSettingsType.PerfectBreak => JudgeDisplayPro.userSettings[player].BreakPerfectDisplayMode < MaxValue,
            NormalSettingsType.Great => JudgeDisplayPro.userSettings[player].GreatDisplayMode < MaxValue,
            NormalSettingsType.Good => JudgeDisplayPro.userSettings[player].GoodDisplayMode < MaxValue,
            _ => false,
        };
    }

    public int GetOptionMax(int player)
    {
        return (int)MaxValue + 1;
    }

    public string GetOptionValue(int player)
    {
        var currentValue = type switch
        {
            NormalSettingsType.Perfect => JudgeDisplayPro.userSettings[player].PerfectDisplayMode,
            NormalSettingsType.PerfectBreak => JudgeDisplayPro.userSettings[player].BreakPerfectDisplayMode,
            NormalSettingsType.Great => JudgeDisplayPro.userSettings[player].GreatDisplayMode,
            NormalSettingsType.Good => JudgeDisplayPro.userSettings[player].GoodDisplayMode,
            _ => NormalDisplayMode.JudgeOnly,
        };
        return currentValue switch
        {
            NormalDisplayMode.JudgeOnly => "仅显示判定",
            NormalDisplayMode.All => "显示判定 + FAST LATE",
            NormalDisplayMode.TimingOnly => "仅显示FAST / LATE",
            NormalDisplayMode.ColoredJudge => "仅显示判定颜色",
            NormalDisplayMode.None => "不显示",
            _ => "未知",
        };
    }

    public int GetOptionValueIndex(int player)
    {
        return type switch
        {
            NormalSettingsType.Perfect => (int)JudgeDisplayPro.userSettings[player].PerfectDisplayMode,
            NormalSettingsType.PerfectBreak => (int)JudgeDisplayPro.userSettings[player].BreakPerfectDisplayMode,
            NormalSettingsType.Great => (int)JudgeDisplayPro.userSettings[player].GreatDisplayMode,
            NormalSettingsType.Good => (int)JudgeDisplayPro.userSettings[player].GoodDisplayMode,
            _ => 0,
        };
    }

    public override string GetSpriteSuffix(int player)
    {
        switch (type)
        {
            case NormalSettingsType.Perfect:
                return JudgeDisplayPro.userSettings[player].PerfectDisplayMode switch
                {
                    NormalDisplayMode.JudgeOnly => "小P_仅显示判定",
                    NormalDisplayMode.All => "小P_显示判定+FAST LATE",
                    NormalDisplayMode.TimingOnly => "小P_仅显示FAST LATE",
                    NormalDisplayMode.ColoredJudge => "小P_仅显示判定颜色",
                    NormalDisplayMode.None => "小P_不显示",
                    _ => null,
                };
            case NormalSettingsType.PerfectBreak:
                return JudgeDisplayPro.userSettings[player].BreakPerfectDisplayMode switch
                {
                    NormalDisplayMode.JudgeOnly => "小P_仅显示判定",
                    NormalDisplayMode.All => "小P_显示判定+FAST LATE",
                    NormalDisplayMode.TimingOnly => "小P_仅显示FAST LATE",
                    NormalDisplayMode.ColoredJudge => "小P_仅显示判定颜色",
                    NormalDisplayMode.None => "小P_不显示",
                    _ => null,
                };
            case NormalSettingsType.Great:
                return JudgeDisplayPro.userSettings[player].GreatDisplayMode switch
                {
                    NormalDisplayMode.JudgeOnly => "GREAT_仅显示判定",
                    NormalDisplayMode.All => "GREAT_显示判定+FAST LATE",
                    NormalDisplayMode.TimingOnly => "GREAT_仅显示FAST LATE",
                    NormalDisplayMode.ColoredJudge => "GREAT_仅显示判定颜色",
                    NormalDisplayMode.None => "GREAT_不显示判定",
                    _ => null,
                };
            case NormalSettingsType.Good:
                return JudgeDisplayPro.userSettings[player].GoodDisplayMode switch
                {
                    NormalDisplayMode.JudgeOnly => "GOOD_仅显示判定",
                    NormalDisplayMode.All => "GOOD_显示判定+FAST LATE",
                    NormalDisplayMode.TimingOnly => "GOOD_仅显示FAST LATE",
                    NormalDisplayMode.ColoredJudge => "GOOD_仅显示颜色判定",
                    NormalDisplayMode.None => "GOOD_不显示判定",
                    _ => null,
                };
        }
        return null;
    }

    public void SubOption(int player)
    {
        if (!GetIsLeftButtonActive(player)) return;
        switch (type)
        {
            case NormalSettingsType.Perfect:
                JudgeDisplayPro.userSettings[player].PerfectDisplayMode--;
                break;
            case NormalSettingsType.PerfectBreak:
                JudgeDisplayPro.userSettings[player].BreakPerfectDisplayMode--;
                break;
            case NormalSettingsType.Great:
                JudgeDisplayPro.userSettings[player].GreatDisplayMode--;
                break;
            case NormalSettingsType.Good:
                JudgeDisplayPro.userSettings[player].GoodDisplayMode--;
                break;
            default:
                break;
        }
    }
}
