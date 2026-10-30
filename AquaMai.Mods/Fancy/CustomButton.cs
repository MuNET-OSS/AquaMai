using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using HarmonyLib;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using Monitor.Entry.Parts;

namespace AquaMai.Mods.Fancy;

[ConfigSection(
    name: "自定义按钮",
    en: "Custom button textures\nLoad button textures from specified directory",
    zh: "从指定目录加载按钮贴图")]

public class CustomButton
{
    [ConfigEntry(
        name: "贴图目录",
        en: "path to button texture directory",
        zh: "按钮贴图的目录")]
    private static readonly string buttonDir = "LocalAssets/Buttons";
    
    // 游戏查找时用的是资源名，和用户文件名的实际大小写不一定一致
    private static readonly Dictionary<string, Sprite> spriteDict = new(StringComparer.OrdinalIgnoreCase);
    private static bool isInitialized = false;

    public static void OnBeforePatch() { AmDaemonReady.RunWhenReady(Initialize); }

    private static void Initialize()
    {
        // 按文件名合并根目录和数据包 / option 包里的 LocalAssets，Windows 下文件名不区分大小写
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in LocalAssets.EnumerateFiles(buttonDir, "*.png")) {
            files[Path.GetFileName(file)] = file;
        }

        if (files.Count == 0) {
            MelonLogger.Msg($"[CustomButton] buttonDir not found: {buttonDir}");
            return;
        }

        // load button textures to dict
        try {
            foreach (var file in files.Values) {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var bytes = File.ReadAllBytes(file);
                var tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
                spriteDict[fileName] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            MelonLogger.Msg($"[CustomButton] Loaded {spriteDict.Count} Textures");
            isInitialized = true;
        } catch (Exception e) {MelonLogger.Msg($"[CustomButton] Error in Initialize: {e}");}
    }

    // 通用获取按钮贴图的方法，对于大多数按钮都适用
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ButtonControllerBase), "LoadDefaultResources")]
    public static void LoadDefaultResourcesPrefix()
    {
        if (!isInitialized) return;
        try {
            // 通过反射获取FlatButtonParam字段
            var flatButtonParamField = typeof(ButtonControllerBase)
                .GetField("FlatButtonParam", BindingFlags.NonPublic | BindingFlags.Static);
            var flatButtonParams = flatButtonParamField.GetValue(null) as ButtonControllerBase.ButtonInformation[];   
            // 遍历并替换贴图
            for (int i = 0; i < flatButtonParams.Length; i++) {
                var buttonInfo = flatButtonParams[i];
                var buttonName = Path.GetFileNameWithoutExtension(buttonInfo.FileName);
                if (spriteDict.TryGetValue(buttonName, out var sprite)) {
                    // 直接修改贴图
                    buttonInfo.Image = sprite;
                } else {
                    // 加载原始贴图
                    buttonInfo.Image = Resources.Load<Sprite>(buttonInfo.FileName);
                }
                flatButtonParams[i] = buttonInfo; // 保存修改
            }
            // 处理箭头贴图
            var arrowSpriteField = typeof(ButtonControllerBase)
                .GetField("ArrowSprite", BindingFlags.NonPublic | BindingFlags.Static);
            var arrowSelectorSpriteField = typeof(ButtonControllerBase)
                .GetField("ArrowSelectorSprite", BindingFlags.NonPublic | BindingFlags.Static);
            arrowSpriteField.SetValue(null, Resources.Load<Sprite>("Common/Sprites/Button/UI_CMN_Arrow"));
            arrowSelectorSpriteField.SetValue(null, Resources.Load<Sprite>("Common/Sprites/Button/UI_MDS_Btn_Arrow"));
            // 阻止原函数执行
            var loadedField = typeof(ButtonControllerBase)
                .GetField("_isFlatButtonLoaded", BindingFlags.NonPublic | BindingFlags.Static);
            loadedField.SetValue(null, true);
        } catch (Exception e) {MelonLogger.Msg($"[CustomButton] {e}");}
    }

    // ButtonManager会独立加载贴图为EntryButton，需要单独处理
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ButtonManager), "Initialize")]
    public static void ButtonManagerInitializePostfix(ButtonManager __instance)
    {
        if (!isInitialized) return;
        try {
            // 通过反射获取_containers字段
            var containersField = typeof(ButtonManager).GetField("_containers", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            var containers = containersField.GetValue(__instance);
            // 获取Container类
            var containerType = typeof(ButtonManager).GetNestedType("Container", 
                BindingFlags.NonPublic);
            var spritePathField = containerType.GetField("SpritePath");
            var spriteField = containerType.GetField("Sprite");
            // 获取Values属性
            var valuesMethod = containers.GetType().GetProperty("Values").GetGetMethod();
            var values = valuesMethod.Invoke(containers, null) as System.Collections.IEnumerable;
            // 遍历所有容器并修改贴图
            foreach (var container in values) {
                var spritePath = spritePathField.GetValue(container) as string;
                var buttonName = Path.GetFileNameWithoutExtension(spritePath);
                if (spriteDict.TryGetValue(buttonName, out var sprite)) {
                    spriteField.SetValue(container, sprite);
                }
            }
        } catch (Exception e) {MelonLogger.Msg($"[CustomButton] {e}");}
    }
}