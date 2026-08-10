using System;
using System.IO;
using System.IO.Compression;
using AquaMai.Config.Attributes;
using AquaMai.Core;
using AquaMai.Core.Helpers;
using AquaMai.Core.Types;
using HarmonyLib;
using Manager;
using MelonLoader;
using Process;
using UnityEngine;

namespace AquaMai.Mods.UX.JudgeDisplayPro;

[ConfigSection(
    name: "高级判定显示",
    en: "Judge Display Pro",
    zh: "自定义游戏内判定显示")]
[ConfigCollapseNamespace]
public partial class JudgeDisplayPro
{
    private const string StorageKey = "JudgeDisplayPro";

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

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MusicSelectProcess), nameof(MusicSelectProcess.OnStart))]
    public static void LoadSettings()
    {
        for (uint i = 0; i < 2; i++)
        {
            var settings = new UserSettings();
            userSettings[i] = settings;

            var userData = UserDataManager.Instance.GetUserData(i);
            if (!userData.IsEntry) continue;

            var serialized = storage.GetString(i, StorageKey, null);
            if (serialized == null) continue;

            try
            {
                settings.Deserialize(serialized);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[JudgeDisplayPro] 玩家 {i} 的设置读取失败，已恢复默认值：{ex.Message}");
                userSettings[i] = new UserSettings();
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MusicSelectProcess), nameof(MusicSelectProcess.OnRelease))]
    public static void SaveSettings()
    {
        for (uint i = 0; i < 2; i++)
        {
            var userData = UserDataManager.Instance.GetUserData(i);
            if (!userData.IsEntry) continue;
            storage.SetString(i, StorageKey, userSettings[i].Serialize());
        }
    }
}
