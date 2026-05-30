using System;
using System.Diagnostics;
using AquaMai.Config.Attributes;
using AquaMai.Core.Attributes;
using AquaMai.Core.Helpers;
using AquaMai.Mods.Types;
using DB;
using HarmonyLib;
using IO;
using MAI2.Util;
using Manager;
using Net;
using MelonLoader;
using Process;
using System.Collections;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "启动直达乐曲",
    en: "Launch directly into the song ID specified by --music after startup.",
    zh: "启动流程结束后直接进入通过 --music 传入的歌曲 ID")]
[EnableIf(nameof(ShouldEnableImplicitly))]
[EnableImplicitlyIf(nameof(ShouldEnableImplicitly))]
public class StartupDirectToGame
{
    private const byte ReadyState = 8;
    private const byte ReleasedState = 9;
    private const int StandardScoreType = 0;
    private const int DeluxeScoreType = 1;
    private const int DeluxeMusicIdThreshold = 10000;

    [ConfigEntry(
        name: "缺省难度",
        zh: "如果没有在命令行中传入难度（--difficulty）的话，默认使用的难度",
        en: "Default difficulty to use if not provided via command line"
    )]
    private static readonly int DefaultDifficulty = 3;

    [ConfigEntry(
        name: "缺省 Note Speed",
        zh: "如果没有在命令行中传入 Note Speed（--noteSpeed）的话，默认使用的 Note Speed",
        en: "Default note speed to use if not provided via command line"
    )]
    private static readonly string DefaultNoteSpeed = "6.5";

    [ConfigEntry(
        name: "缺省 Touch Speed",
        zh: "如果没有在命令行中传入 Touch Speed（--touchSpeed）的话，默认使用的 Touch Speed",
        en: "Default touch speed to use if not provided via command line"
    )]
    private static readonly string DefaultTouchSpeed = "6.5";

    [ConfigEntry(
        name: "缺省 Slide Speed",
        zh: "如果没有在命令行中传入 Slide Speed（--slideSpeed）的话，默认使用的 Slide Speed。例如 normal、-0.5、0.5",
        en: "Default slide speed to use if not provided via command line. Examples: normal, -0.5, 0.5"
    )]
    private static readonly string DefaultSlideSpeed = "normal";

    [ConfigEntry(
        name: "缺省 Autoplay 类型",
        zh: "如果没有在命令行中传入 Autoplay 类型（--autoplay）的话，默认使用的类型。例如 Critical、Perfect、Great、Good、Miss、Random、None",
        en: "Default autoplay type to use if not provided via command line. Examples: Critical, Perfect, Great, Good, Miss, Random, None"
    )]
    private static readonly string DefaultAutoplay = "Critical";

    private static bool _argsParsed;
    private static int? _musicId;
    private static int _difficulty;
    private static OptionNotespeedID _noteSpeed;
    private static OptionTouchspeedID _touchSpeed;
    private static OptionSlidespeedID _slideSpeed;
    private static GameManager.AutoPlayMode _autoplay;

    public static bool ShouldEnableImplicitly
    {
        get
        {
            ParseCommandLineArgs();
            return _musicId.HasValue;
        }
    }

    public static void OnBeforeEnableCheck()
    {
        ParseCommandLineArgs();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(StartupProcess), "OnUpdate")]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static bool StartupProcessPreOnUpdate(StartupProcess __instance, ProcessDataContainer ___container, ref byte ____state, Stopwatch ___timer)
    {
        if (____state != ReadyState) return true;

        GameManager.Initialize();
        PrepareDummyGuestUser();
        PrepareGameStartState();

        ____state = ReleasedState;
        Singleton<OperationManager>.Instance.SetCoinBlockerMode(CoinBlocker.Mode.Game);
        GameManager.IsInitializeEnd = true;
        ___container.processManager.AddProcess(new CommonProcess(___container), 10);
        ___container.processManager.AddProcess(new PleaseWaitProcess(___container), 50);
        ___container.processManager.ReleaseProcess(__instance);

        MelonCoroutines.Start(NextFrameInit(___container, __instance));
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameProcess), "OnStart")]
    public static void GameProcessPostOnStart()
    {
        GameManager.AutoPlay = _autoplay;
    }

    private static IEnumerator NextFrameInit(ProcessDataContainer container, ProcessBase process)
    {
        yield return null;
        container.processManager.AddProcess(new FadeProcess(container, process, new TrackStartProcess(container)), 50);
        container.processManager.ClearTimeoutAction();
        container.processManager.SendMessage(new Message(ProcessType.CommonProcess, CommonProcess.MessageID_EntryInfoOut, false));
        container.processManager.SetVisibleTimers(isVisible: false);
        container.processManager.IsTimeCounting(isTimeCount: false);
        SoundManager.PreviewEnd();
        SoundManager.StopBGM(2);
    }

    private static void PrepareDummyGuestUser()
    {
        var userDataManager = Singleton<UserDataManager>.Instance;
        for (var i = 0; i < 2; i++)
        {
            var userData = userDataManager.GetUserData(i);
            userData.Initialize();
            if (i != 0)
            {
                userDataManager.SetDefault(i);
                continue;
            }

            userData.SetEntry(isEntry: true);
            userData.SetActiveUser(isActiveUser: true);
            userData.UserType = UserData.UserIDType.Guest;
            userData.Detail.UserID = UserID.GuestID();
            userData.Detail.UserName = CommonMessageID.GuestUserName.GetName();
            userDataManager.SetGuestContentBit(i);
            userDataManager.SetDefault(i);
            userData.Option.OptionKind = OptionKindID.Custom;
            userData.Option.NoteSpeed = _noteSpeed;
            userData.Option.TouchSpeed = _touchSpeed;
            userData.Option.SlideSpeed = _slideSpeed;
        }
    }

    private static void PrepareGameStartState()
    {
        var musicId = _musicId.Value;
        var dataManager = Singleton<DataManager>.Instance;
        var music = dataManager.GetMusic(musicId);

        Shim.Set_GameManager_IsNormalMode(true);
        GameManager.IsLongMusic = dataManager.IsLong(music.longMusic);
        GameManager.SelectedDeleteGhostID = GhostManager.GhostTarget.End;
        GameManager.SelectScoreType = GetScoreType(musicId);
        GameManager.MusicTrackNumber = 1;
        GameManager.SetMaxTrack();

        GameManager.SelectMusicID[0] = musicId;
        GameManager.SelectDifficultyID[0] = _difficulty;
        GameManager.SelectGhostID[0] = GhostManager.GhostTarget.End;
    }

    private static void ParseCommandLineArgs()
    {
        if (_argsParsed) return;
        _argsParsed = true;

        _difficulty = DefaultDifficulty;
        _noteSpeed = OptionSpeedParser.ParseOrDefault(DefaultNoteSpeed, OptionNotespeedID.Speed6_5);
        _touchSpeed = OptionSpeedParser.ParseOrDefault(DefaultTouchSpeed, OptionTouchspeedID.Speed6_5);
        _slideSpeed = OptionSpeedParser.ParseOrDefault(DefaultSlideSpeed, OptionSlidespeedID.Normal);
        _autoplay = ParseAutoplay(DefaultAutoplay, GameManager.AutoPlayMode.Critical);

        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length; i++)
        {
            if (!TryReadOptionValue(args, ref i, "--music", out var value) || !int.TryParse(value, out var musicId)) continue;
            _musicId = musicId;
            break;
        }

        if (!_musicId.HasValue) return;

        for (var i = 0; i < args.Length; i++)
        {
            if (TryReadOptionValue(args, ref i, "--difficulty", out var difficultyValue))
            {
                if (int.TryParse(difficultyValue, out var difficulty)) _difficulty = difficulty;
                continue;
            }

            if (TryReadOptionValue(args, ref i, "--noteSpeed", out var noteSpeedValue))
            {
                _noteSpeed = OptionSpeedParser.ParseOrDefault(noteSpeedValue, _noteSpeed);
                continue;
            }

            if (TryReadOptionValue(args, ref i, "--touchSpeed", out var touchSpeedValue))
            {
                _touchSpeed = OptionSpeedParser.ParseOrDefault(touchSpeedValue, _touchSpeed);
                continue;
            }

            if (TryReadOptionValue(args, ref i, "--slideSpeed", out var slideSpeedValue))
            {
                _slideSpeed = OptionSpeedParser.ParseOrDefault(slideSpeedValue, _slideSpeed);
                continue;
            }

            if (TryReadOptionValue(args, ref i, "--autoplay", out var autoplayValue) || TryReadOptionValue(args, ref i, "--autoPlay", out autoplayValue))
            {
                _autoplay = ParseAutoplay(autoplayValue, _autoplay);
            }
        }
    }

    private static bool TryReadOptionValue(string[] args, ref int index, string name, out string value)
    {
        value = null;
        var arg = args[index];
        if (arg == name)
        {
            if (index + 1 >= args.Length) return false;
            value = args[++index];
            return true;
        }

        var prefix = name + "=";
        if (!arg.StartsWith(prefix, StringComparison.Ordinal)) return false;

        value = arg.Substring(prefix.Length);
        return true;
    }

    private static int GetScoreType(int musicId)
    {
        return musicId >= DeluxeMusicIdThreshold ? DeluxeScoreType : StandardScoreType;
    }

    private static GameManager.AutoPlayMode ParseAutoplay(string value, GameManager.AutoPlayMode fallback)
    {
        return Enum.TryParse(value, ignoreCase: true, out GameManager.AutoPlayMode autoplay) ? autoplay : fallback;
    }
}
