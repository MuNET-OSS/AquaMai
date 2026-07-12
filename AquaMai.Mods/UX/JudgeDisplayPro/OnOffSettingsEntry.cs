using AquaMai.Core.Types;

namespace AquaMai.Mods.UX.JudgeDisplayPro;

public class OnOffSettingsEntry : IPlayerSettingsItem
{
    public int Sort => 151;

    public string Name => "启用高级判定表示";

    public string Detail => "影响接下来 TODO 个选项是否生效，还是跟随游戏自带的判定表示";

    public void AddOption(int player)
    {
        JudgeDisplayPro.userSettings[player].IsEnable = true;
    }

    public bool GetIsLeftButtonActive(int player)
    {
        return JudgeDisplayPro.userSettings[player].IsEnable;
    }

    public bool GetIsRightButtonActive(int player)
    {
        return !JudgeDisplayPro.userSettings[player].IsEnable;
    }

    public int GetOptionMax(int player)
    {
        return 1;
    }

    public string GetOptionValue(int player)
    {
        return JudgeDisplayPro.userSettings[player].IsEnable ? "ON" : "OFF";
    }

    public int GetOptionValueIndex(int player)
    {
        return JudgeDisplayPro.userSettings[player].IsEnable ? 0 : 1;
    }

    public string GetSpriteFile(int player)
    {
        return "UI_OPT_00_00";
    }

    public void SubOption(int player)
    {
        JudgeDisplayPro.userSettings[player].IsEnable = false;
    }
}
