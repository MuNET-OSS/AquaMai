using System;
using System.IO;
using System.IO.Compression;
using AquaMai.Config.Attributes;
using AquaMai.Core;
using AquaMai.Core.Helpers;
using AquaMai.Core.Types;
using MelonLoader;
using UnityEngine;

namespace AquaMai.Mods.UX.JudgeDisplayPro;

[ConfigSection]
[ConfigCollapseNamespace]
public partial class JudgeDisplayPro
{
    // 有些地方有 4P
    public static UserSettings[] userSettings = [new UserSettings(), new UserSettings(), new UserSettings(), new UserSettings()];
    public static IPersistentStorage storage = new PlayerPrefsStorage();

    private static Stream GetAssetBundleStream()
    {
        var s = Core.BuildInfo.ModAssembly.Assembly.GetManifestResourceStream("judgedisplaypro");
        if (s != null) return s;
        return null;
    }

    public static void OnBeforePatch()
    {
        GameSettingsManager.RegisterSetting(new OnOffSettingsEntry());
        GameSettingsManager.RegisterSetting(new CriticalSettingsEntry());
        GameSettingsManager.RegisterSetting(new NormalSettingsEntry(NormalSettingsType.Perfect));
        GameSettingsManager.RegisterSetting(new NormalSettingsEntry(NormalSettingsType.PerfectBreak));
        GameSettingsManager.RegisterSetting(new NormalSettingsEntry(NormalSettingsType.Great));
        GameSettingsManager.RegisterSetting(new NormalSettingsEntry(NormalSettingsType.Good));

        try
        {
            using var stream = GetAssetBundleStream();
            if (stream == null) return;
            var bundle = AssetBundle.LoadFromStream(stream);
            if (bundle == null) return;
            GameSettingsManagerSprites.RegisterBundle("AQM_JudgeDisplayPro_", bundle);
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[JudgeDisplayPro] Failed to load AB: " + ex.Message);
        }
    }
}
