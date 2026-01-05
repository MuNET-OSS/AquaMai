using HarmonyLib;
using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using Process;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.IO;
using Manager;
using MelonLoader;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Monitor; 

namespace AquaMai.Mods.Fancy;

[ConfigSection(name: "转场动画PLUS",
    en: "Set Fade Animation",
    zh: "修改转场动画为其他变种")]
public class SetFade
{
    [ConfigEntry(name: "转场类型", zh: "0:Normal, 1:Plus, 2:Festa（仅限1.60+）")]
    public static readonly int FadeType = 0;

    [ConfigEntry(name: "[仅限1.55+]启用特殊KLD转场，需要下载额外JSON文件", zh: "仅在配置过的歌曲启用KLD转场。1.50及以下版本无效。1.50我不想适配了如果有人想适配可以dd我）@力大砖飞")]
    public static readonly bool isKLDEnabled = true;

    private static readonly string JSONDir = "LocalAssets";
    private static readonly string JSONFileName = "CommonFadeList.json"; 

    private static bool isResourcePatchEnabled = false;
    private static bool _isInitialized = false;
    private static Sprite[] subBGs = new Sprite[3];
    private static List<CommonFadeEntry> cachedEntries = new List<CommonFadeEntry>();
    
    // 计数锁定逻辑变量
    private static int _kldRemainingCharges = 0; 
    private static CommonFadeEntry _activeKldConfig = null;

    public class CommonFadeEntry { public int ID; public int isBlack; public int Type; public int FadeType; }

    [HarmonyPrepare]
    public static bool Prepare()
    {
        if (_isInitialized) return true;
        subBGs[0] = Resources.Load<Sprite>("Process/ChangeScreen/Sprites/Sub_01");
        subBGs[1] = Resources.Load<Sprite>("Process/ChangeScreen/Sprites/Sub_02");
        subBGs[2] = (GameInfo.GameVersion >= 26000) ? Resources.Load<Sprite>("Process/ChangeScreen/Sprites/Sub_03") : subBGs[0];
        LoadJsonManual();
        _isInitialized = true;
        return true;
    }

    // --- 1. 实时监听选曲：充能点 ---
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MusicSelectMonitor), "UpdateRivalScore")]
    [HarmonyPatch(typeof(MusicSelectMonitor), "SetRivalScore")]
    public static void OnMusicSelectionChanged(MusicSelectProcess ____musicSelect)
    {
        if (!isKLDEnabled || ____musicSelect == null) return;

        try {
            var musicData = ____musicSelect.GetMusic(0)?.MusicData;
            if (musicData != null)
            {
                var matched = cachedEntries.Find(e => e.ID == musicData.name.id);
                if (matched != null)
                {
                    if (_activeKldConfig != matched) 
                    {
                        _activeKldConfig = matched;
                        _kldRemainingCharges = 3; // 锁定 3 次机会
                        MelonLogger.Msg($"[SetFade] 目标锁定：ID {matched.ID}，KLD 已充能 (3次)");
                    }
                }
                else
                {
                    _activeKldConfig = null;
                    _kldRemainingCharges = 0;
                }
            }
        } catch { }
    }

    // --- 2. 资源拦截触发 ---
    [HarmonyPrefix]
    [HarmonyPatch(typeof(FadeProcess), "OnStart")]
    [HarmonyPatch(typeof(AdvertiseProcess), "InitFade")]
    [HarmonyPatch(typeof(NextTrackProcess), "OnStart")]
    public static void StartFadePrefix()
    {
        // 只有在有充能次数且配置存在时才开启拦截
        isResourcePatchEnabled = (_kldRemainingCharges > 0 && _activeKldConfig != null);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Resources), "Load", new[] { typeof(string), typeof(global::System.Type) })]
    public static bool ResourcesLoadPrefix(ref string path, global::System.Type systemTypeInstance, ref UnityEngine.Object __result)
    {
        // 如果 KLD 拦截未激活，则执行普通重定向（力大砖飞）
        if (!isResourcePatchEnabled)
        {
            if (FadeType >= 0 && FadeType <= 2)
            {
                string targetPath = $"Process/ChangeScreen/Prefabs/ChangeScreen_0{FadeType + 1}";
                if (path.StartsWith("Process/ChangeScreen/Prefabs/ChangeScreen_0") && path != targetPath)
                {
                    if (GameInfo.GameVersion < 26000 && FadeType == 2) return true;
                    __result = Resources.Load(targetPath, systemTypeInstance);
                    return false;
                }
            }
            return true;
        }

        // KLD 资源拦截
        if (path.StartsWith("Process/ChangeScreen/Prefabs/ChangeScreen_0"))
        {
            __result = Resources.Load("Process/Kaleidxscope/Prefab/UI_KLD_ChangeScreen", systemTypeInstance);
            return false;
        }
        if (path.StartsWith("Process/ChangeScreen/Prefabs/Sub_ChangeScreen"))
        {
            __result = Resources.Load("Process/Kaleidxscope/Prefab/UI_KLD_Sub_ChangeScreen", systemTypeInstance);
            return false;
        }
        return true;
    }

    // --- 3. 后置处理：消耗次数与动画播放 ---
    [HarmonyPostfix]
    [HarmonyPatch(typeof(FadeProcess), "OnStart")]
    [HarmonyPatch(typeof(AdvertiseProcess), "InitFade")]
    [HarmonyPatch(typeof(NextTrackProcess), "OnStart")]
    public static void GlobalPostfix(GameObject[] ___fadeObject)
    {
        if (isResourcePatchEnabled && _activeKldConfig != null)
        {
            _kldRemainingCharges--; // 消耗一次
            MelonLogger.Msg($"[SetFade] 触发 KLD 成功，剩余次数: {_kldRemainingCharges}");

            if (___fadeObject != null)
            {
                foreach (var monitor in ___fadeObject) 
                    DriveKLDAnimation(monitor, _activeKldConfig);
            }
        }
        else if (___fadeObject != null)
        {
            // 普通重定向模式下的 SubBG 替换
            foreach (var monitor in ___fadeObject) 
                ReplaceSubBG(monitor);
        }

        isResourcePatchEnabled = false; // 关闭当次拦截锁

        // 次数耗尽清理配置
        if (_kldRemainingCharges <= 0) _activeKldConfig = null;
    }

    private static void ReplaceSubBG(GameObject monitor)
    {
        if (FadeType < 0 || FadeType >= subBGs.Length) return;
        try {
            var subBG = monitor.transform.Find("Canvas/Sub/Sub_ChangeScreen(Clone)/Sub_BG")?.GetComponent<Image>();
            if (subBG != null) subBG.sprite = subBGs[FadeType];
        } catch { }
    }

    private static void DriveKLDAnimation(GameObject monitor, CommonFadeEntry cfg)
    {
        try
        {
            var main = monitor.transform.Find("Canvas/Main/UI_KLD_ChangeScreen(Clone)");
            var sub = monitor.transform.Find("Canvas/Sub/UI_KLD_Sub_ChangeScreen(Clone)");

            // 根据你的要求修正动画映射
            string animName = cfg.FadeType switch { 
                1 => "Out", 
                2 => "Out_02", 
                3 => "Out_03", 
                _ => "Out" 
            };

            if (main != null)
            {
                var ctrl = main.GetComponent<KaleidxScopeFadeController>();
                if (ctrl != null) {
                    ctrl.SetBackGroundType(cfg.isBlack != 0 ? KaleidxScopeFadeController.BackGroundType.Black : KaleidxScopeFadeController.BackGroundType.Normal);
                    ctrl.SetSpriteType((KaleidxScopeFadeController.SpriteType)cfg.Type);
                    if (Enum.TryParse<KaleidxScopeFadeController.AnimState>(animName, out var state))
                        ctrl.PlayAnimation(state);
                }
            }
            if (sub != null)
            {
                var sCtrl = sub.GetComponent<KaleidxScopeSubFadeController>();
                if (sCtrl != null) {
                    sCtrl.SetBackGroundType(cfg.isBlack != 0 ? KaleidxScopeSubFadeController.BackGroundType.Black : KaleidxScopeSubFadeController.BackGroundType.Normal);
                    sCtrl.SetSpriteType((KaleidxScopeSubFadeController.SpriteType)cfg.Type);
                    sCtrl.PlayAnimation(KaleidxScopeSubFadeController.AnimState.In);
                }
            }
        } catch { }
    }

    private static void LoadJsonManual()
    {
        try {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, JSONDir, JSONFileName);
            if (!File.Exists(path)) return;
            string content = File.ReadAllText(path);
            cachedEntries.Clear();
            var matches = Regex.Matches(content, @"\{[^{}]+\}");
            foreach (Match m in matches) {
                string raw = m.Value;
                var e = new CommonFadeEntry {
                    ID = ExtractInt(raw, "ID"),
                    isBlack = ExtractInt(raw, "isBlack"),
                    Type = ExtractInt(raw, "Type"),
                    FadeType = ExtractInt(raw, "FadeType")
                };
                if (e.ID > 0) cachedEntries.Add(e);
            }
            MelonLogger.Msg($"[SetFade] 共载入 {cachedEntries.Count} 条 KLD 特殊配置。");
        } catch (Exception e) { MelonLogger.Error($"[SetFade] JSON加载出错: {e.Message}"); }
    }

    private static int ExtractInt(string text, string key) {
        var m = Regex.Match(text, $"\"{key}\"\\s*:\\s*\"?(\\d+)\"?");
        return (m.Success && int.TryParse(m.Groups[1].Value, out int res)) ? res : 0;
    }
}
