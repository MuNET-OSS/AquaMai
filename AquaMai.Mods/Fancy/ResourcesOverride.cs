using System;
using System.Collections.Generic;
using System.IO;
using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace AquaMai.Mods.Fancy;

[ConfigSection(
    name: "力大砖飞",
    en: "[Dangerous] Full-scene texture / sprite replacement. Static injection.",
    zh: "【危险功能】适用于便捷魔改的自定义全场景图片。警告：可能对游戏造成未知性能影响，可能与其他模组冲突？")]
public class ResourcesOverride
{
    [ConfigEntry(name: "资源目录")]
    private static string skinsDir = "LocalAssets/ResourcesOverride";

    private static readonly Dictionary<string, Sprite> SpritePool = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, Texture2D> TexturePool = new Dictionary<string, Texture2D>();

    public static void OnBeforePatch()
    {
        // 目录要到 amdaemon 就绪后才齐（option 包这时才挂载），所以推迟到就绪后再扫描
        AmDaemonReady.RunWhenReady(LoadSprites);
    }

    private static void LoadSprites()
    {
        // 目录都不存在时按根目录建一个，方便用户往里放图
        var rootDir = LocalAssets.ResolvePath(skinsDir);
        if (!string.IsNullOrEmpty(rootDir) && !Directory.Exists(rootDir))
        {
            Directory.CreateDirectory(rootDir);
            return;
        }

        // 按文件名合并根目录和数据包 / option 包里的 LocalAssets，Windows 下文件名不区分大小写
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in LocalAssets.EnumerateFiles(skinsDir, "*.png"))
        {
            files[Path.GetFileName(file)] = file;
        }

        foreach (var file in files.Values)
        {
            try {
                byte[] data = File.ReadAllBytes(file);
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                if (tex.LoadImage(data)) {
                    var name = Path.GetFileNameWithoutExtension(file).ToLower();
                    var sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    sprite.name = name;
                    tex.name = name;

                    SpritePool[name] = sprite;
                    TexturePool[name] = tex;
                }
            } catch (Exception e) { MelonLogger.Error($"Failed to load custom skin '{Path.GetFileName(file)}': {e.Message}"); }
        }
    }


    // 渲染底层


    [HarmonyPatch(typeof(Graphic), "Rebuild")]
    [HarmonyPrefix]
    private static void OnGraphicRebuild(Graphic __instance)
    {
        if (__instance == null) return;

        // 处理 Image 组件
        if (__instance is Image img && img.sprite != null)
        {
            if (SpritePool.TryGetValue(img.sprite.name.ToLower(), out var customSprite))
            {
                if (img.sprite != customSprite)
                {
                    img.sprite = customSprite;
                    // 强制恢复
                    img.canvasRenderer.SetColor(Color.white);
                }
            }
        }
        // 处理RawImage
        else if (__instance is RawImage rImg && rImg.texture != null)
        {
            if (TexturePool.TryGetValue(rImg.texture.name.ToLower(), out var customTex))
            {
                if (rImg.texture != customTex)
                {
                    rImg.texture = customTex;
                    rImg.canvasRenderer.SetColor(Color.white);
                }
            }
        }
    }


    //全量暴力同步


    [HarmonyPatch(typeof(CanvasScaler), "OnEnable")]
    [HarmonyPostfix]
    private static void OnCanvasEnable(CanvasScaler __instance)
    {
        // 立即扫描
        var images = __instance.GetComponentsInChildren<Image>(true);
        foreach (var img in images)
        {
            if (img.sprite != null && SpritePool.TryGetValue(img.sprite.name.ToLower(), out var s))
                img.sprite = s;
        }

        var rImages = __instance.GetComponentsInChildren<RawImage>(true);
        foreach (var rImg in rImages)
        {
            if (rImg.texture != null && TexturePool.TryGetValue(rImg.texture.name.ToLower(), out var t))
                rImg.texture = t;
        }
    }
}
/*
这是一大坨屎山，原理上和 CustomSkinsPlus 差不多？
后续补：功能上差不多，原理上大概完全不一样了。
这个东西可能有点危险，毕竟是静态劫持所有图片赋值操作，理论上会影响性能
不过好处是可以彻底更换大概几乎可能的所有图片资源
Powered By AkiACG Team
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣠⣠⣤⣄⣀⣀⣀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠈⠉⠛⠻⢿⣿⣿⣷⣦⣄⡀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠉⠻⣿⣿⣿⣿⣦⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠙⣿⣿⣿⣷⡀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠈⠻⣿⣿⡇⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢀⣀⣠⣤⣤⣤⣴⣶⣶⣦⣤⣤⣤⣤⣀⣀⠹⣿⡇⣀⣀⣀⣀⣀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢀⣠⣤⣶⣶⣦⣤⣄⡀⢀⡤⠶⠿⠿⠟⢛⣛⠿⠿⢿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣇⢋⣼⣿⣿⣿⣿⣿⣿⣿⣶⡦⢤⣀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⢀⣠⣾⣿⣿⣿⣿⣿⣿⣿⣿⣿⣷⣄⠀⠀⠀⠀⠀⣁⣤⣶⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣷⣦⣌⡙⠲⣤⣀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⣰⣿⣿⣿⢟⣭⣶⣶⣶⣮⣝⢿⣿⣿⣿⣧⠀⣠⣶⣿⣿⣿⣿⣿⣿⣿⣿⡿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡿⠻⢿⣿⣿⣿⣿⣿⣿⣷⣌⠻⣷⣦⣀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⣼⣿⣿⣿⣿⡸⠟⣛⣛⢿⣿⣿⣧⢻⣿⣿⣿⣇⢻⣿⣿⣿⡿⢋⣸⣿⣿⣿⣤⣾⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣷⣷⣶⣿⣿⣿⣿⣿⣿⣿⣿⣧⡘⢿⣿⣷⣄⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⢸⣿⣿⣿⣿⣿⣿⣾⣿⣿⡇⣻⣿⣿⢸⣿⣿⣿⣿⢸⣿⣿⠟⣠⣿⣿⣿⡿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡟⢻⣿⣿⣿⣿⣿⣿⣷⡈⢿⣿⣿⣷⣄⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⢸⣿⣿⣿⣿⣿⣿⣿⣿⢩⣾⠟⣫⣵⣿⣿⣿⣿⣿⢸⡿⢃⣼⣿⣿⣿⡟⠀⢸⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡈⢿⣿⣿⣿⣿⣿⣿⣷⡈⢿⣿⣿⣿⣧⡀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠈⢿⣿⣿⣿⣿⣿⣿⣿⣦⠭⣭⣿⣿⣿⣿⣿⣿⡟⡼⢡⣾⣿⣿⣿⡟⡴⠀⣿⣿⣿⣿⣿⣿⠉⣿⣿⣿⠉⣿⣿⣿⣿⣿⣿⣿⣷⠘⣿⣿⡿⢿⣿⣿⣿⣧⠘⣿⣿⣿⣿⣷⡄⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠈⠻⣿⣿⣿⣿⣿⣿⣇⠾⢆⣽⣿⣿⣿⣿⠟⡴⢡⣿⣿⣿⣿⡟⣼⡇⢸⣿⣿⣿⣿⣿⣿⢰⣿⣿⣿⢠⢻⣿⣿⣿⣿⣿⡿⢟⣃⡭⠶⢚⣡⣿⣿⣿⣿⡆⢻⣿⣿⣿⣿⣿⡄⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠈⠙⠿⣿⣿⣿⣿⣿⣿⣿⡿⢛⣋⣵⡾⢠⣿⣿⣿⣿⡿⣰⣿⠀⣿⣿⣿⣿⣿⣿⣿⢸⣿⣿⡇⣿⡜⣿⣿⣿⣿⣿⣧⣭⣴⡀⢟⣋⣭⠙⣿⣿⣿⣷⠘⣿⣿⣿⣿⣿⣿⡄⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠈⠉⢩⣥⣶⣬⣍⣼⣿⣿⠃⣾⣿⣿⢛⣿⢣⣿⣿⢠⣿⣿⣿⣿⣿⣿⣿⢸⣿⣿⡇⣿⡧⠿⠿⣿⣿⣿⣿⡏⠵⠚⢋⣥⣶⣾⣿⣿⣿⣿⡄⣿⣿⣿⣿⣿⠙⢿⡄⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣼⣿⣿⣿⣿⣿⣿⡟⢰⣉⣭⠄⣤⣆⣾⣿⣷⢠⣿⣿⣿⣿⣿⣿⣿⠘⣿⣿⢹⣿⣿⡔⣶⣶⣤⣬⣉⡙⠻⢧⢸⣿⣿⣿⣿⣿⣿⣿⡇⢸⣿⣿⣿⣿⡈⠀⠑⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢠⣿⣿⣿⣿⣿⣿⣿⠃⣾⣿⣏⣴⡿⣼⣿⣿⣿⢸⣿⣿⣿⣿⣿⣿⣿⡇⣿⣿⣸⣿⣿⣷⢻⣿⣿⣿⣿⣿⣿⣶⠘⣿⣿⣿⣿⣿⣿⣿⡇⢸⣿⣿⣿⣿⡇⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢸⣿⣿⣿⣿⣿⣿⣿⠀⠛⠛⠚⠘⠓⠿⠿⠿⣯⢈⡛⢹⣿⣿⣿⣿⣿⣧⢿⡏⣿⢿⣿⣿⣯⢿⡿⣿⣿⣿⣿⣿⠀⣿⣿⣿⣿⣿⣿⣿⡇⢸⣿⣿⣿⣿⡇⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣿⡏⢸⣿⣿⣿⣿⣟⠀⠀⣠⣄⣀⡤⣀⣀⣀⣀⡀⠉⠈⣿⣿⣿⣿⣿⣿⡸⣧⣿⡿⠉⠉⠉⠈⠁⠈⠉⠉⠙⠛⠀⣿⣿⣿⣿⣿⣿⣿⡇⢸⣿⣿⣿⣿⡇⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣿⠠⠘⣿⣿⣿⣿⣿⠀⢠⣿⣿⣸⢱⣿⣿⣿⣿⣿⣧⠸⣎⢿⣿⣿⣿⣿⣧⢹⣿⣷⡖⣴⣶⣶⣶⣶⣦⢰⣄⢠⠀⣿⣿⣿⣿⣿⣿⣿⠁⣿⣿⣿⣿⣿⠁⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⡄⡿⠀⣀⠹⣿⣿⣿⣿⡄⢸⣿⣿⡏⣿⣿⣿⣿⣿⣿⣿⡄⢻⣷⡹⢿⣿⡿⣿⣞⣿⣿⣼⣿⣿⣿⣿⣿⣿⡦⣝⠲⢘⣿⣿⣿⣿⣿⣿⠏⠀⣿⣿⣿⣿⡟⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠘⠀⠀⠀⣿⣦⠙⣿⣿⣿⣷⠘⣿⣿⡿⣿⣿⣿⣿⣿⣿⣿⢷⣷⣿⣿⣷⣽⣓⣐⣭⣬⡿⣿⣿⣿⣿⣿⣿⣿⣇⣿⣷⢸⣿⣿⡟⢻⣿⠏⡴⢠⣿⣿⣿⣿⠁⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣿⠿⣷⣌⠻⡟⠻⣇⢻⣿⣧⢻⣿⣿⣿⣿⣿⡟⣼⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣹⣿⡇⣾⣿⠟⠀⡼⢃⣾⠇⣸⣿⣿⣿⡟⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢹⢠⡙⢿⣷⣤⡄⣬⣑⣙⣿⣷⣝⡻⠿⢿⣫⣼⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣮⡻⢿⣿⡿⢟⣵⣿⡟⣸⠟⣡⠎⢀⣴⣿⠋⣼⣿⣿⣿⣿⠇⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠘⢸⡇⣦⣉⡛⠳⠘⢿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣯⣭⣭⣭⣭⣭⣍⣻⣿⣿⣿⣷⡶⣾⣿⣿⠏⣈⠅⣼⡿⠖⣁⠘⣡⣾⣿⠛⢿⣿⣿⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠓⢿⣿⠀⠱⣌⠛⢿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣷⣽⣿⣿⡿⣿⣷⣿⣿⣿⣿⣿⣿⣿⣿⣵⣿⣩⣬⡴⢂⣾⣿⣿⣿⡿⠁⠀⢸⣿⡇⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢸⣿⠀⠀⠈⠿⣦⣌⡙⠻⠿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡿⠟⢋⡡⢴⣿⣿⣿⡿⠋⠀⠀⠀⢸⣿⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢸⣿⠀⠀⠀⠀⠈⠻⢿⣿⣶⣶⣬⣍⠙⠛⣛⣋⣉⠹⡏⠉⣉⣉⣛⣛⣛⣛⡛⠉⢥⣤⣶⠿⠋⢂⣾⣿⠟⠋⠀⠀⠀⠀⠀⢸⡿⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠸⠇⠀⠀⠀⠀⠀⠀⠀⠈⠙⠻⠿⠅⠈⢿⣿⣿⠏⣼⣷⡀⣿⣿⣿⣿⣿⣿⣿⣇⠘⠋⠁⠀⠀⠚⠉⠀⠀⠀⠀⠀⠀⠀⠀⠀⡇⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠻⠿⢦⡍⢱⡇⣿⣿⣿⡿⠿⠟⠛⢋⣠⡀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣠⣒⡖⠶⣤⠇⣼⠇⣎⣉⣤⣴⣶⣾⣿⣿⣿⣿⣦⡀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣠⡾⢿⣿⣿⣶⣤⣅⠙⡂⠿⠿⣿⣿⣿⣿⣿⣿⣿⡏⢹⠃⡤⡀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢀⢸⡇⢸⣿⣿⣿⣿⣿⠀⣧⢰⣶⣤⣤⣉⣙⣿⣿⣿⡇⡈⢰⣿⣿⡄⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢸⠀⡇⢸⣿⣿⣿⣿⣿⢸⣿⢸⣿⣿⣿⣿⣿⣿⣿⣿⠀⠀⣿⣿⣿⣿⡄⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣞⡇⠀⢸⣿⣿⣿⣿⣿⢸⡇⢸⣿⣿⣿⣿⣿⣿⣿⣿⠀⢀⣿⣿⣿⣿⣿⡀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢀⣿⣷⠀⢸⣿⣿⣿⣿⣿⢸⡇⢸⣿⣿⣿⣿⣿⣿⣿⣿⡆⢸⣿⣿⣿⣿⣿⣧⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢰⣿⣿⡆⠘⣿⣿⣿⣿⣟⢸⡇⢼⣿⣿⣿⣿⣿⣿⣿⣿⣇⠸⣿⣿⣿⣿⣿⣿⣆⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢸⣿⣿⣿⠀⣿⣿⣿⣿⣿⢸⡇⢸⣿⣿⣿⣿⣿⣿⣿⣿⣿⠀⣿⣿⣿⣿⣿⣿⣿⡄⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣿⣿⣿⣿⡄⢹⣿⣿⣿⣿⢸⡇⢸⣿⣿⣿⣿⣿⣿⣿⣿⣿⡄⢻⣿⣿⣿⣿⣿⣿⣷⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀
*/
