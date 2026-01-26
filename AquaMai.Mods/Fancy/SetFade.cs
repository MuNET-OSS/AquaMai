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
using MelonLoader.TinyJSON;
using System.Collections.Generic;
using Monitor;
using Mono.Posix;

namespace AquaMai.Mods.Fancy;

[ConfigSection(name: "转场动画PLUS",
    en: "Set Fade Animation",
    zh: "修改转场动画为其他变种")]
public class SetFade
{
    [ConfigEntry(name: "转场类型", zh: "0:Normal, 1:Plus, 2:Festa（仅限1.60+）,5:禁用")]
    public static readonly int FadeType = 5;

    [ConfigEntry(name: "[仅限1.55+]启用特殊KLD转场", zh: "仅在配置过的歌曲启用KLD转场。")]
    public static readonly bool isKLDEnabled = true;
    private static readonly string JSONDir = "LocalAssets";
    private static readonly string JSONFileName = "CommonFadeList.json";

    private static bool isResourcePatchEnabled = false;
    private static bool _isInitialized = false;
    private static Sprite[] subBGs = new Sprite[3];
    private static List<CommonFadeEntry> cachedEntries = new List<CommonFadeEntry>();

    private static int _kldRemainingCharges = 0;
    internal static CommonFadeEntry _activeKldConfig = null;

    // --- TinyJson 数据模型 ---
    // 注意：字段名必须与 JSON 中的 Key 完全一致，且必须为 public
    public class CommonFadeEntry
    {
        public int ID;
        public int isBlack;
        public int Type;
        public int FadeType;
    }

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

    // --- 1. 核心解析逻辑：使用 TinyJson ---
    private static void LoadJsonManual()
    {
        try
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, JSONDir, JSONFileName);
            if (!File.Exists(path))
            {
                MelonLogger.Warning($"[SetFade] 配置文件未找到: {path}");
                return;
            }

            string jsonContent = File.ReadAllText(path);
            cachedEntries.Clear();

            // --- 修复 Line 80 ---
            // 错误写法: var data = Process.TinyJson.FromJson<List<CommonFadeEntry>>(jsonContent);
            // 正确写法 (使用 MelonLoader.TinyJSON):

            var variant = JSON.Load(jsonContent); // 1. 先载入为 Variant 对象
            if (variant != null)
            {
                var data = variant.Make<List<CommonFadeEntry>>(); // 2. 再转换为具体的 List

                if (data != null)
                {
                    cachedEntries = data;
                    MelonLogger.Msg($"[SetFade] 成功通过 MelonLoader.TinyJSON 载入 {cachedEntries.Count} 条配置。");
                }
            }
        }
        catch (Exception e)
        {
            MelonLogger.Error($"[SetFade] JSON 解析失败! 请检查格式。错误: {e.Message}");
        }
    }

    // --- 2. 选曲监听与充能 ---
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MusicSelectMonitor), "UpdateRivalScore")]
    [HarmonyPatch(typeof(MusicSelectMonitor), "SetRivalScore")]
    public static void OnMusicSelectionChanged(MusicSelectProcess ____musicSelect)
    {
        if (!isKLDEnabled || ____musicSelect == null) return;
        try
        {
            var musicData = ____musicSelect.GetMusic(0)?.MusicData;
            if (musicData != null)
            {
                var matched = cachedEntries.Find(e => e.ID == musicData.name.id);
                if (matched != null)
                {
                    if (_activeKldConfig != matched)
                    {
                        _activeKldConfig = matched;
                        _kldRemainingCharges = 3;
                        MelonLogger.Msg($"[SetFade] 目标锁定：ID {matched.ID}，KLD 已充能 (3次)");
                    }
                }
                else
                {
                    _activeKldConfig = null;
                    _kldRemainingCharges = 0;
                }
            }
        }
        catch { }
    }

    // --- 3. 资源拦截与重定向 ---
    [HarmonyPrefix]
    [HarmonyPatch(typeof(FadeProcess), "OnStart")]
    [HarmonyPatch(typeof(AdvertiseProcess), "InitFade")]
    [HarmonyPatch(typeof(NextTrackProcess), "OnStart")]
    public static void StartFadePrefix()
    {
        isResourcePatchEnabled = (_kldRemainingCharges > 0 && _activeKldConfig != null);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Resources), "Load", new[] { typeof(string), typeof(global::System.Type) })]
    public static bool ResourcesLoadPrefix(ref string path, global::System.Type systemTypeInstance, ref UnityEngine.Object __result)
    {
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

    // --- 4. 动画驱动 ---
    [HarmonyPostfix]
    [HarmonyPatch(typeof(FadeProcess), "OnStart")]
    [HarmonyPatch(typeof(AdvertiseProcess), "InitFade")]
    [HarmonyPatch(typeof(NextTrackProcess), "OnStart")]
    public static void GlobalPostfix(GameObject[] ___fadeObject)
    {
        if (isResourcePatchEnabled && _activeKldConfig != null)
        {
            _kldRemainingCharges--;
            if (___fadeObject != null)
            {
                foreach (var monitor in ___fadeObject)
                    DriveKLDAnimation(monitor, _activeKldConfig);
            }
        }
        else if (___fadeObject != null)
        {
            foreach (var monitor in ___fadeObject)
                ReplaceSubBG(monitor);
        }
        isResourcePatchEnabled = false;
        if (_kldRemainingCharges <= 0) _activeKldConfig = null;
    }

    private static void ReplaceSubBG(GameObject monitor)
    {
        if (FadeType < 0 || FadeType >= subBGs.Length) return;
        try
        {
            var subBG = monitor.transform.Find("Canvas/Sub/Sub_ChangeScreen(Clone)/Sub_BG")?.GetComponent<Image>();
            if (subBG != null) subBG.sprite = subBGs[FadeType];
        }
        catch { }
    }

    private static void DriveKLDAnimation(GameObject monitor, CommonFadeEntry cfg)
    {
        try
        {
            var main = monitor.transform.Find("Canvas/Main/UI_KLD_ChangeScreen(Clone)");
            var sub = monitor.transform.Find("Canvas/Sub/UI_KLD_Sub_ChangeScreen(Clone)");

            string animName = cfg.FadeType switch
            {
                1 => "In",
                2 => "Out_02",
                3 => "Out_03",
                _ => "In"
            };

            if (main != null)
            {
                var ctrl = main.GetComponent<KaleidxScopeFadeController>();
                if (ctrl != null)
                {
                    ctrl.SetBackGroundType(cfg.isBlack != 0 ? KaleidxScopeFadeController.BackGroundType.Black : KaleidxScopeFadeController.BackGroundType.Normal);
                    if (cfg.Type == 10) ctrl.SetSpriteType((KaleidxScopeFadeController.SpriteType)7);
                    else ctrl.SetSpriteType((KaleidxScopeFadeController.SpriteType)cfg.Type);
                    if (Enum.TryParse<KaleidxScopeFadeController.AnimState>(animName, out var state))
                        ctrl.PlayAnimation(state);
                }
            }
            if (sub != null)
            {
                var sCtrl = sub.GetComponent<KaleidxScopeSubFadeController>();
                if (sCtrl != null)
                {
                    sCtrl.SetBackGroundType(cfg.isBlack != 0 ? KaleidxScopeSubFadeController.BackGroundType.Black : KaleidxScopeSubFadeController.BackGroundType.Normal);
                    if (cfg.Type == 10) sCtrl.SetSpriteType((KaleidxScopeSubFadeController.SpriteType)7);
                    else sCtrl.SetSpriteType((KaleidxScopeSubFadeController.SpriteType)cfg.Type);
                    sCtrl.PlayAnimation(KaleidxScopeSubFadeController.AnimState.In);
                }
            }
        }
        catch { }
    }
}
