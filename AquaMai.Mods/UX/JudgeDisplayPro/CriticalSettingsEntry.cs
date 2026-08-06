using AquaMai.Core.Types;

namespace AquaMai.Mods.UX.JudgeDisplayPro;

public class CriticalSettingsEntry : SettingsEntryBase, IPlayerSettingsItem
{
    public int Sort => 152;

    public string Name => "CRITICAL PERFECT";

    public string Detail => "影响大P的显示方式";

    public const CriticalDisplayMode MinValue = CriticalDisplayMode.None;
    public const CriticalDisplayMode MaxValue = CriticalDisplayMode.OffAll;

    public void AddOption(int player)
    {
        if (!GetIsRightButtonActive(player)) return;
        JudgeDisplayPro.userSettings[player].CriticalDisplayMode++;
    }

    public bool GetIsLeftButtonActive(int player)
    {
        return JudgeDisplayPro.userSettings[player].CriticalDisplayMode > MinValue;
    }

    public bool GetIsRightButtonActive(int player)
    {
        return JudgeDisplayPro.userSettings[player].CriticalDisplayMode < MaxValue;
    }

    public int GetOptionMax(int player)
    {
        return (int)MaxValue + 1;
    }

    public string GetOptionValue(int player)
    {
        switch (JudgeDisplayPro.userSettings[player].CriticalDisplayMode)
        {
            case CriticalDisplayMode.None:
                return "不开启大P";
            case CriticalDisplayMode.OnBreak:
                return "只有绝赞开启大P，并显示";
            case CriticalDisplayMode.OffBreak:
                return "只有绝赞开启大P，但不显示";
            case CriticalDisplayMode.OnAll:
                return "所有音符开启大P，并显示";
            case CriticalDisplayMode.OnAllShowBreak:
                return "所有音符开启大P，仅绝赞显示";
            case CriticalDisplayMode.OffAll:
                return "所有音符开启大P，但不显示";
            default:
                return "未知";
        }
    }

    public int GetOptionValueIndex(int player)
    {
        return (int)JudgeDisplayPro.userSettings[player].CriticalDisplayMode;
    }

    public override string GetSpriteSuffix(int player)
    {
        return JudgeDisplayPro.userSettings[player].CriticalDisplayMode switch
        {
            CriticalDisplayMode.None => "不开启大P",
            CriticalDisplayMode.OnBreak => "只有绝赞开启大P，并显示",
            CriticalDisplayMode.OffBreak => "只有绝赞开启大P，但不显示",
            CriticalDisplayMode.OnAll => "所有的音符开启大P，并显示",
            CriticalDisplayMode.OnAllShowBreak => "所有的音符开启大P，仅绝赞显示",
            CriticalDisplayMode.OffAll => "所有音符开启大P，但不显示",
            _ => null,
        };
    }

    public void SubOption(int player)
    {
        if (!GetIsLeftButtonActive(player)) return;
        JudgeDisplayPro.userSettings[player].CriticalDisplayMode--;
    }
}
