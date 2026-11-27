using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using AquaMai.Mods.Fix;
using AquaMai.Core.Helpers;
using AquaMai.Core.Resources;
using AquaMai.Mods.UX.PracticeMode.Libs;
using HarmonyLib;
using Manager;
using Monitor;
using Monitor.Game;
using Process;
using UnityEngine;
using AquaMai.Config.Attributes;
using AquaMai.Config.Types;
using AquaMai.Core.Attributes;
using MelonLoader;

namespace AquaMai.Mods.UX.PracticeMode;

[ConfigCollapseNamespace]
[ConfigSection(
    en: "Practice Mode.",
    name: "练习模式")]
[EnableGameVersion(23000)]
public class PracticeMode
{
    [ConfigEntry(
        name: "按键",
        en: "Key to show Practice Mode UI.",
        zh: "显示练习模式 UI 的按键")]
    public static readonly KeyCodeOrName key = KeyCodeOrName.Test;

    [ConfigEntry]
    public static readonly bool longPress = false;

    [ConfigEntry(
        name: "仅自由模式可用",
        en: "Only allow Practice Mode in Freedom Mode while time remains.",
        zh: "仅在自由模式且时间未耗尽时允许使用练习模式")]
    public static readonly bool onlyInFreedomMode = false;

    [ConfigEntry(
        name: "变速保持音高",
        en: "Preserve background music pitch when changing speed (0.5x-2.0x) in Practice Mode.",
        zh: "在练习模式中变速（0.5x-2.0x）时保持背景音乐音高不变")]
    public static readonly bool keepPitch = true;

    public static double repeatStart = -1;
    public static double repeatEnd = -1;
    public static float speed = 1;
    private static CriAtomExPlayer player;
    // Stock SoundCtrl creates a plain player for music (slot 2), using pool ID 0.
    // Only this player is ever redirected; effects and both monitors retain their pools.
    private const uint StretchPoolId = 0x4151504D; // "AQPM"
    private static CriAtomExStandardVoicePool stretchPool;
    private static bool gameActive, musicStarted, stretchRoute, legacyPitch, stretchFailed;
    private static int gameGeneration;

    // CRI registers the pool for disposal on module finalization. Share one pool across
    // songs: stopping a player does not guarantee that its voices have finished releasing.
    private static void InitializeStretchPool()
    {
        if (stretchPool != null && stretchPool.nativeHandle != IntPtr.Zero) return;
        stretchPool = null;
        CriAtomExStandardVoicePool candidate = null;
        try
        {
            candidate = new CriAtomExStandardVoicePool(2, 8, 96000, true, StretchPoolId);
            if (candidate.nativeHandle == IntPtr.Zero) throw new InvalidOperationException("No CRI voice pool");
            candidate.AttachDspTimeStretch();
            stretchPool = candidate;
        }
        catch (Exception ex)
        {
            FailStretch(ex);
            // Never published or selected, so no in-use voice can reference this pool.
            candidate?.Dispose();
        }
    }

    private static void FailStretch(Exception ex)
    {
        if (stretchFailed) return;
        stretchFailed = true;
        MelonLogger.Warning($"[PracticeMode] Keep pitch unavailable; using original playback: {ex.Message}");
    }

    private static void ApplyOriginal()
    {
        if (stretchRoute)
        {
            player.SetVoicePoolIdentifier(0);
            stretchRoute = false;
        }
        // The stock pool has no stretch DSP; fallback must not require any DSP setter.
        player.SetPitch((float)(1200 * Math.Log(speed, 2)));
        legacyPitch = speed != 1f;
    }

    private static void ApplyAudio(bool updateVoices)
    {
        if (!gameActive || isInAdvDemo || Shim.IsKaleidxScopeMode || player == null || !player.isAvailable) return;
        if (speed == 1f && !stretchRoute && !legacyPitch) return;
        bool wasStretch = stretchRoute;
        if (keepPitch && speed != 1f && speed >= 0.5f && speed <= 2f && !stretchFailed &&
            stretchPool != null && stretchPool.nativeHandle != IntPtr.Zero)
        {
            try
            {
                stretchRoute = true; // Also restore on a partial setter failure.
                player.SetVoicePoolIdentifier(stretchPool.identifier);
                player.SetPitch(0f);
                player.SetDspTimeStretchRatio(1f / speed);
                legacyPitch = false;
                if (updateVoices)
                {
                    if (!wasStretch) RestartMusicAtChartTime();
                    else player.UpdateAll();
                }
                return;
            }
            catch (Exception ex) { FailStretch(ex); }
        }
        ApplyOriginal();
        if (!updateVoices) return;
        if (wasStretch) RestartMusicAtChartTime();
        else player.UpdateAll();
    }

    private static void RestartMusicAtChartTime()
    {
        // A play/seek is already queued for the music player; BeforeMusicStart will apply parameters when PlayExecute runs.
        if (!SoundManager.PlayingCueInfoList[(int)SoundManager.PlayerID.Music].IsEmpty) return;

        // Do not restart music that has already stopped or finished playing.
        var status = player.GetStatus();
        if (status == CriAtomExPlayer.Status.Stop || status == CriAtomExPlayer.Status.PlayEnd) return;

        // Preserve paused state if music hasn't started yet (Init/Sync/Start/StartWait) or is currently paused.
        bool shouldPause = DebugFeature.Pause || !musicStarted || (status != CriAtomExPlayer.Status.Error && player.IsPaused());
        int seekMsec = musicStarted ? (int)Math.Max(0, CurrentPlayMsec) : 0;

        // SeekMusic queues a fresh, prepared voice. Do not reset notes, movies or the chart clock.
        SoundManager.SeekMusic(seekMsec);
        SoundManager.PauseMusic(shouldPause);
    }

    // SoundCtrl.Play resets player parameters immediately before Start, including after a seek.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CriAtomExPlayer), nameof(CriAtomExPlayer.Start))]
    private static void BeforeMusicStart(CriAtomExPlayer __instance)
    {
        if (ReferenceEquals(__instance, player)) ApplyAudio(false);
    }

    private static void RestoreMusicPlayer()
    {
        try
        {
            if (player == null || !player.isAvailable || (!stretchRoute && !legacyPitch)) return;
            if (stretchRoute)
            {
                try { player.Stop(true); }
                catch (Exception ex) { MelonLogger.Warning($"[PracticeMode] Failed to stop stretched music voice: {ex.Message}"); }
                player.SetVoicePoolIdentifier(0);
            }
            player.SetPitch(0f);
            player.UpdateAll();
            stretchRoute = legacyPitch = false;
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"[PracticeMode] Failed to restore music player: {ex.Message}");
        }
    }

    private static List<MovieMaterialMai2> movie;
    private static GameCtrl[] gameCtrl = new GameCtrl[2];
    public static bool keepNoteSpeed = false;
    
    private static void ClearPracticeEffects()
    {
        // 清除练习模式的状态，并恢复正常播放。
        if (ui != null)
        {
            UnityEngine.Object.Destroy(ui);
            ui = null;
        }
        if (repeatStart >= 0 || repeatEnd >= 0) ClearRepeat();
        if (speed != 1f || stretchRoute) SpeedReset();
        if (DebugFeature.Pause) TogglePause();
    }

    public static void SetRepeatEnd(double time)
    {
        if (repeatStart == -1)
        {
            MessageHelper.ShowMessage(Locale.RepeatStartTimeNotSet);
            return;
        }

        if (time < repeatStart)
        {
            MessageHelper.ShowMessage(Locale.RepeatEndTimeLessThenStartTime);
            return;
        }

        repeatEnd = time;
    }

    public static void ClearRepeat()
    {
        repeatStart = -1;
        repeatEnd = -1;
    }

    public static void TogglePause()
    {
        DebugFeature.Pause = !DebugFeature.Pause;
        if (!DebugFeature.Pause)
        {
            Seek(0);
        }
    }

    public static void GameCtrlResetOptionSpeed()
    {
        foreach (var g in gameCtrl)
        {
            try
            {
                g?.ResetOptionSpeed();
            }
            catch (NullReferenceException) {} // 忽略即可，因为SBGA的代码在ResetOptionSpeed内部没有做null检查，单刷的时候对2P那侧这个函数必定会抛NPE
        }
    }

    public static void SetSpeed()
    {
        DontRuinMyAccount.triggerForPracticeMode();
        
        ApplyAudio(true);
        
        movie?.ForEach(m => m.player?.SetSpeed(speed));
        GameCtrlResetOptionSpeed();
    }

    private static IEnumerator SetSpeedCoroutineInner(int generation)
    {
        yield return null;
        if (gameActive && generation == gameGeneration) SetSpeed();
    }

    public static void SetSpeedCoroutine()
    {
        SharedInstances.GameMainObject.StartCoroutine(SetSpeedCoroutineInner(gameGeneration));
    }

    public static void SpeedUp()
    {
        speed = (float)Math.Round(speed + .05f, 2);
        if (speed > 2)
        {
            speed = 2;
        }

        SetSpeed();
    }

    public static void SpeedDown()
    {
        speed = (float)Math.Round(speed - .05f, 2);
        if (speed < 0.05)
        {
            speed = 0.05f;
        }

        SetSpeed();
    }

    public static void SpeedReset()
    {
        speed = 1;
        SetSpeed();
    }

    public static void Seek(int addMsec)
    {
        // Debug feature 里面那个 timer 不能感知变速
        // 为了和魔改版本统一，polyfill 里面不修这个
        // 这里重新实现一个能感知变速的 Seek
        var msec = CurrentPlayMsec + addMsec;
        if (msec < 0)
        {
            msec = 0;
        }

        CurrentPlayMsec = msec;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameCtrl), "ForceNoteCollect")]
    private static void ForceNoteCollect(NotesManager ___NoteMng)
    {
        foreach (NoteData note in ___NoteMng.getReader().GetNoteList())
        {
            if (note != null && note.type.isConnectSlide())
            {
                note.isJudged = true;
            }
        }
    }

    public static double CurrentPlayMsec
    {
        get => NotesManager.GetCurrentMsec() - 91;
        set
        {
            DebugFeature.CurrentPlayMsec = value;
            SetSpeedCoroutine();
        }
    }

    public static PracticeModeUI ui;

    private static long prevFreedomModeMSec = -1; // 上一帧时，自由模式的剩余秒数

    [HarmonyPatch]
    public class PatchNoteSpeed
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(GameManager), "GetNoteSpeed");
            yield return AccessTools.Method(typeof(GameManager), "GetTouchSpeed");
        }

        public static void Postfix(ref float __result)
        {
            if (!keepNoteSpeed) return;
            __result /= speed;
        }
    }

    [HarmonyPatch(typeof(GameProcess), "OnStart")]
    [HarmonyPostfix]
    public static void GameProcessPostStart()
    {
        stretchFailed = false;
        gameActive = true;
        musicStarted = false;
        gameGeneration++;
        repeatStart = -1;
        repeatEnd = -1;
        speed = 1;
        ui = null;
        prevFreedomModeMSec = GameManager.IsFreedomMode ? GameManager.GetFreedomModeMSec() : -1;
    }

    [HarmonyPatch(typeof(GameProcess), "SetRelease")]
    [HarmonyPrefix]
    public static void GameProcessPreSetRelease()
    {
        gameActive = false;
        musicStarted = false;
        speed = 1;
        RestoreMusicPlayer();
    }

    [HarmonyPatch(typeof(GameProcess), "OnRelease")]
    [HarmonyPostfix]
    public static void GameProcessPostRelease()
    {
        gameActive = false;
        musicStarted = false;
        speed = 1;
        RestoreMusicPlayer();
        repeatStart = -1;
        repeatEnd = -1;
        ui = null;
        prevFreedomModeMSec = -1;
    }

    [HarmonyPatch(typeof(GameCtrl), "Initialize")]
    [HarmonyPostfix]
    public static void GameCtrlPostInitialize(GameCtrl __instance)
    {
        gameCtrl[__instance.MonitorIndex] =__instance;
    }

# if DEBUG
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GenericProcess), "OnUpdate")]
    public static void OnGenericProcessUpdate(GenericMonitor[] ____monitors)
    {
        if (Input.GetKeyDown(KeyCode.F11))
        {
            ____monitors[0].gameObject.AddComponent<PracticeModeUI>();
        }
    }
# endif

    [HarmonyPatch(typeof(GameProcess), "OnUpdate")]
    [HarmonyPostfix]
    public static void GameProcessPostUpdate(GameProcess __instance, GameMonitor[] ____monitors, bool ____startMusic)
    {
        musicStarted = ____startMusic;
        if (KeyListener.GetKeyDownOrLongPress(key, longPress) && ui is null && 
            (!onlyInFreedomMode || (GameManager.IsFreedomMode && GameManager.GetFreedomModeMSec() > 0))) // onlyInFreedomMode=true情况，则额外检查是否在练习模式内、且时间有剩余，如果不满足则不开启UI。
        {
            ui = ____monitors[0].gameObject.AddComponent<PracticeModeUI>();
        }
        
        if (onlyInFreedomMode && GameManager.IsFreedomMode)
        {
            var currentFreedomModeMSec = GameManager.GetFreedomModeMSec();
            if (prevFreedomModeMSec > 0 && currentFreedomModeMSec <= 0)
                ClearPracticeEffects(); // 如果这一帧内、练习模式的时间刚好归零了：则清空练习模式的一切效果。
            prevFreedomModeMSec = currentFreedomModeMSec;
        }

        // Pool allocation/start errors may be reported asynchronously by the native plugin.
        if (!stretchFailed && stretchRoute && player != null && player.isAvailable && player.GetStatus() == CriAtomExPlayer.Status.Error)
        {
            FailStretch(new InvalidOperationException("Music player entered Error status on stretch route"));
            ApplyAudio(true);
            return;
        }

        if (repeatStart >= 0 && repeatEnd >= 0)
        {
            if (CurrentPlayMsec >= repeatEnd)
            {
                CurrentPlayMsec = repeatStart;
            }
        }
    }

    private static float startGap = -1f;

    [HarmonyPatch(typeof(NotesManager), "StartPlay")]
    [HarmonyPostfix]
    public static void NotesManagerPostUpdateTimer(float msecStartGap)
    {
#if DEBUG
        MelonLogger.Msg($"[PracticeMode] NotesManager.StartPlay msecStartGap={msecStartGap}");
#endif
        startGap = msecStartGap;
    }

    private static bool isInAdvDemo = false;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AdvDemoProcess), nameof(AdvDemoProcess.OnStart))]
    public static void AdvDemoProcessOnStart()
    {
        isInAdvDemo = true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AdvDemoProcess), nameof(AdvDemoProcess.OnRelease))]
    public static void AdvDemoProcessOnRelease()
    {
        isInAdvDemo = false;
    }


    [HarmonyPatch(typeof(NotesManager), "UpdateTimer")]
    [HarmonyPrefix]
    public static bool NotesManagerPostUpdateTimer(bool ____isPlaying, Stopwatch ____stopwatch, ref float ____curMSec, ref float ____curMSecPre, float ____msecStartGap)
    {
        if (isInAdvDemo || Shim.IsKaleidxScopeMode)
        {
            return true;
        }

        if (startGap != -1f)
        {
            ____curMSec = startGap;
            startGap = -1f;
        }
        ____curMSecPre = ____curMSec;
        if (____isPlaying && ____stopwatch != null && !DebugFeature.Pause)
        {
            var num = (double)____stopwatch.ElapsedTicks / Stopwatch.Frequency * 1000.0 * speed;
            ____curMSec += (float)num;
            ____stopwatch.Reset();
            ____stopwatch.Start();
        }

        return false;
    }

    [HarmonyPatch(typeof(SoundCtrl), "Initialize")]
    [HarmonyPostfix]
    public static void SoundCtrlPostInitialize(IDictionary ____players)
    {
        var wrapper = ____players?[2];
        var next = wrapper?.GetType().GetField("Player")?.GetValue(wrapper) as CriAtomExPlayer;
        if (!ReferenceEquals(player, next)) { RestoreMusicPlayer(); stretchRoute = legacyPitch = false; }
        player = next;
        if (player != null && player.isAvailable)
        {
            if (keepPitch) InitializeStretchPool();
        }
        else
        {
            MelonLogger.Warning("[PracticeMode] Music player missing; practice audio speed control unavailable");
        }
    }

    [HarmonyPatch(typeof(MovieController), "Awake")]
    [HarmonyPostfix]
    public static void MovieControllerPostAwake(object ____moviePlayers)
    {
        // 1.55以上，_moviePlayers是List<MovieMaterialMai2>；1.50以下，___moviePlayers是MovieMaterialMai2。
        // 所以这里用object承接，再根据object的类型做具体处理
        if (____moviePlayers is List<MovieMaterialMai2> mList) movie = mList;
        else if (____moviePlayers is MovieMaterialMai2 m) movie = new([m]);
        else MelonLogger.Error("[PracticeMode] MovieControllerPostAwake: [BUG] 收到的____moviePlayers参数不正确");
    }
}
