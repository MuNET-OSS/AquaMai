using AquaMai.Config.Attributes;
using AquaMai.Core.Attributes;
using AquaMai.Core.Helpers;
using HarmonyLib;
using MAI2.Util;
using Manager;
using MelonLoader;
using Monitor;
using Process;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace AquaMai.Mods.Fancy;

[ConfigSection(
    "仿旧框下一曲目随机图像",
    zh: "模仿旧框 FiNALE 在下一首曲目前显示随机提示图",
    en: "Shows random tips image before next track, just like the good ol' FiNALE")]
[EnableGameVersion(22000)]
public class NextTrackTips
{
    [ConfigEntry(
        zh: "随机提示图目录，图像格式为 png",
        en: "Tips image directory, only png images are supported")]
    private static readonly string TipsDirectory = "LocalAssets/Tips";

    private static readonly List<Sprite> _nextTrackSprites = [];

    private static bool _timeCounterChanged = false;

    private static readonly CommonWindow[] _hackyWindows = new CommonWindow[2];
    private static IMessageMonitor[] _genericMonitorRefs = new IMessageMonitor[2];

    [HarmonyPrepare]
    public static bool Initialize()
    {
        var resolvedDir = FileSystem.ResolvePath(TipsDirectory);
        if (!Directory.Exists(resolvedDir))
        {
            MelonLogger.Error($"[NextTrackTips] Tips directory does not exist: {resolvedDir}");
            return false;
        }

        var tipImgs = Directory.GetFiles(resolvedDir, "*.png", SearchOption.TopDirectoryOnly);

        foreach (var tipImgPath in tipImgs)
        {
            try
            {
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.LoadImage(File.ReadAllBytes(tipImgPath));
                _nextTrackSprites.Add(Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f)));
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[NextTrackTips] Failed to load image {tipImgPath}: {e}");
            }
        }

        if (_nextTrackSprites.Count < 1)
        {
            MelonLogger.Error($"[NextTrackTips] Tips directory seems empty or cannot load all images: {resolvedDir}");
            return false;
        }

        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GenericProcess), "OnStart")]
    public static void GenericProcess_OnStart_Postfix(GenericMonitor[] ____monitors)
    {
        _genericMonitorRefs = ____monitors;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GenericProcess), "OnRelease")]
    public static void GenericProcess_OnRelease_Postfix()
    {
        _genericMonitorRefs = new IMessageMonitor[2];
    }

    private static CommonWindow InitializeCommonWindowObject(CommonWindow prefab, Transform parent, int monitorIndex)
    {
        var window = UnityEngine.Object.Instantiate(prefab, parent);

        window.Prepare(
            _genericMonitorRefs[monitorIndex],
            DB.WindowMessageID.NextTrackTips01,
            DB.WindowPositionID.Middle,
            Vector3.zero,
            new WindowParam
            {
                changeSize = true,
                sizeID = DB.WindowSizeID.LargeHorizontal,
                hideTitle = true,
                replaceText = true,
                text = "",
                directSprite = true,
                sprite = _nextTrackSprites[UnityEngine.Random.Range(0, _nextTrackSprites.Count)]
            }
        );

        // Some hacks to force the layout and "fix" spacing
        var winLayout = window.transform.Find("IMG_Window").gameObject.GetComponent<HorizontalLayoutGroup>();
        winLayout.spacing = 0.0f;
        winLayout.padding = new RectOffset(40, 40, 40, 40);

        return window;
    }

    #region NextTrackProcess Patch
    private static bool CheckNextTrackProcess(NextTrackProcess.NextTrackMode mode)
    {
        return mode != NextTrackProcess.NextTrackMode.FreedomTimeup && mode != NextTrackProcess.NextTrackMode.NeedAwake && mode != NextTrackProcess.NextTrackMode.GotoEnd;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NextTrackProcess), "ProcessingProcess")]
    public static void ProcessingProcess_Postfix(NextTrackProcess.NextTrackMode ____mode, ref float ____timeCounter)
    {
        if (CheckNextTrackProcess(____mode) && !_timeCounterChanged)
        {
            ____timeCounter = 5f;
            _timeCounterChanged = true;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NextTrackProcess), "OnStart")]
    public static void OnStart_Postfix(NextTrackMonitor[] ____monitors, NextTrackProcess.NextTrackMode ____mode)
    {
        if (!CheckNextTrackProcess(____mode))
            return;

        var commonWindowPref = Resources.Load<GameObject>("Process/Generic/GenericProcess").transform.Find("Canvas/Main/MessageRoot/HorizontalSplitWindow").gameObject.GetComponent<CommonWindow>();

        for (int i = 0; i < ____monitors.Length; ++i)
        {
            var currUser = Singleton<UserDataManager>.Instance.GetUserData(i);
            if (currUser == null || !currUser.IsActiveUser)
                continue;

            var mainCanvas = ____monitors[i].transform.Find("Canvas/Main");
            _hackyWindows[i] = InitializeCommonWindowObject(commonWindowPref, mainCanvas.transform, i);

            // Play the sound effects and voice line
            SoundManager.PlaySE(Mai2.Mai2Cue.Cue.JINGLE_NEXT_TRACK, i);

            Mai2.Voice_Partner_000001.Cue nextTrackVoice = UnityEngine.Random.Range(0, 2) == 0 ? Mai2.Voice_Partner_000001.Cue.VO_000151 : Mai2.Voice_Partner_000001.Cue.VO_000152;
            if (GameManager.MusicTrackNumber + 1U == GameManager.GetMaxTrackCount())
                nextTrackVoice = Mai2.Voice_Partner_000001.Cue.VO_000153;
            SoundManager.PlayPartnerVoice(nextTrackVoice, i);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NextTrackProcess), "OnLateUpdate")]
    public static void OnLateUpdate_Postfix(NextTrackMonitor[] ____monitors)
    {
        for (int i = 0; i < ____monitors.Length; ++i)
            _hackyWindows[i]?.UpdateView(GameManager.GetGameMSecAdd());
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NextTrackProcess), "StartFadeIn")]
    public static void StartFadeIn_Postfix(NextTrackMonitor[] ____monitors)
    {
        for (int i = 0; i < ____monitors.Length; ++i)
            _hackyWindows[i]?.Close();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NextTrackProcess), "OnRelease")]
    public static void OnRelease_Prefix(NextTrackMonitor[] ____monitors)
    {
        for (int i = 0; i < ____monitors.Length; ++i)
        {
            if (_hackyWindows[i] != null)
            {
                UnityEngine.Object.Destroy(_hackyWindows[i]);
                _hackyWindows[i] = null;
            }
        }

        _timeCounterChanged = false;
    }
    #endregion

    #region KaleidxScopeFadeProcess Patch
    [EnableGameVersion(25000, noWarn: true)]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(KaleidxScopeFadeProcess), "OnStart")]
    public static void KS_OnStart_Postfix(ProcessBase ___toProcess, List<KaleidxScopeFadeController> ___mainControllerList)
    {
        if (___toProcess.GetType() != typeof(MusicSelectProcess) || GameManager.MusicTrackNumber < 2)  // WTF SBGA???
            return;

        var commonWindowPref = Resources.Load<GameObject>("Process/Generic/GenericProcess").transform.Find("Canvas/Main/MessageRoot/HorizontalSplitWindow").gameObject.GetComponent<CommonWindow>();

        // WTF SBGA?
        for (int i = 0; i < ___mainControllerList.Count; ++i)
        {
            var currUser = Singleton<UserDataManager>.Instance.GetUserData(i);
            if (currUser == null || !currUser.IsActiveUser)
                continue;

            _hackyWindows[i] = InitializeCommonWindowObject(commonWindowPref, ___mainControllerList[i].transform, i);

            // Play the sound effects and voice line
            SoundManager.PlaySE(Mai2.Mai2Cue.Cue.JINGLE_NEXT_TRACK, i);

            Mai2.Voice_Partner_000001.Cue nextTrackVoice = UnityEngine.Random.Range(0, 2) == 0 ? Mai2.Voice_Partner_000001.Cue.VO_000151 : Mai2.Voice_Partner_000001.Cue.VO_000152;
            // WTF SBGA??
            if (GameManager.MusicTrackNumber == GameManager.GetMaxTrackCount())
                nextTrackVoice = Mai2.Voice_Partner_000001.Cue.VO_000153;
            SoundManager.PlayPartnerVoice(nextTrackVoice, i);
        }
    }

    [EnableGameVersion(25000, noWarn: true)]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(KaleidxScopeFadeProcess), "OnLateUpdate")]
    public static void KS_OnLateUpdate_Postfix(List<KaleidxScopeFadeController> ___mainControllerList, KaleidxScopeFadeState ___stateMachine)
    {
        for (int i = 0; i < ___mainControllerList.Count; ++i)
            _hackyWindows[i]?.UpdateView(GameManager.GetGameMSecAdd());
    }

    [EnableGameVersion(25000, noWarn: true)]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(KaleidxScopeFadeProcess), "StartFadeIn")]
    public static void KS_StartFadeIn_Postfix(List<KaleidxScopeFadeController> ___mainControllerList)
    {
        for (int i = 0; i < ___mainControllerList.Count; ++i)
            _hackyWindows[i]?.Close();
    }

    [EnableGameVersion(25000, noWarn: true)]
    [HarmonyPrefix]
    [HarmonyPatch(typeof(KaleidxScopeFadeProcess), "OnRelease")]
    public static void KS_OnRelease_Prefix(List<KaleidxScopeFadeController> ___mainControllerList)
    {
        for (int i = 0; i < ___mainControllerList.Count; ++i)
        {
            if (_hackyWindows[i] != null)
            {
                UnityEngine.Object.Destroy(_hackyWindows[i]);
                _hackyWindows[i] = null;
            }
        }
    }
    #endregion
}
