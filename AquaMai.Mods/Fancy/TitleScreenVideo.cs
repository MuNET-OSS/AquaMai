using AquaMai.Config.Attributes;
using AquaMai.Core.Attributes;
using AquaMai.Core.Helpers;
using HarmonyLib;
using MAI2.Util;
using Manager;
using MelonLoader;
using Monitor;
using Process;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

namespace AquaMai.Mods.Fancy;

[ConfigSection(
    "标题画面视频",
    en: "Plays custom video on title screen, just like in the good ol' days",
    zh: "复刻 bud 代之前的标题界面视频动画")]
[EnableGameVersion(24000)]
public class TitleScreenVideo
{
    [ConfigEntry(
        en: "Title Video / Audio File Path (without file extensions, mp4 video and acb/awb audio are supported)",
        zh: "标题视音频文件路径，不包括文件后缀名（视频为 mp4 格式，音频为 acb/awb 格式")]
    private static readonly string VideoPath = "LocalAssets/DX_title";

    private static GameObject[] _movieObjects = new GameObject[2];
    private static VideoPlayer[] _videoPlayers = new VideoPlayer[2];

    private static List<string>[] _disabledCompoments = [[], []];

    private static bool _isVideoPrepared = false;
    private static bool _isAudioPrepared = false;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AdvertiseProcess), "OnStart")]
    public static void OnStart_Postfix(AdvertiseMonitor[] ____monitors)
    {
        var moviePref = Resources.Load<GameObject>("Process/AdvertiseCommercial/AdvertiseCommercialProcess").transform.Find("Canvas/Main/MovieMask").gameObject;

        for (int i = 0; i < ____monitors.Length; ++i)
        {
            var monitor = ____monitors[i];

            // Disable fade out cover on cir (and maybe future version?)
            if (GameInfo.GameVersion >= 26000)
                monitor.transform.Find("Canvas/Main/UI_ADV_Title/Null_all/out_cover")?.gameObject.SetActive(false);

            var titleLoop = monitor.transform.Find("Canvas/Main/UI_ADV_Title/Null_all/TitleLoop");

            // Disable all elements on the original title screen
            for (int j = 0; j < titleLoop.childCount; ++j)
            {
                var obj = titleLoop.GetChild(j).gameObject;
                if (obj.activeSelf)
                {
                    obj.SetActive(false);
                    _disabledCompoments[i].Add(obj.name);
                }
            }

            _movieObjects[i] = UnityEngine.Object.Instantiate(moviePref, titleLoop);
            _movieObjects[i].GetComponent<SpriteRenderer>().color = new Color(0, 0, 0, 0);

            _videoPlayers[i] = _movieObjects[i].AddComponent<VideoPlayer>();
            _videoPlayers[i].url = FileSystem.ResolvePath(VideoPath + ".mp4");
            _videoPlayers[i].playOnAwake = false;
            _videoPlayers[i].isLooping = false;
            _videoPlayers[i].renderMode = VideoRenderMode.MaterialOverride;
            _videoPlayers[i].audioOutputMode = VideoAudioOutputMode.None;

            var movieSprite = _movieObjects[i].transform.Find("Movie").gameObject.GetComponent<SpriteRenderer>();

            _videoPlayers[i].prepareCompleted += (source) =>
            {
                // Prevent autoplay
                source.Pause();
                source.time = 0;

                // Setting the video player size
                var vWidth = source.width;
                var vHeight = source.height;

                var calWidth = vHeight > vWidth ? (1080 * vWidth / vHeight) : 1080;
                var calHeight = vHeight > vWidth ? 1080 : (1080 * vHeight / vWidth);

                movieSprite.size = new Vector2(calWidth, calHeight);

                _isVideoPrepared = true;
            };

            _videoPlayers[i].errorReceived += (source, err) =>
            {
                _isVideoPrepared = false;

                MelonLogger.Error($"[TitleScreenVideo] Failed to load video file: {err}");
            };

            _videoPlayers[i].Prepare();

            var movieMaterial = new Material(Shader.Find("Sprites/Default"));
            movieSprite.material = movieMaterial;
            _videoPlayers[i].targetMaterialRenderer = movieSprite;
        }

        _isAudioPrepared = SoundManager.MusicPrepareForFileName(VideoPath);
        if (!_isAudioPrepared)
            MelonLogger.Warning("[TitleScreenVideo] Failed to load audio file, game's default title jingle will be played instead");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AdvertiseProcess), "OnUpdate")]
    public static void OnUpdate_Postfix(AdvertiseProcess.AdvertiseSequence ____state, AdvertiseMonitor[] ____monitors)
    {
        switch (____state)
        {
            case AdvertiseProcess.AdvertiseSequence.Logo:
                // Re-enable original title screen elements if the video is unavailable
                // yeah calling it early so the switch is unnoticeable
                if (!_isVideoPrepared)
                {
                    for (int i = 0; i < ____monitors.Length; ++i)
                    {
                        _movieObjects[i].SetActive(false);

                        var titleLoop = ____monitors[i].transform.Find("Canvas/Main/UI_ADV_Title/Null_all/TitleLoop");
                        foreach (string name in _disabledCompoments[i])
                            titleLoop.Find(name).gameObject.SetActive(true);

                        _disabledCompoments[i] = [];
                    }
                }
                break;
            case AdvertiseProcess.AdvertiseSequence.TransitionOut:
                if (_isVideoPrepared)
                {
                    for (int i = 0; i < ____monitors.Length; ++i)
                    {
                        // Stop yelling "maimai deluxe" I'm tired to hearing it
                        SoundManager.StopVoice(i);

                        _videoPlayers[i].Play();

                        if (_isAudioPrepared)
                        {
                            // Stop game's original title music and plays our own
                            SoundManager.StopJingle(i);
                            SoundManager.StartMusic();
                        }
                    }
                }
                break;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AdvertiseProcess), "LeaveAdvertise")]
    public static void LeaveAdvertise_Postfix()
    {
        if (_isAudioPrepared)
        {
            // Stop and unloads title music
            SoundManager.StopMusic();
            Singleton<SoundCtrl>.Instance.UnloadCueSheet(1);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AdvertiseProcess), "OnRelease")]
    public static void OnRelease_Prefix(AdvertiseMonitor[] ____monitors)
    {
        for (int i = 0; i < ____monitors.Length; ++i)
        {
            if (_videoPlayers[i] != null)
            {
                _videoPlayers[i].prepareCompleted -= null;
                _videoPlayers[i].errorReceived -= null;
                UnityEngine.Object.Destroy(_videoPlayers[i]);
                _videoPlayers[i] = null;
            }

            if (_movieObjects[i] != null)
            {
                UnityEngine.Object.Destroy(_movieObjects[i]);
                _movieObjects[i] = null;
            }
        }

        // Resets status
        _isVideoPrepared = false;
        _isAudioPrepared = false;
        _disabledCompoments = [[], []];
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AdvertiseMonitor), "AllStop")]
    public static bool Monitor_AllStop_Prefix()
    {
        // So uhh... When I was testing the feature, this method makes title screen suddently go black before transition
        // I don't like the sudden cutout so I disabled it, not sure about the side effect or compatibility though
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AdvertiseMonitor), "IsTitleAnimationEnd")]
    public static bool Monitor_IsTitleAnimationEnd_Prefix(ref bool __result, int ___monitorIndex)
    {
        if (!_isVideoPrepared)
            return true;

        __result = !_videoPlayers[___monitorIndex].isPlaying && _videoPlayers[___monitorIndex].frame >= (long) _videoPlayers[___monitorIndex].frameCount - 1;
        return false;
    }
}
