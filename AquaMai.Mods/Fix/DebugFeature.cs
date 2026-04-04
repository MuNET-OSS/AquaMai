using System;
using System.Reflection;
using AquaMai.Config.Attributes;
using AquaMai.Mods.GameSystem;
using AquaMai.Mods.UX;
using HarmonyLib;
using MAI2.Util;
using Manager;
using MelonLoader;
using Monitor;
using Process;
using UnityEngine;

namespace AquaMai.Mods.Fix;

[ConfigSection(exampleHidden: true, defaultOn: true)]
public class DebugFeature
{
    public static bool IsPolyfill { get; private set; }
    private static GameProcess _gameProcess;
    private static MovieController _gameMovie;
    private static GameMonitor[] _monitors;
    private static object _debugFeatureOriginal;
    private static System.Type _debugFeatureType;

    private static MethodBase gameMoviePauseMethod;

    private static void GameMoviePause(bool pause)
    {
        if (gameMoviePauseMethod == null)
        {
            gameMoviePauseMethod = _gameMovie.GetType().GetMethod("Pause");
        }
        if (gameMoviePauseMethod.GetParameters().Length == 1)
        {
            gameMoviePauseMethod.Invoke(_gameMovie, [pause]);
        }
        else
        {
            for (int i = 0; i < 2; i++)
            {
                gameMoviePauseMethod.Invoke(_gameMovie, [i, pause]);
            }
        }
    }

    private static MethodBase gameMovieSetSeekFrameMethod;

    private static void GameMovieSetSeekFrame(double msec)
    {
        if (gameMovieSetSeekFrameMethod == null)
        {
            gameMovieSetSeekFrameMethod = _gameMovie.GetType().GetMethod("SetSeekFrame");
        }
        if (gameMovieSetSeekFrameMethod.GetParameters().Length == 1)
        {
            gameMovieSetSeekFrameMethod.Invoke(_gameMovie, [msec]);
        }
        else
        {
            for (int i = 0; i < 2; i++)
            {
                gameMovieSetSeekFrameMethod.Invoke(_gameMovie, [i, msec]);
            }
        }
    }

    [HarmonyPatch(typeof(GameProcess), "OnStart")]
    [HarmonyPostfix]
    public static void Init(GameProcess __instance, MovieController ____gameMovie, GameMonitor[] ____monitors)
    {
        _gameProcess = __instance;
        _gameMovie = ____gameMovie;
        _monitors = ____monitors;
        PolyFill.timer = 0;
        PolyFill.isPause = false;
    }

    public static void OnBeforePatch(HarmonyLib.Harmony h)
    {
        var original = typeof(GameProcess).GetField("debugFeature", BindingFlags.NonPublic | BindingFlags.Instance);
        if (original is null)
        {
            MelonLogger.Msg("  > [DebugFeature] Running Polyfill");
            IsPolyfill = true;
            h.PatchAll(typeof(PolyFill));
        }
        else
        {
            MelonLogger.Msg("  > [DebugFeature] Already included");
            _debugFeatureType = typeof(GameProcess).GetNestedType("DebugFeature", BindingFlags.Instance | BindingFlags.NonPublic);
            h.PatchAll(typeof(GetOriginal));
        }
    }

    public static bool Pause
    {
        get
        {
            if (IsPolyfill)
            {
                return PolyFill.isPause;
            }

            return (bool)_debugFeatureType.GetField("_debugPause", BindingFlags.Instance | BindingFlags.Public).GetValue(_debugFeatureOriginal);
        }

        set
        {
            if (IsPolyfill)
            {
                PolyFill.isPause = value;
            }
            else
            {
                _debugFeatureType.GetField("_debugPause", BindingFlags.Instance | BindingFlags.Public).SetValue(_debugFeatureOriginal, value);
            }

            SoundManager.PauseMusic(value);
            GameMoviePause(value);
            NotesManager.Pause(value);
            if (value) DontRuinMyAccount.triggerForPracticeMode();
        }
    }

    public static void Seek(int msec)
    {
        Singleton<GamePlayManager>.Instance.Initialize();
        if (IsPolyfill)
        {
            PolyFill.DebugTimeSkip(msec);
        }
        else
        {
            _debugFeatureType.GetMethod("DebugTimeSkip", BindingFlags.Instance | BindingFlags.Public).Invoke(_debugFeatureOriginal, new object[] { msec });
        }
        DontRuinMyAccount.triggerForPracticeMode();
    }

    public static double CurrentPlayMsec
    {
        [Obsolete("不要用它，它有问题。用 PracticeMode.CurrentPlayMsec")]
        get
        {
            if (IsPolyfill)
            {
                return PolyFill.timer;
            }

            return (double)_debugFeatureType.GetField("_debugTimer", BindingFlags.Instance | BindingFlags.Public).GetValue(_debugFeatureOriginal);
        }
        set
        {
            if (IsPolyfill)
            {
                PolyFill.timer = value;
            }
            else
            {
                _debugFeatureType.GetField("_debugTimer", BindingFlags.Instance | BindingFlags.Public).SetValue(_debugFeatureOriginal, value);
            }

            Seek(0);
        }
    }

    private static class GetOriginal
    {
        [HarmonyPatch(typeof(GameProcess), "OnStart")]
        [HarmonyPostfix]
        public static void Postfix(object ___debugFeature)
        {
            _debugFeatureOriginal = ___debugFeature;
        }
    }

    private static class PolyFill
    {
        public static bool isPause;
        public static double timer;
        public static KeyCode Autoplay = SetAutoplay();

        private static KeyCode SetAutoplay()
        {
            try { return (KeyCode)Enum.Parse(typeof(KeyCode), KeyMap.GetAutoplay()); }
            catch (Exception) { return KeyCode.Home; }
        }

        public static void DebugTimeSkip(int addMsec)
        {
            GameMoviePause(true);
            NotesManager.Pause(true);
            if (addMsec >= 0)
            {
                timer += addMsec;
            }
            else
            {
                timer = timer + addMsec >= 0.0 ? timer + addMsec : 0.0;
            }

            GameMovieSetSeekFrame(timer);
            SoundManager.SeekMusic((int)timer);
            for (int i = 0; i < _monitors.Length; i++)
            {
                _monitors[i].Seek((int)timer);
            }

            // magic number, dont know why
            NotesManager.StartPlay((int)timer + 91);
            NotesManager.Pause(isPause);
            if (!isPause)
            {
                SoundManager.PauseMusic(pause: false);
                GameMoviePause(false);
            }
            else
            {
                GameMoviePause(true);
            }

            _gameProcess.UpdateNotes();
        }

        [HarmonyPatch(typeof(GameProcess), "OnUpdate")]
        [HarmonyPostfix]
        public static void Postfix(byte ____sequence)
        {
            if (____sequence != 4) return;
            // GameSequence.Play
            if (!isPause)
            {
                timer += GameManager.GetGameMSecAddD();
            }

            if (KeyMap.disableDebugFeatureHotkeys) return;

            if (Input.GetKeyDown(Autoplay))
            {
                GameManager.AutoPlay = (GameManager.AutoPlayMode)((int)(GameManager.AutoPlay + 1) % Enum.GetNames(typeof(GameManager.AutoPlayMode)).Length);
            }
            else if (Input.GetKeyDown(KeyCode.Return))
            {
                isPause = !isPause;
                SoundManager.PauseMusic(isPause);
                GameMoviePause(isPause);
                NotesManager.Pause(isPause);
                if (isPause) DontRuinMyAccount.triggerForPracticeMode();
            }
            else if (DebugInput.GetKeyDown(KeyCode.LeftArrow) || DebugInput.GetKeyDown(KeyCode.RightArrow))
            {
                var num23 = 0;
                if (DebugInput.GetKeyDown(KeyCode.LeftArrow))
                {
                    num23 = -1000;
                }

                if (DebugInput.GetKeyDown(KeyCode.RightArrow))
                {
                    num23 = 1000;
                }

                int addMsec = ((!DebugInput.GetKey(KeyCode.LeftShift) && !DebugInput.GetKey(KeyCode.RightShift))
                    ? ((!DebugInput.GetKey(KeyCode.LeftControl) && !DebugInput.GetKey(KeyCode.RightControl)) ? num23 : (num23 * 10))
                    : (num23 * 5));
                Singleton<GamePlayManager>.Instance.Initialize();
                DebugTimeSkip(addMsec);
                DontRuinMyAccount.triggerForPracticeMode();
            }
        }
    }
}