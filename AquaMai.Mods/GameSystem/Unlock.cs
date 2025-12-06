using AquaMai.Config.Attributes;
using AquaMai.Core.Attributes;
using MAI2System;
using Manager;
using Manager.MaiStudio;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using MelonLoader;
using System.Collections;
using Manager.UserDatas;
using Net.VO.Mai2;
using Process;
using Util;
using System.Runtime.CompilerServices;
using AquaMai.Core;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "全解",
    en: """
        Unlock normally locked (including normally non-unlockable) game content.
        Anything unlocked (except the characters you leveled-up) by this mod will not be uploaded your account.
        You'll still "get" those musics/collections/courses just like in normal plays.
        """,
    zh: """
        解锁原本锁定（包括正常途径无法解锁）的游戏内容
        由本 Mod 解锁的内容（除了被你升级过的角色以外）不会上传到你的账户
        游玩时仍会像未开启解锁一样「获得」那些乐曲/收藏品/段位
        """)]
[EnableGameVersion(23000)]
public class Unlock
{
    public static void OnBeforeEnableCheck()
    {
        InitializeCollectionHooks();
    }

    [ConfigEntry(
        name: "区域",
        en: "Unlock maps that are not in this version.",
        zh: "解锁游戏里所有的区域，包括非当前版本的（并不会帮你跑完）")]
    private static readonly bool maps = true;

    [EnableIf(typeof(Unlock), nameof(maps))]
    public class MapHook
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MapData), "get_OpenEventId")]
        public static bool get_OpenEventId(ref StringID __result)
        {
            // For any map, return the event ID 1 to unlock it
            var id = new Manager.MaiStudio.Serialize.StringID
            {
                id = 1,
                str = "無期限常時解放"
            };

            var sid = new StringID();
            sid.Init(id);

            __result = sid;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UserMapData), "get_IsLock")]
        public static bool get_IsLock(ref bool __result)
        {
            __result = false;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MapMaster), "IsLock")]
        public static bool PreIsLock(ref bool __result)
        {
            __result = false;
            return false;
        }
    }

    [ConfigEntry(
        name: "乐曲",
        en: "Unlock all songs, and skip the Master/ReMaster unlock screen (still save the unlock status).",
        zh: "解锁所有乐曲，并跳过紫/白解锁画面（会正常保存解锁状态）")]
    private static readonly bool songs = true;

    [EnableIf(typeof(Unlock), nameof(songs))]
    public class SongHook
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MAI2System.Config), "IsAllOpen", MethodType.Getter)]
        public static bool IsAllOpen(ref bool __result)
        {
            __result = true;
            return false;
        }

        // Skip the UnlockProcess but still save the music Master/ReMaster unlock status normally.

        private static List<int> allUnlockedList = null;

        private static readonly Dictionary<UserData,
        (
            List<int> masterList,
            List<int> reMasterList
        )> userDataBackupMap = [];

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ResultProcess), "ToNextProcess")]
        public static void PreToNextProcess() {
            allUnlockedList ??= DataManager.Instance
                .GetMusics()
                .Select(pair => pair.Key)
                .ToList();
            for (var i = 0; i < 2; i++)
            {
                var userData = UserDataManager.Instance.GetUserData(i);
                if (!userData.IsEntry) continue;
                if (!userDataBackupMap.ContainsKey(userData))
                {
                    userDataBackupMap[userData] =
                    (
                        masterList: userData.MusicUnlockMasterList,
                        reMasterList: userData.MusicUnlockReMasterList
                    );
                }
                else
                {
                    MelonLogger.Error($"[Unlock.SongHook] User data already backed up, incompatible mods loaded?");
                }
                userData.MusicUnlockMasterList = allUnlockedList;
                userData.MusicUnlockReMasterList = allUnlockedList;
            }
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(ResultProcess), "ToNextProcess")]
        public static void FinToNextProcess()
        {
            for (var i = 0; i < 2; i++)
            {
                var userData = UserDataManager.Instance.GetUserData(i);
                if (!userData.IsEntry) continue;
                if (userData.MusicUnlockMasterList != allUnlockedList || userData.MusicUnlockReMasterList != allUnlockedList)
                {
                    MelonLogger.Error($"[Unlock.SongHook] Music Master/ReMaster unlock list changed unexpectedly, incompatible mods loaded?");
                }
                else if (!userDataBackupMap.TryGetValue(userData, out var backup))
                {
                    MelonLogger.Error($"[Unlock.SongHook] User data backup not found, incompatible mods loaded?");
                }
                else
                {
                    userData.MusicUnlockMasterList = backup.masterList;
                    userData.MusicUnlockReMasterList = backup.reMasterList;
                }

                // Trigger a manual silent unlock check.
                if (!GameManager.IsEventMode)
                {
                    int trackNo = GamePlayManager.Instance.GetScoreListCount();
                    GameScoreList gameScore = GamePlayManager.Instance.GetGameScore(i, trackNo - 1);
                    int musicId = gameScore.SessionInfo.musicId;
                    if (gameScore.SessionInfo.difficulty >= 2 &&
                        musicId >= 10000 && musicId < 20000 &&
                        gameScore.GetAchivement() >= 97m)
                    {
                        var notesInfo = NotesListManager.Instance.GetNotesList()[musicId];
                        var musicInfo = DataManager.Instance.GetMusic(musicId);
                        if (!userData.MusicUnlockMasterList.Contains(musicId) && notesInfo.IsEnable[3])
                        {
                            userData.MusicUnlockMasterList.Add(musicId);
                        }
                        if (!userData.MusicUnlockReMasterList.Contains(musicId) && notesInfo.IsEnable[4] && musicInfo.subLockType == 0)
                        {
                            userData.MusicUnlockReMasterList.Add(musicId);
                        }
                    }
                }
            }
            userDataBackupMap.Clear();
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MusicData), nameof(MusicData.Init))]
    [EnableIf(nameof(songs))]
    public static void PostMusicDataInit(MusicData __instance)
    {
        Traverse.Create(__instance).Property<bool>("disable").Value = true;
    }

    [ConfigEntry(
        name: "跑图券",
        en: "Unlock normally event-only tickets.",
        zh: "解锁游戏里所有可能的跑图券")]
    private static readonly bool tickets = false;

    [EnableIf(typeof(Unlock), nameof(tickets))]
    public class TicketHook
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(TicketData), "get_ticketEvent")]
        public static bool get_ticketEvent(ref StringID __result)
        {
            // For any ticket, return the event ID 1 to unlock it
            var id = new Manager.MaiStudio.Serialize.StringID
            {
                id = 1,
                str = "無期限常時解放"
            };

            var sid = new StringID();
            sid.Init(id);

            __result = sid;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TicketData), "get_maxCount")]
        public static bool get_maxCount(ref int __result)
        {
            // Modify the maxTicketNum to 0
            // this is because TicketManager.GetTicketData adds the ticket to the list if either
            // the player owns at least one ticket or the maxTicketNum = 0
            __result = 0;
            return false;
        }
    }

    [ConfigEntry(
        name: "段位",
        en: "Unlock all course-mode courses (no need to reach 10th dan to play \"real\" dan).",
        zh: "解锁所有段位模式的段位（不需要十段就可以打真段位）")]
    private static readonly bool courses = false;

    [EnableIf(typeof(Unlock), nameof(courses))]
    public class CourseHook
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CourseData), "get_eventId")]
        public static void get_eventId(ref StringID __result)
        {
            if (__result.id == 0) return; // Should not be unlocked

            // Return the event ID 1 to unlock it
            var id = new Manager.MaiStudio.Serialize.StringID
            {
                id = 1,
                str = "無期限常時解放"
            };

            var sid = new StringID();
            sid.Init(id);

            __result = sid;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CourseData), "get_isLock")]
        public static bool get_isLock(ref bool __result)
        {
            __result = false;
            return false;
        }
    }

    [ConfigEntry(
        name: "宴会场",
        en: "Unlock Utage without the need of DXRating 10000.",
        zh: "不需要万分也可以进宴会场")]
    private static readonly bool utage = true;

    [EnableIf(typeof(Unlock), nameof(utage))]
    [EnableGameVersion(24000)]
    public class UtageHook
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameManager), "CanUnlockUtageTotalJudgement")]
        public static bool CanUnlockUtageTotalJudgement(out ConstParameter.ResultOfUnlockUtageJudgement result1P, out ConstParameter.ResultOfUnlockUtageJudgement result2P)
        {
            result1P = ConstParameter.ResultOfUnlockUtageJudgement.Unlocked;
            result2P = ConstParameter.ResultOfUnlockUtageJudgement.Unlocked;
            return false;
        }
    }

    private static readonly List<
    (
        string configField,
        string collectionProcessMethod,
        string userDataProperty,
        string dataManagerMethod
    )> collectionHookSpecification =
    [
        (nameof(titles), "CreateTitleData", "TitleList", "GetTitles"),
        (nameof(icons), "CreateIconData", "IconList", "GetIcons"),
        (nameof(plates), "CreatePlateData", "PlateList", "GetPlates"),
        (nameof(frames), "CreateFrameData", "FrameList", "GetFrames"),
        (nameof(partners), "CreatePartnerData", "PartnerList", "GetPartners"),
    ];

    [ConfigEntry(
        name: "称号",
        en: "Unlock all titles."
    )]
    private static readonly bool titles = false;

    [ConfigEntry(
        name: "头像",
        en: "Unlock all icons."
    )]
    private static readonly bool icons = false;

    [ConfigEntry(
        name: "姓名框",
        en: "Unlock all plates."
    )]
    private static readonly bool plates = false;

    [ConfigEntry(
        name: "背景",
        en: "Unlock all frames."
    )]
    private static readonly bool frames = false;

    [ConfigEntry(
        name: "搭档",
        en: "Unlock all partners."
    )]
    private static readonly bool partners = false;

    [ConfigEntry(
        name: "Events",
        en: "Enable all events."
    )]
    private static readonly bool events = false;

    [ConfigEntry(
        name: "Event 黑名单",
        en: "Do not unlock the following events. Leave it enabled if you don't know what this is.",
        zh: "不解锁以下 Event。如果你不知道这是什么，请勿修改",
        hideWhenDefault: true
    )]
    private static readonly string eventBlackList = "0,250926121,251205121";
    private static HashSet<int> eventBlackListSet = null;

    [EnableIf(nameof(events))]
    [HarmonyPrefix]
    [HarmonyPatch(typeof(EventManager), "IsOpenEvent")]
    private static bool EnableAllEvent(ref bool __result, int eventId)
    {
        eventBlackListSet ??= eventBlackList
            .Split(',')
            .Select(s => s.Trim())
            .Where(s => int.TryParse(s, out _))
            .Select(int.Parse)
            .ToHashSet();
        if (eventBlackListSet.Contains(eventId))
            return true;
        __result = true;
        return false;
    }

    private static List<
    (
        MethodInfo collectionProcessMethod,
        PropertyInfo userDataProperty,
        MethodInfo dataManagerMethod
    )> collectionHooks;

    private static void InitializeCollectionHooks()
    {
        collectionHooks = collectionHookSpecification
            .Where(spec =>
                typeof(Unlock)
                    .GetField(spec.configField, BindingFlags.Static | BindingFlags.NonPublic)
                    .GetValue(null) as bool? ?? false)
            .Select(spec =>
            (
                collectionProcessMethod: typeof(CollectionProcess)
                    .GetMethod(spec.collectionProcessMethod, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                userDataProperty: typeof(UserData)
                    .GetProperty(spec.userDataProperty, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                dataManagerMethod: typeof(DataManager)
                    .GetMethod(spec.dataManagerMethod, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ))
            .Where(target =>
                target.collectionProcessMethod != null &&
                target.userDataProperty != null &&
                target.dataManagerMethod != null)
            .ToList();
    }

    private static bool CollectionHookEnabled => collectionHooks.Count > 0;

    // The data in collection process is initialized in CreateXXXData() methods.
    // Hook those method, set corrsponding UserData property to the all unlocked list and restore it after the method call.
    // So the all unlocked items list will not be uploaded to the server.
    [EnableIf(typeof(Unlock), nameof(CollectionHookEnabled))]
    [HarmonyPatch]
    public class CollectionHook
    {
        private static readonly Dictionary<MethodInfo, List<Manager.UserDatas.UserItem>> allUnlockedItemsCache = [];

        public static List<Manager.UserDatas.UserItem> GetAllUnlockedItemList(MethodInfo dataManagerMethod)
        {
            if (allUnlockedItemsCache.TryGetValue(dataManagerMethod, out var result))
            {
                return result;
            }
            result = dataManagerMethod.Invoke(DataManager.Instance, null) is not IEnumerable dictionary
                ? []
                : dictionary
                    .Cast<object>()
                    .Select(pair =>
                        pair
                            .GetType()
                            .GetProperty("Key")
                            .GetValue(pair))
                    .Select(id =>
                        new Manager.UserDatas.UserItem
                        {
                            itemId = (int)id,
                            stock = 1,
                            isValid = true
                        })
                    .ToList();
            allUnlockedItemsCache[dataManagerMethod] = result;
            return result;
        }

        public static IEnumerable<MethodBase> TargetMethods() => collectionHooks.Select(target => target.collectionProcessMethod);

        public record PropertyChangeLog(object From, object To);

        public static void Prefix(out Dictionary<UserData, Dictionary<PropertyInfo, PropertyChangeLog>> __state)
        {
            __state = [];
            ModifyUserData(false, ref __state);
        }

        public static void Finalizer(Dictionary<UserData, Dictionary<PropertyInfo, PropertyChangeLog>> __state)
        {
            ModifyUserData(true, ref __state);
        }

        private static void ModifyUserData(bool restore, ref Dictionary<UserData, Dictionary<PropertyInfo, PropertyChangeLog>> backup)
        {
            for (int i = 0; i < 2; i++)
            {
                var userData = UserDataManager.Instance.GetUserData(i);
                if (!userData.IsEntry) continue;
                if (!backup.TryGetValue(userData, out var userBackup))
                {
                    backup[userData] = userBackup = [];
                }
                foreach (var (_, userDataProperty, dataManagerMethod) in collectionHooks)
                {
                    var currentValue = userDataProperty.GetValue(userData);
                    if (restore)
                    {
                        if (!userBackup.TryGetValue(userDataProperty, out var backupData))
                        {
                            MelonLogger.Error($"[Unlock.CollectionHook] Failed to restore {userDataProperty.Name} to the original value. Backup data not found.");
                            continue;
                        }
                        else if (currentValue != backupData.To)
                        {
                            MelonLogger.Error($"[Unlock.CollectionHook] Failed to restore {userDataProperty.Name} to the original value. Value changed unexpectedly, incompatible mods loaded?");
                            continue;
                        }
                        userDataProperty.SetValue(userData, backupData.From);
                    }
                    else
                    {
                        var allUnlockedItems = GetAllUnlockedItemList(dataManagerMethod);
                        userBackup[userDataProperty] = new(From: currentValue, To: allUnlockedItems);
                        userDataProperty.SetValue(userData, allUnlockedItems);
                    }
                }
            }
        }
    }

    [ConfigEntry(
        name: "旅行伙伴",
        en: "Unlock all characters."
    )]
    private static readonly bool characters = false;

    [EnableIf(typeof(Unlock), nameof(characters))]
    public class CharacterHook
    {
        private enum State
        {
            AllUnlockedList,
            OriginalList,
            ExportList
        }

        // NOTE(menci): State manipulations -- I don't know a better way to do this in Harmony.

        // The original list is being cleared and initialized in these two methods.
        [HarmonyPatch(typeof(UserData), "Initialize")] [HarmonyPrefix] public static void PreInitialize(UserData __instance) { TryInitializeUserState(__instance); PushState("UserData.Initialize", State.OriginalList); }
        [HarmonyPatch(typeof(UserData), "Initialize")] [HarmonyFinalizer] public static void FinInitialize() => PopState("UserData.Initialize", State.OriginalList);
        [HarmonyPatch(typeof(PlInformationProcess), "RestoreCharadata")] [HarmonyPrefix] public static void PreRestoreCharadata() => PushState("PlInformationProcess.RestoreCharadata", State.OriginalList);
        [HarmonyPatch(typeof(PlInformationProcess), "RestoreCharadata")] [HarmonyFinalizer] public static void FinRestoreCharadata() => PopState("PlInformationProcess.RestoreCharadata", State.OriginalList);
        // When adding a character to the user's list, let the game add it to the original list.
        [HarmonyPatch(typeof(UserData), "AddCollections")] [HarmonyPrefix] public static void PreAddCollections() => PushState("UserData.AddCollections", State.OriginalList);
        [HarmonyPatch(typeof(UserData), "AddCollections")] [HarmonyFinalizer] public static void FinAddCollections() => PopState("UserData.AddCollections", State.OriginalList);
        [HarmonyPatch(typeof(VOExtensions), "ExportUserAll")] [HarmonyPrefix] public static void PreExportUserAll() => PushState("VOExtensions.ExportUserAll", State.ExportList);
        [HarmonyPatch(typeof(VOExtensions), "ExportUserAll")] [HarmonyFinalizer] public static void FinExportUserAll() => PopState("VOExtensions.ExportUserAll", State.ExportList);

        // Ideally, the all unlocked list should be computed every time based on the user's owned characters
        // to avoid one character id being referenced by multiple UserChara instances
        // to ensure leveling-up characters is saved correctly.
        private record PropertyChangeLog
        {
            public List<UserChara> OriginalList { get; set; }
            public int CachedOriginalSize { get; set; } // Track the size of original list (above list) to detect changes.
            public List<UserChara> AllUnlockedListCache { get; set; }
            public Stack<State> StateStack { get; set; }
        };
        private readonly static ConditionalWeakTable<UserData, PropertyChangeLog> userStateMap = new();

        private static void TryInitializeUserState(UserData userData)
        {
            if (userStateMap.TryGetValue(userData, out _))
            {
                userStateMap.Remove(userData);
            }
            // The game data may not be initialized yet, so we can't get the all unlocked list here.
            var allUnlockedListCache = new List<UserChara>();
            var stateStack = new Stack<State>();
            stateStack.Push(State.AllUnlockedList); // The base state.
            userStateMap.Add(
                userData,
                new PropertyChangeLog()
                {
                    OriginalList = userData.CharaList,
                    CachedOriginalSize = -1,
                    AllUnlockedListCache = allUnlockedListCache,
                    StateStack = stateStack
                });
            // Fill in the all unlocked list later when the state is poped to State.AllUnlockedList
            // and game data is initialized. At least we have RestoreCharadata().
            userData.CharaList = allUnlockedListCache;
        }

        private static void PushState(string method, State state)
        {
#if DEBUG
            MelonLogger.Msg($"[Unlock.CharacterHook] {method}: {state} push");
#endif
            if (UserDataManager.Instance == null) return;
            for (int i = 0; i < 2; i++)
            {
                var userData = UserDataManager.Instance.GetUserData(i);
                if (!userData.IsEntry || !userStateMap.TryGetValue(userData, out var userState) || userState == null) continue;
                var oldStackTop = userState.StateStack.Peek();
                userState.StateStack.Push(state);
                if (state != oldStackTop)
                {
                    ApplyState(userData, state);
                }
            }
        }

        private static void PopState(string method, State state)
        {
#if DEBUG
            MelonLogger.Msg($"[Unlock.CharacterHook] {method}: {state} pop");
#endif
            if (UserDataManager.Instance == null) return;
            for (int i = 0; i < 2; i++)
            {
                var userData = UserDataManager.Instance.GetUserData(i);
                if (!userData.IsEntry || !userStateMap.TryGetValue(userData, out var userState) || userState == null) continue;
                if (userState.StateStack.Count <= 1)
                {
                    MelonLogger.Error($"[Unlock.CharacterHook] State stack underflow (Count = {userState.StateStack.Count}), incompatible mods loaded?");
                    continue;
                }
                else if (userState.StateStack.Peek() != state)
                {
                    MelonLogger.Error($"[Unlock.CharacterHook] State stack top mismatch (Expected: {state}, Actual: {userState.StateStack.Peek()}), incompatible mods loaded?");
                    continue;
                }
                userState.StateStack.Pop();
                var newStackTop = userState.StateStack.Peek();
                if (state != newStackTop)
                {
                    ApplyState(userData, newStackTop);
                }
            }
        }

        private static void ApplyState(UserData userData, State state)
        {
            if (!userStateMap.TryGetValue(userData, out var userState))
            {
                throw new KeyNotFoundException("User data not found in the user state map. This should not happen.");
            }

            var originalList = userState.OriginalList;
            if (state == State.OriginalList)
            {
                userData.CharaList = originalList;
            }
            else if (state == State.ExportList)
            {
                // Return a list of characters that should be saved to the server, i.e.
                // 1. Characters in the original list
                // 2. Characters NOT in the original list, but leveled-up
                Dictionary<int, UserChara> originalDict = originalList.ToDictionary(chara => chara.ID);
                userData.CharaList = MaybeMergeUserCharaList(userData, originalList)
                    .Where(chara => originalDict.ContainsKey(chara.ID) || chara.Level > 1)
                    .ToList();
            }
            else if (state == State.AllUnlockedList)
            {
                userData.CharaList = MaybeMergeUserCharaList(userData, originalList);
            }
        }

        // Since we cache the all unlocked list for each user, we need to merge the original list with the all unlocked list
        // each time when the original list is changed, judged by the size change of the original list
        // (assuming the references in two lists always consistent, except in the skip methods).
        private static List<UserChara> MaybeMergeUserCharaList(UserData userData, List<UserChara> originalList)
        {
            if (!userStateMap.TryGetValue(userData, out var userState))
            {
                throw new KeyNotFoundException("User data not found in the user state map. This should not happen.");
            }

            var allUnlockedListEmpty = userState.AllUnlockedListCache.Count == 0;
            if (allUnlockedListEmpty)
            {
                userState.AllUnlockedListCache.AddRange(DataManager.Instance
                    .GetCharas()
                    .Select(pair => pair.Value)
                    .Select(chara => new UserChara(chara.GetID())));
            }

            if (!allUnlockedListEmpty && userState.CachedOriginalSize == originalList.Count)
            {
                // The original list is not changed, return the cached all unlocked list.
                return userState.AllUnlockedListCache;
            }

            // The original list is changed (or all unlocked list just initialized), merge the original list with the override list.
            Dictionary<int, UserChara> originalDict = originalList.ToDictionary(chara => chara.ID);
            Dictionary<int, UserChara> cachedDict = userState.AllUnlockedListCache.ToDictionary(chara => chara.ID);
            
            // Apply leveling-ups of the all unlocked characters to the original ones (if present).
            // (If not present, they'll be finally added to the user's list in ExportUserAll() hook.)
            foreach (var (id, chara) in cachedDict)
            {
                if (originalDict.TryGetValue(id, out var originalChara) &&
                    originalChara != chara &&
                    originalChara.Level < chara.Level)
                {
                    foreach (var property in typeof(UserChara).GetProperties())
                    {
                        if (property.CanWrite)
                        {
                            property.SetValue(originalChara, property.GetValue(chara));
                        }
                    }
                }
            }

            userState.AllUnlockedListCache = cachedDict
                .Select(pair =>
                    originalDict.TryGetValue(pair.Key, out var originalChara)
                    ? originalChara
                    : pair.Value)
                .ToList();
            userState.CachedOriginalSize = originalList.Count;
            return userState.AllUnlockedListCache;
        }
    }
}
