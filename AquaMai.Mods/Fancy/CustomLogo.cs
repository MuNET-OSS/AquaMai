using System;
using System.Collections.Generic;
using System.IO;
using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using HarmonyLib;
using MelonLoader;
using Monitor;
using Process;
using UnityEngine;
using UnityEngine.UI;

namespace AquaMai.Mods.Fancy;

[ConfigSection(
    name: "自定义 Logo",
    en: "Replace the \"SEGA\" and \"ALL.Net\" logos with custom ones.",
    zh: "用自定义的图片替换「SEGA」和「ALL.Net」的标志")]
public class CustomLogo
{
    [ConfigEntry(
        name: "SEGA Logo 目录",
        en: "Replace the \"SEGA\" logo with a random PNG image from this directory.",
        zh: "从此目录中随机选择一张 PNG 图片用于「SEGA」标志")]
    private static readonly string segaLogoDir = "LocalAssets/SegaLogo";

    [ConfigEntry(
        name: "ALL.Net Logo 目录",
        en: "Replace the \"ALL.Net\" logo with a random PNG image from this directory.",
        zh: "从此目录中随机选择一张 PNG 图片用于「ALL.Net」标志")]
    private static readonly string allNetLogoDir = "LocalAssets/AllNetLogo";

    [ConfigEntry(
        name: "Logo 全屏幕",
        en: "FullScreen the logo for the bottom screen.",
        zh: "将标志全屏占满下屏幕")]
    private static readonly bool logoFullScreen = false;

    private readonly static List<Sprite> segaLogo = [];
    private readonly static List<Sprite> allNetLogo = [];

    public static void OnBeforePatch()
    {
        // 目录要到 amdaemon 就绪后才齐（option 包这时才挂载），所以推迟到就绪后再扫描
        AmDaemonReady.RunWhenReady(() =>
        {
            EnumSprite(segaLogo, segaLogoDir);
            EnumSprite(allNetLogo, allNetLogoDir);
        });
    }

    private static void EnumSprite(List<Sprite> collection, string configuredDir)
    {
        // 按文件名合并根目录和数据包 / option 包里的 LocalAssets，Windows 下文件名不区分大小写
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in LocalAssets.EnumerateFiles(configuredDir, "*.png"))
        {
            files[Path.GetFileName(file)] = file;
        }

        foreach (var file in files.Values)
        {
            var data = File.ReadAllBytes(file);
            var texture2D = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            if (texture2D.LoadImage(data))
            {
                collection.Add(Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f)));
            }
        }
    }

    [HarmonyPatch(typeof(AdvertiseProcess), "OnStart")]
    [HarmonyPostfix]
    private static void AdvProcessPostFix(AdvertiseMonitor[] ____monitors)
    {
        if (segaLogo.Count > 0)
        {
            var logo = segaLogo[UnityEngine.Random.Range(0, segaLogo.Count)];
            foreach (var monitor in ____monitors)
            {
                var go = monitor.transform.Find("Canvas/Main/SegaAllNet_LOGO/NUL_ADT_SegaAllNet_LOGO/SegaLogo");
                if (go == null)
                    go = monitor.transform.Find("Canvas/Main/UI_ADV_SegaAllNet/Null_all/SegaLogo");
                go.GetComponent<Image>().sprite = logo;
                if (!logoFullScreen) continue;
                var rect = go.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(1080, 1080);
                rect.offsetMin = new Vector2(-540, -540);
                rect.offsetMax = new Vector2(540, 540);
            }
        }

        if (allNetLogo.Count > 0)
        {
            var logo = allNetLogo[UnityEngine.Random.Range(0, allNetLogo.Count)];
            foreach (var monitor in ____monitors)
            {
                var go = monitor.transform.Find("Canvas/Main/SegaAllNet_LOGO/NUL_ADT_SegaAllNet_LOGO/AllNetLogo");
                if (go == null)
                    go = monitor.transform.Find("Canvas/Main/UI_ADV_SegaAllNet/Null_all/AllNetLogo");
                go.GetComponent<Image>().sprite = logo;
                if (!logoFullScreen) continue;
                var rect = go.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(1080, 1080);
                rect.offsetMin = new Vector2(-540, -540);
                rect.offsetMax = new Vector2(540, 540);
            }
        }
    }
}
