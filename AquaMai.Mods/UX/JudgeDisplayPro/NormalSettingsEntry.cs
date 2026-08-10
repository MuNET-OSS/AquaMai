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

    public bool GetIsLeftButtonActive(int player) => GetOptionMode(player) > MinValue;

    public bool GetIsRightButtonActive(int player) => GetOptionMode(player) < MaxValue;

    public int GetOptionMax(int player)
    {
        return (int)MaxValue + 1;
    }

    public string GetOptionValue(int player)
    {
        var currentValue = GetOptionMode(player);
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

    private NormalDisplayMode GetOptionMode(int player)
    {
        return type switch
        {
            NormalSettingsType.Perfect => JudgeDisplayPro.userSettings[player].PerfectDisplayMode,
            NormalSettingsType.PerfectBreak => JudgeDisplayPro.userSettings[player].BreakPerfectDisplayMode,
            NormalSettingsType.Great => JudgeDisplayPro.userSettings[player].GreatDisplayMode,
            NormalSettingsType.Good => JudgeDisplayPro.userSettings[player].GoodDisplayMode,
            _ => NormalDisplayMode.JudgeOnly,
        };
    }

    public int GetOptionValueIndex(int player) => (int)GetOptionMode(player);

    public override string GetSpriteSuffix(int player)
    {
       var typeStr = type switch
        {
            NormalSettingsType.Perfect or NormalSettingsType.PerfectBreak => "小P",
            NormalSettingsType.Great => "GREAT",
            NormalSettingsType.Good => "GOOD",
            _ => null
        };
        var optionStr = GetOptionMode(player) switch
        {
            NormalDisplayMode.JudgeOnly => "仅显示判定",
            NormalDisplayMode.All => "显示判定+FAST LATE",
            NormalDisplayMode.TimingOnly => "仅显示FAST LATE",
            NormalDisplayMode.ColoredJudge when typeStr == "GOOD" => "仅显示颜色判定",
            NormalDisplayMode.ColoredJudge => "仅显示判定颜色",
            NormalDisplayMode.None when typeStr == "小P" => "不显示",
            NormalDisplayMode.None => "不显示判定",
            _ => null
        };

        if (typeStr == null || optionStr == null) return null;
        return $"{typeStr}_{optionStr}";
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
