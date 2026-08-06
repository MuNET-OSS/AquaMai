using AquaMai.Core.Types;

namespace AquaMai.Mods.UX.JudgeDisplayPro;

public abstract class SettingsEntryBase
{
    public string GetSpriteFile(int player)
    {
        var suffix = GetSpriteSuffix(player);
        if(suffix == null) return "UI_OPT_00_00";
        return "AQM_JudgeDisplayPro_" + GetSpriteSuffix(player);
    }

    public abstract string GetSpriteSuffix(int player);
}