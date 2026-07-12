using AquaMai.Core.Types;

namespace AquaMai.Mods.UX.JudgeDisplayPro;

public enum NormalSettingsType
{
    Perfect,
    Great,
    Good,
}

public class NormalSettingsEntry(NormalSettingsType type) : IPlayerSettingsItem
{
    public int Sort => type switch
    {
        NormalSettingsType.Perfect => 153,
        NormalSettingsType.Great => 154,
        NormalSettingsType.Good => 155,
        _ => 0,
    };

    public string Name => type switch
    {
        NormalSettingsType.Perfect => "PERFECT",
        NormalSettingsType.Great => "GREAT",
        NormalSettingsType.Good => "GOOD",
        _ => "UNKNOWN",
    };

    public string Detail => type switch
    {
        NormalSettingsType.Perfect => "影响小P的显示方式",
        NormalSettingsType.Great => "影响绝赞的显示方式",
        NormalSettingsType.Good => "影响GOOD的显示方式",
        _ => "未知",
    };

    public const NormalDisplayMode MinValue = NormalDisplayMode.JudgeOnly;
    public const NormalDisplayMode MaxValue = NormalDisplayMode.TimingOnly;

    public void AddOption(int player)
    {
        switch (type)
        {
            case NormalSettingsType.Perfect:
                JudgeDisplayPro.userSettings[player].PerfectDisplayMode++;
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
            NormalSettingsType.Great => JudgeDisplayPro.userSettings[player].GreatDisplayMode,
            NormalSettingsType.Good => JudgeDisplayPro.userSettings[player].GoodDisplayMode,
            _ => NormalDisplayMode.JudgeOnly,
        };
        return currentValue switch
        {
            NormalDisplayMode.JudgeOnly => "只显示判定",
            NormalDisplayMode.All => "显示判定 + FAST / LATE",
            NormalDisplayMode.TimingOnly => "只显示FAST / LATE",
            NormalDisplayMode.None => "不显示",
            _ => "未知",
        };
    }

    public int GetOptionValueIndex(int player)
    {
        return type switch
        {
            NormalSettingsType.Perfect => (int)JudgeDisplayPro.userSettings[player].PerfectDisplayMode,
            NormalSettingsType.Great => (int)JudgeDisplayPro.userSettings[player].GreatDisplayMode,
            NormalSettingsType.Good => (int)JudgeDisplayPro.userSettings[player].GoodDisplayMode,
            _ => 0,
        };
    }

    public string GetSpriteFile(int player)
    {
        return "UI_OPT_00_00";
    }

    public void SubOption(int player)
    {
        switch (type)
        {
            case NormalSettingsType.Perfect:
                JudgeDisplayPro.userSettings[player].PerfectDisplayMode--;
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
