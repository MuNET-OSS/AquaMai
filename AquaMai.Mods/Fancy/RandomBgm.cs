using System;
using System.Collections.Generic;
using System.IO;
using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using HarmonyLib;
using Mai2.Mai2Cue;
using MAI2.Util;
using Manager;
using MelonLoader;

namespace AquaMai.Mods.Fancy;

[ConfigSection(
    name: "随机 BGM",
    en: """
        Random BGM.
        Put Mai2Cue.{acb,awb} of old version of the game in the configured directory and rename them.
        Won't work with 2P mode.
        """,
    zh: """
        在配置的目录下放置了旧版游戏的 Mai2Cue.{acb,awb} 并重命名的话，可以在播放游戏 BGM 的时候随机播放这里面的旧版游戏 BGM
        无法在 2P 模式下工作
        """)]
public class RandomBgm
{
    [ConfigEntry(name: "Mai2Cue 目录")]
    private static readonly string mai2CueDir = "LocalAssets/Mai2Cue";

    private static List<string> _acbs = new List<string>();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SoundManager), "Initialize")]
    public static void Init()
    {
        // 目录要到 amdaemon 就绪后才齐（option 包这时才挂载）
        AmDaemonReady.RunWhenReady(LoadAcbs);
    }

    private static void LoadAcbs()
    {
        // 按文件名合并，同名 acb 以优先级高的目录为准，避免重复注册
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in LocalAssets.EnumerateFiles(mai2CueDir))
        {
            if (!file.EndsWith(".acb", StringComparison.OrdinalIgnoreCase)) continue;

            // acb 和 awb 是成对的，游戏按 acb 所在目录去找 awb，缺了 awb 的话这份 cue 加载不起来，
            // 所以宁可继续用低优先级目录里完整的那份
            if (!File.Exists(Path.ChangeExtension(file, ".awb")))
            {
                MelonLogger.Warning($"[RandomBgm] Missing awb for {Path.GetFileName(file)}, skipping");
                continue;
            }

            files[Path.GetFileNameWithoutExtension(file)] = file;
        }

        foreach (var file in files.Values)
        {
            // Seems there's limit for max opened ACB files
            _acbs.Add(Path.ChangeExtension(file, null));
        }

        MelonLogger.Msg($"Random BGM loaded {_acbs.Count} files");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SoundManager), "Play")]
    public static void PrePlay(ref SoundManager.AcbID acbID, int cueID)
    {
        if (acbID != SoundManager.AcbID.Default) return;
        if (_acbs.Count == 0) return;
        var cueIndex = (Cue)cueID;
        switch (cueIndex)
        {
            case Cue.BGM_ENTRY:
            case Cue.BGM_COLLECTION:
            case Cue.BGM_RESULT_CLEAR:
            case Cue.BGM_RESULT:
                var acb = _acbs[UnityEngine.Random.Range(0, _acbs.Count)];
                acbID = SoundManager.AcbID.Max;
                var result = Singleton<SoundCtrl>.Instance.LoadCueSheet((int)acbID, acb);
                MelonLogger.Msg($"Picked {acb} for {cueIndex}, result: {result}");
                return;
            default:
                return;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SoundManager), "PlayBGM")]
    public static bool PrePlayBGM(ref int target)
    {
        switch (target)
        {
            case 0:
                return true;
            case 1:
                return false;
            case 2:
                target = 0;
                return true;
            default:
                return false;
        }
    }
}
