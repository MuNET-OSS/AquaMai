using System.Collections.Generic;
using System.IO;
using AquaMai.Config.Attributes;
using AquaMai.Core.Attributes;
using AquaMai.Core.Helpers;
using CriMana;
using HarmonyLib;
using MAI2.Util;
using Manager;
using MelonLoader;
using Monitor;
using Monitor.Game;
using UnityEngine;
using UnityEngine.Video;

namespace AquaMai.Mods.GameSystem.Assets;

[ConfigSection(
    name: "直读 PV",
    en: "Custom Play Song Background\nPriority: game source PV > local mp4 PV > jacket",
    zh: "自定义歌曲游玩界面背景\n优先级: 首先读游戏自带 PV, 然后读本地 mp4 格式 PV, 最后读封面",
    defaultOn: true)]
[EnableGameVersion(23000)]
public class MovieLoader
{
    [ConfigEntry(
        name: "游戏自带 PV",
        en: "Load Movie from game source",
        zh: "加载游戏自带的 PV")]
    private static bool loadSourceMovie = true;


    [ConfigEntry(
        name: "本地 MP4",
        en: "Load Movie from LocalAssets mp4 files",
        zh: "从 LocalAssets 中加载 MP4 文件作为 PV")]
    private static bool loadMp4Movie = true;


    [ConfigEntry(
        name: "MP4 路径",
        en: "MP4 files directory",
        zh: "加载 MP4 文件的路径")]
    private static readonly string movieAssetsDir = "LocalAssets";


    [ConfigEntry(
        name: "封面作为背景",
        en: "Use jacket as movie\nUse together with `LoadLocalImages`.",
        zh: "用封面作为背景 PV\n请和 `LoadLocalImages` 一起用")]
    private static bool jacketAsMovie = true;


    private static readonly Dictionary<string, string> optionFileMap = [];
    private static uint[] bgaSize = [0, 0];

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DataManager), "LoadMusicBase")]
    public static void LoadMusicPostfix(List<string> ____targetDirs)
    {
        foreach (var aDir in ____targetDirs)
        {
            if (!Directory.Exists(Path.Combine(aDir, "MovieData"))) continue;
            var files = Directory.GetFiles(Path.Combine(aDir, "MovieData"), "*.mp4");
            foreach (var file in files)
            {
                optionFileMap[Path.GetFileName(file)] = file;
            }
        }
    }

    private static VideoPlayer[] _videoPlayers = new VideoPlayer[2];
    private static bool _isReplaced = false;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameCtrl), "Initialize")]
    public static void LoadLocalBgaAwake(GameObject ____movieMaskObj, int ___monitorIndex)
    {
        _isReplaced = false;
        var music = Singleton<DataManager>.Instance.GetMusic(GameManager.SelectMusicID[0]);
        if (music is null) return;
        
        // Load source bga
        if (loadSourceMovie) {
            var moviePath = Singleton<OptionDataManager>.Instance.GetMovieDataPath($"{music.movieName.id:000000}") + ".dat";
            if (!moviePath.Contains("dummy")) return;
        }

        // Load localasset mp4 bga
        var mp4Exists = false;
        var mp4Path = "";
        if (loadMp4Movie) {
            var fileName = $"{music.movieName.id:000000}.mp4";
            // 游戏数据目录里的 MovieData 优先，其次才是配置的目录（可能分散在根目录和数据包 / option 包里）
            if (!optionFileMap.TryGetValue(fileName, out mp4Path))
            {
                mp4Path = LocalAssets.ResolveFile(movieAssetsDir, fileName);
            }
            mp4Exists = mp4Path != null;
        }

        // Load jacket
        Texture2D jacket = null;
        if (jacketAsMovie) {
            jacket = LoadLocalImages.GetJacketTexture2D(music.movieName.id);
            if (jacket is null) {
                var filename = $"Jacket/UI_Jacket_{music.movieName.id:000000}.png";
                jacket = AssetManager.Instance().GetJacketTexture2D(filename);
            }
        }

        if (!mp4Exists && jacket is null) {
            MelonLogger.Msg($"[MovieLoader] No jacket or bga for {music.movieName.id:000000}");
            return;
        }
        
        _isReplaced = true;

        if (mp4Exists)
        {
            if (_videoPlayers[___monitorIndex] == null)
            {
# if DEBUG
                MelonLogger.Msg("Init _videoPlayer");
# endif
                _videoPlayers[___monitorIndex] = ____movieMaskObj.AddComponent<VideoPlayer>();
            }
# if DEBUG
            else
            {
                MelonLogger.Msg("_videoPlayer already exists");
            }
# endif
            _videoPlayers[___monitorIndex].url = mp4Path;
            _videoPlayers[___monitorIndex].playOnAwake = false;
            _videoPlayers[___monitorIndex].renderMode = VideoRenderMode.MaterialOverride;
            _videoPlayers[___monitorIndex].audioOutputMode = VideoAudioOutputMode.None;
            // 似乎 MaterialOverride 没法保持视频的长宽比，用 RenderTexture 的话放在 SpriteRenderer 里面会比较麻烦。
            // 所以就不保持了，在塞 pv 的时候自己转吧，反正原本也要根据 first 加 padding
        }

        var movie = ____movieMaskObj.transform.Find("Movie");

        // If I create a new RawImage component, the jacket will be not be displayed
        // I think it will be difficult to make it work with RawImage
        // So I change the material that plays video to default sprite material
        // The original player is actually a sprite renderer and plays video with a custom material
        var sprite = movie.GetComponent<SpriteRenderer>();
        if (mp4Exists)
        {
            sprite.material = new Material(Shader.Find("Sprites/Default"));
            _videoPlayers[___monitorIndex].targetMaterialRenderer = sprite;

            // 异步等待视频准备好，获取视频的长宽
            var player = _videoPlayers[___monitorIndex];
            player.prepareCompleted += (source) => {
                var vp = source as VideoPlayer;
                if (vp != null) {
                    var height = vp.height;
                    var width = vp.width;
                    // 按照比例缩放到(1080, 1080)
                    if (height > width) {
                        bgaSize = [1080, (uint)(1080.0 * width / height)];
                    } else {
                        bgaSize = [(uint)(1080.0 * height / width), 1080];
                    }
                    //MelonLogger.Msg($"[MovieLoader] {music.movieName.id:000000} {width}x{height} -> {bgaSize[0]}x{bgaSize[1]}"); //debug
                }
            };
        }
        else
        {
            sprite.sprite = Sprite.Create(jacket, new Rect(0, 0, jacket.width, jacket.height), new Vector2(0.5f, 0.5f));
            sprite.material = new Material(Shader.Find("Sprites/Default"));
            bgaSize = [1080, 1080];
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameMonitor), "SetMovieMaterial")]
    [EnableGameVersion(minVersion: 25500)]
    public static bool SetMovieMaterial(Material material, int ___monitorIndex)
    {
# if DEBUG
        MelonLogger.Msg("SetMovieMaterial");
# endif
        return !_isReplaced;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MovieController), "GetMovieHeight")]
    public static void GetMovieHeightPostfix(ref uint __result)
    {
        if (bgaSize[0] > 0) {
            __result = bgaSize[0];
            bgaSize[0] = 0;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MovieController), "GetMovieWidth")]
    public static void GetMovieWidthPostfix(ref uint __result)
    {
        if (bgaSize[1] > 0) {
            __result = bgaSize[1];
            bgaSize[1] = 0;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MovieController), "Play")]
    public static void Play(int frame)
    {
        foreach (var player in _videoPlayers)
        {
            if (player == null) continue;
            player.frame = frame;
            player.Play();
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MovieController), "Pause")]
    public static void Pause(bool pauseFlag)
    {
        foreach (var player in _videoPlayers)
        {
            if (player == null) continue;
            if (pauseFlag)
            {
                player.Pause();
            }
            else
            {
                player.Play();
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), "SetSpeed")]
    public static void SetSpeed(float speed)
    {
        foreach (var player in _videoPlayers)
        {
            if (player == null) continue;
            player.playbackSpeed = speed;
        }
    }
}