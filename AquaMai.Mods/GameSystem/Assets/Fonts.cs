using System.Collections.Generic;
using System.Linq;
using AquaMai.Config.Attributes;
using AquaMai.Core.Attributes;
using AquaMai.Core.Helpers;
using HarmonyLib;
using MelonLoader;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace AquaMai.Mods.GameSystem.Assets;

[ConfigSection(
    name: "自定义字体",
    en: "Use custom font(s) as fallback or fully replace the original game font.",
    zh: "使用自定义字体作为回退（解决中文字形缺失问题），或完全替换游戏原字体",
    defaultOn: true)]
[EnableGameVersion(23000)]
public class Fonts
{
    [ConfigEntry(
        name: "字体路径",
        en: """
            Font path(s).
            Use semicolon to separate multiple paths for a fallback chain.
            Microsoft YaHei Bold by default.
            """,
        zh: """
            使用分号分隔多个路径以构成 Fallback 链
            默认为微软雅黑 Bold
            """)]
    private static readonly string paths = "%SYSTEMROOT%/Fonts/msyhbd.ttc";

    [ConfigEntry(
        name: "作为回退字体",
        en: "Add custom font(s) as fallback, use original game font when possible.",
        zh: "将自定义字体作为游戏原字体的回退，尽可能使用游戏原字体")]
    private static readonly bool addAsFallback = true;

    [ConfigEntry(
        en: """
            Font path(s) specifically for SEGA_MaruGothicDB SDF.
            Use semicolon to separate multiple paths.
            If empty, uses the general paths above.
            """,
        zh: """
            SEGA_MaruGothicDB SDF 的单独字体路径
            使用分号分隔多个路径
            留空则使用上方的总设置
            """)]
    private static readonly string maruGothicPaths = "";

    [ConfigEntry(
        en: """
            Font path(s) specifically for SEGA_NewRodinN v2-EB_0 SDF.
            Use semicolon to separate multiple paths.
            If empty, uses the general paths above.
            """,
        zh: """
            SEGA_NewRodinN v2-EB_0 SDF 的单独字体路径
            使用分号分隔多个路径
            留空则使用上方的总设置
            """)]
    private static readonly string newRodinPaths = "";

    private static List<TMP_FontAsset> fontAssets = [];
    private static List<TMP_FontAsset> maruGothicFontAssets = [];
    private static List<TMP_FontAsset> newRodinFontAssets = [];
    private static readonly List<TMP_FontAsset> processedFonts = [];
    private static readonly Dictionary<string, TMP_FontAsset> fontAssetCache = new();

    private static List<TMP_FontAsset> LoadFonts(string pathsStr)
    {
        var resolvedPaths = pathsStr
            .Split(';')
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(FileSystem.ResolvePath);

        var result = new List<TMP_FontAsset>();
        foreach (var p in resolvedPaths)
        {
            if (fontAssetCache.TryGetValue(p, out var cached))
            {
                result.Add(cached);
                continue;
            }

            var font = new Font(p);
            if (font == null)
            {
                MelonLogger.Warning($"[Fonts] Font not found: {p}");
                continue;
            }

            var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 8192, 8192);
            asset.ReadFontAssetDefinition();
            fontAssetCache[p] = asset;
            result.Add(asset);
        }

        return result;
    }

    public static void OnBeforePatch()
    {
        fontAssets = LoadFonts(paths);

        if (!string.IsNullOrWhiteSpace(maruGothicPaths))
            maruGothicFontAssets = LoadFonts(maruGothicPaths);

        if (!string.IsNullOrWhiteSpace(newRodinPaths))
            newRodinFontAssets = LoadFonts(newRodinPaths);

        if (fontAssets.Count == 0 && maruGothicFontAssets.Count == 0 && newRodinFontAssets.Count == 0)
        {
            MelonLogger.Warning("[Fonts] No font loaded.");
        }
    }

    private static List<TMP_FontAsset> GetFallbackFonts(string fontName)
    {
        return fontName switch
        {
            "SEGA_MaruGothicDB SDF" when maruGothicFontAssets.Count > 0 => maruGothicFontAssets,
            "SEGA_NewRodinN v2-EB_0 SDF" when newRodinFontAssets.Count > 0 => newRodinFontAssets,
            _ => fontAssets
        };
    }

    [HarmonyPatch(typeof(TextMeshProUGUI), "Awake")]
    [HarmonyPostfix]
    public static void PostAwake(TextMeshProUGUI __instance)
    {
        var fallbackFonts = GetFallbackFonts(__instance.font.name);
        if (fallbackFonts.Count == 0) return;
        if (processedFonts.Contains(__instance.font)) return;

        if (!addAsFallback)
        {
            __instance.font.ClearFontAssetData();
        }
        ProcessFallback(__instance, fallbackFonts);

        processedFonts.Add(__instance.font);
    }

//     private static void ProcessReplacement(TextMeshProUGUI __instance)
//     {
// # if DEBUG
//         MelonLogger.Msg($"{__instance.font.name} {__instance.text}");
// # endif
//
//         var materialOrigin = __instance.fontMaterial;
//         __instance.font = replacementFontAsset;
//
// # if DEBUG
//         MelonLogger.Msg($"shaderKeywords {materialOrigin.shaderKeywords.Join()} {__instance.fontMaterial.shaderKeywords.Join()}");
// # endif
//         // if (materialOrigin != null)
//         // {
//         //     materialOrigin.SetTexture(ShaderUtilities.ID_MainTex, replacementFontAsset.atlasTexture);
//         //     __instance.fontMaterial = materialOrigin;
//         // }
//     }

    private static void ProcessFallback(TextMeshProUGUI __instance, List<TMP_FontAsset> fonts)
    {
        foreach (var fontAsset in fonts)
        {
            __instance.font.fallbackFontAssetTable.Add(fontAsset);
        }
    }
}
