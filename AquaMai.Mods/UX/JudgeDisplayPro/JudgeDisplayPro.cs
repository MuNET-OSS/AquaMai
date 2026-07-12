using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using AquaMai.Core.Types;

namespace AquaMai.Mods.UX.JudgeDisplayPro;

[ConfigSection]
[ConfigCollapseNamespace]
public class JudgeDisplayPro
{
    public static UserSettings[] userSettings = [new UserSettings(), new UserSettings()];
    public static IPersistentStorage storage = new PlayerPrefsStorage();

    public static void OnBeforePatch()
    {
        GameSettingsManager.RegisterSetting(new OnOffSettingsEntry());
        GameSettingsManager.RegisterSetting(new CriticalSettingsEntry());
        GameSettingsManager.RegisterSetting(new NormalSettingsEntry(NormalSettingsType.Perfect));
        GameSettingsManager.RegisterSetting(new NormalSettingsEntry(NormalSettingsType.Great));
        GameSettingsManager.RegisterSetting(new NormalSettingsEntry(NormalSettingsType.Good));
    }
}
