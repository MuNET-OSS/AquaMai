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
    // 在启用检查前初始化收藏品 Hook
    // 这样可以根据用户配置动态决定启用哪些 Hook
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
            // 对于任何区域，返回活动 ID 1（无期限常时解放）来解锁它
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
            // 让游戏认为所有区域都已解锁
            __result = false;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MapMaster), "IsLock")]
        public static bool PreIsLock(ref bool __result)
        {
            // 让游戏认为所有区域都已解锁
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
            // 让游戏认为所有乐曲都已解锁
            __result = true;
            return false;
        }

        // 跳过解锁画面，但仍然正常保存紫谱/白谱的解锁状态
        // 实现思路：
        // 1. 在结算流程开始前，临时把用户的解锁列表替换成全部乐曲的列表
        // 2. 这样游戏就不会显示解锁画面（因为"已经解锁了"）
        // 3. 在结算流程结束后，恢复用户原本的解锁列表
        // 4. 同时手动检查并保存本次游玩应该解锁的紫谱/白谱

        private static List<int> allUnlockedList = null; // 所有乐曲 ID 的列表（用于伪装成全解）

        // 备份每个玩家的原始解锁列表（key: 用户数据，value: (紫谱列表, 白谱列表)）
        private static readonly Dictionary<UserData,
        (
            List<int> masterList,
            List<int> reMasterList
        )> userDataBackupMap = [];

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ResultProcess), "ToNextProcess")]
        public static void PreToNextProcess() {
            // 懒加载：第一次调用时获取所有乐曲的 ID 列表
            allUnlockedList ??= DataManager.Instance
                .GetMusics()
                .Select(pair => pair.Key)
                .ToList();
            
            // 遍历两个玩家位（双人游玩时）
            for (var i = 0; i < 2; i++)
            {
                var userData = UserDataManager.Instance.GetUserData(i);
                if (!userData.IsEntry) continue; // 跳过未登录的玩家位
                
                // 备份用户的原始解锁列表
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
                
                // 临时替换成全解锁列表，这样游戏就不会显示解锁画面
                userData.MusicUnlockMasterList = allUnlockedList;
                userData.MusicUnlockReMasterList = allUnlockedList;
            }
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(ResultProcess), "ToNextProcess")]
        public static void FinToNextProcess()
        {
            // 结算流程结束后，恢复用户的原始解锁列表
            for (var i = 0; i < 2; i++)
            {
                var userData = UserDataManager.Instance.GetUserData(i);
                if (!userData.IsEntry) continue;
                
                // 检查列表是否被意外修改（可能是其他 Mod 冲突）
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
                    // 恢复原始的解锁列表
                    userData.MusicUnlockMasterList = backup.masterList;
                    userData.MusicUnlockReMasterList = backup.reMasterList;
                }

                // 手动静默检查并保存本次应该解锁的紫谱/白谱
                // 解锁条件：非活动模式下，达成率 >= 97%，难度 >= Re:Master (2)，乐曲 ID 在 10000-20000 之间
                if (!GameManager.IsEventMode)
                {
                    int trackNo = GamePlayManager.Instance.GetScoreListCount();
                    GameScoreList gameScore = GamePlayManager.Instance.GetGameScore(i, trackNo - 1);
                    int musicId = gameScore.SessionInfo.musicId;
                    
                    // 检查是否满足解锁条件
                    if (gameScore.SessionInfo.difficulty >= 2 &&
                        musicId >= 10000 && musicId < 20000 &&
                        gameScore.GetAchivement() >= 97m)
                    {
                        var notesInfo = NotesListManager.Instance.GetNotesList()[musicId];
                        var musicInfo = DataManager.Instance.GetMusic(musicId);
                        
                        // 如果紫谱存在且未解锁，则添加到解锁列表
                        if (!userData.MusicUnlockMasterList.Contains(musicId) && notesInfo.IsEnable[3])
                        {
                            userData.MusicUnlockMasterList.Add(musicId);
                        }
                        
                        // 如果白谱存在且未解锁（且没有特殊锁定），则添加到解锁列表
                        if (!userData.MusicUnlockReMasterList.Contains(musicId) && notesInfo.IsEnable[4] && musicInfo.subLockType == 0)
                        {
                            userData.MusicUnlockReMasterList.Add(musicId);
                        }
                    }
                }
            }
            
            // 清空备份字典，准备下次使用
            userDataBackupMap.Clear();
        }
    }

    // 在乐曲数据初始化后，将所有乐曲标记为"未禁用"
    // 这样可以确保所有乐曲都可以在选曲界面显示
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MusicData), nameof(MusicData.Init))]
    [EnableIf(nameof(songs))]
    public static void PostMusicDataInit(MusicData __instance)
    {
        // disable 属性为 true 表示乐曲未被禁用（命名有点反直觉）
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
            // 对于任何跑图券，返回活动 ID 1（无期限常时解放）来解锁它
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
            // 将最大跑图券数量修改为 0
            // 这是因为 TicketManager.GetTicketData 在以下任一条件满足时会将跑图券添加到列表：
            // 1. 玩家拥有至少一张跑图券
            // 2. maxTicketNum = 0
            // 所以设置为 0 可以让所有跑图券都显示出来
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
            // 如果活动 ID 是 0，说明这个段位不应该被解锁（可能是未实装的）
            if (__result.id == 0) return;

            // 对于其他段位，返回活动 ID 1（无期限常时解放）来解锁它
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
            // 让游戏认为所有段位都已解锁
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
            // 让游戏认为两个玩家位都已解锁宴会场
            // （原本需要 DX Rating 达到 10000）
            result1P = ConstParameter.ResultOfUnlockUtageJudgement.Unlocked;
            result2P = ConstParameter.ResultOfUnlockUtageJudgement.Unlocked;
            return false;
        }
    }

    // 收藏品解锁的统一配置规范
    // 每一项定义了一种收藏品类型的相关方法和属性名称
    // 这样可以用同一套逻辑处理多种收藏品（称号、头像、姓名框、背景、搭档）
    private static readonly List<
    (
        string configField,              // 对应的配置字段名（如 titles、icons）
        string collectionProcessMethod,  // CollectionProcess 类中初始化该收藏品的方法名
        string userDataProperty,         // UserData 类中存储该收藏品列表的属性名
        string dataManagerMethod         // DataManager 类中获取所有该类型收藏品的方法名
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
        // 懒加载：第一次调用时解析黑名单字符串
        eventBlackListSet ??= eventBlackList
            .Split(',')
            .Select(s => s.Trim())
            .Where(s => int.TryParse(s, out _))
            .Select(int.Parse)
            .ToHashSet();
        
        // 如果活动在黑名单中，使用游戏原本的判断逻辑
        if (eventBlackListSet.Contains(eventId))
            return true;
        
        // 否则强制解锁该活动
        __result = true;
        return false;
    }

    // 收藏品 Hook 的实际反射信息列表
    // 根据用户配置从 collectionHookSpecification 中筛选出需要启用的 Hook
    private static List<
    (
        MethodInfo collectionProcessMethod,  // CollectionProcess 中的初始化方法
        PropertyInfo userDataProperty,       // UserData 中的收藏品列表属性
        MethodInfo dataManagerMethod         // DataManager 中获取所有收藏品的方法
    )> collectionHooks;

    // 在模块启用前初始化收藏品 Hook
    // 只为用户启用的收藏品类型创建 Hook
    private static void InitializeCollectionHooks()
    {
        collectionHooks = collectionHookSpecification
            // 筛选出配置为 true 的收藏品类型
            .Where(spec =>
                typeof(Unlock)
                    .GetField(spec.configField, BindingFlags.Static | BindingFlags.NonPublic)
                    .GetValue(null) as bool? ?? false)
            // 通过反射获取对应的方法和属性信息
            .Select(spec =>
            (
                collectionProcessMethod: typeof(CollectionProcess)
                    .GetMethod(spec.collectionProcessMethod, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                userDataProperty: typeof(UserData)
                    .GetProperty(spec.userDataProperty, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                dataManagerMethod: typeof(DataManager)
                    .GetMethod(spec.dataManagerMethod, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ))
            // 过滤掉反射失败的项（防止版本不兼容）
            .Where(target =>
                target.collectionProcessMethod != null &&
                target.userDataProperty != null &&
                target.dataManagerMethod != null)
            .ToList();
    }

    // 检查是否有任何收藏品 Hook 被启用
    private static bool CollectionHookEnabled => collectionHooks.Count > 0;

    // 收藏品解锁的核心逻辑：
    // CollectionProcess 的 CreateXXXData() 方法会读取用户的收藏品列表来初始化界面数据
    // 我们 Hook 这些方法，在调用前临时替换用户的收藏品列表为全解锁列表，调用后恢复
    // 这样界面上会显示所有收藏品，但上传到服务器的仍然是用户原本的列表
    [EnableIf(typeof(Unlock), nameof(CollectionHookEnabled))]
    [HarmonyPatch]
    public class CollectionHook
    {
        // 缓存每种收藏品的全解锁列表，避免重复生成
        private static readonly Dictionary<MethodInfo, List<Manager.UserDatas.UserItem>> allUnlockedItemsCache = [];

        // 获取指定收藏品类型的全解锁列表
        public static List<Manager.UserDatas.UserItem> GetAllUnlockedItemList(MethodInfo dataManagerMethod)
        {
            // 如果已缓存，直接返回
            if (allUnlockedItemsCache.TryGetValue(dataManagerMethod, out var result))
            {
                return result;
            }
            
            // 调用 DataManager 的方法获取所有该类型的收藏品
            // 返回值是一个字典，我们提取所有的 Key（收藏品 ID）
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
            
            // 缓存结果
            allUnlockedItemsCache[dataManagerMethod] = result;
            return result;
        }

        // Harmony 的多目标 Patch：返回需要 Hook 的所有方法
        public static IEnumerable<MethodBase> TargetMethods() => collectionHooks.Select(target => target.collectionProcessMethod);

        // 记录属性变更的日志：从什么值（From）改成什么值（To）
        public record PropertyChangeLog(object From, object To);

        // 在 CreateXXXData() 调用前执行：备份并替换收藏品列表
        public static void Prefix(out Dictionary<UserData, Dictionary<PropertyInfo, PropertyChangeLog>> __state)
        {
            __state = [];
            ModifyUserData(false, ref __state); // false 表示替换为全解锁列表
        }

        // 在 CreateXXXData() 调用后执行：恢复原始收藏品列表
        public static void Finalizer(Dictionary<UserData, Dictionary<PropertyInfo, PropertyChangeLog>> __state)
        {
            ModifyUserData(true, ref __state); // true 表示恢复原始列表
        }

        // 修改用户数据：替换或恢复收藏品列表
        private static void ModifyUserData(bool restore, ref Dictionary<UserData, Dictionary<PropertyInfo, PropertyChangeLog>> backup)
        {
            for (int i = 0; i < 2; i++)
            {
                var userData = UserDataManager.Instance.GetUserData(i);
                if (!userData.IsEntry) continue; // 跳过未登录的玩家位
                
                // 获取或创建该用户的备份字典
                if (!backup.TryGetValue(userData, out var userBackup))
                {
                    backup[userData] = userBackup = [];
                }
                
                // 遍历所有启用的收藏品类型
                foreach (var (_, userDataProperty, dataManagerMethod) in collectionHooks)
                {
                    var currentValue = userDataProperty.GetValue(userData);
                    
                    if (restore)
                    {
                        // 恢复模式：把收藏品列表恢复成原始值
                        if (!userBackup.TryGetValue(userDataProperty, out var backupData))
                        {
                            MelonLogger.Error($"[Unlock.CollectionHook] Failed to restore {userDataProperty.Name} to the original value. Backup data not found.");
                            continue;
                        }
                        else if (currentValue != backupData.To)
                        {
                            // 检查当前值是否是我们设置的全解锁列表（防止被其他 Mod 修改）
                            MelonLogger.Error($"[Unlock.CollectionHook] Failed to restore {userDataProperty.Name} to the original value. Value changed unexpectedly, incompatible mods loaded?");
                            continue;
                        }
                        userDataProperty.SetValue(userData, backupData.From);
                    }
                    else
                    {
                        // 替换模式：备份原始列表，并替换成全解锁列表
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
        // 角色列表的三种状态：
        // - AllUnlockedList: 全解锁列表（默认状态，界面显示所有角色）
        // - OriginalList: 原始列表（游戏需要修改用户角色数据时切换到这个状态）
        // - ExportList: 导出列表（上传到服务器时，只包含原本拥有的角色 + 被升级过的角色）
        private enum State
        {
            AllUnlockedList,  // 全解锁列表，让界面显示所有角色
            OriginalList,     // 原始列表，让游戏正常修改用户数据
            ExportList        // 导出列表，只上传真正拥有的角色
        }

        // 角色解锁的状态管理：
        // 由于角色可以升级，而升级数据需要保存，所以逻辑比收藏品更复杂
        // 使用状态栈来管理不同方法调用时应该使用哪个角色列表

        // 在这些方法中，游戏会清空并初始化原始列表，需要切换到 OriginalList 状态
        [HarmonyPatch(typeof(UserData), "Initialize")] [HarmonyPrefix] public static void PreInitialize(UserData __instance) { TryInitializeUserState(__instance); PushState("UserData.Initialize", State.OriginalList); }
        [HarmonyPatch(typeof(UserData), "Initialize")] [HarmonyFinalizer] public static void FinInitialize() => PopState("UserData.Initialize", State.OriginalList);
        [HarmonyPatch(typeof(PlInformationProcess), "RestoreCharadata")] [HarmonyPrefix] public static void PreRestoreCharadata() => PushState("PlInformationProcess.RestoreCharadata", State.OriginalList);
        [HarmonyPatch(typeof(PlInformationProcess), "RestoreCharadata")] [HarmonyFinalizer] public static void FinRestoreCharadata() => PopState("PlInformationProcess.RestoreCharadata", State.OriginalList);
        
        // 当游戏添加角色到用户列表时（比如游玩后获得角色），需要添加到原始列表
        [HarmonyPatch(typeof(UserData), "AddCollections")] [HarmonyPrefix] public static void PreAddCollections() => PushState("UserData.AddCollections", State.OriginalList);
        [HarmonyPatch(typeof(UserData), "AddCollections")] [HarmonyFinalizer] public static void FinAddCollections() => PopState("UserData.AddCollections", State.OriginalList);
        
        // 导出用户数据到服务器时，切换到 ExportList 状态
        // ExportList 只包含：原本拥有的角色 + 被解锁角色中升级过的角色
        [HarmonyPatch(typeof(VOExtensions), "ExportUserAll")] [HarmonyPrefix] public static void PreExportUserAll() => PushState("VOExtensions.ExportUserAll", State.ExportList);
        [HarmonyPatch(typeof(VOExtensions), "ExportUserAll")] [HarmonyFinalizer] public static void FinExportUserAll() => PopState("VOExtensions.ExportUserAll", State.ExportList);

        // 记录每个用户的状态信息
        // 包括：原始列表、全解锁列表缓存、状态栈
        private record PropertyChangeLog
        {
            public List<UserChara> OriginalList { get; set; }           // 用户原本拥有的角色列表
            public int CachedOriginalSize { get; set; }                 // 原始列表的大小（用于检测变化）
            public List<UserChara> AllUnlockedListCache { get; set; }   // 全解锁列表的缓存
            public Stack<State> StateStack { get; set; }                // 状态栈
        };
        
        // 使用 ConditionalWeakTable 存储每个 UserData 的状态
        // 当 UserData 被垃圾回收时，对应的状态也会自动清理
        private readonly static ConditionalWeakTable<UserData, PropertyChangeLog> userStateMap = new();

        // 初始化用户的状态管理
        // 在 UserData.Initialize 时调用
        private static void TryInitializeUserState(UserData userData)
        {
            // 如果已存在状态，先移除（重新初始化）
            if (userStateMap.TryGetValue(userData, out _))
            {
                userStateMap.Remove(userData);
            }
            
            // 此时游戏数据可能还没加载完，所以先创建一个空的全解锁列表
            var allUnlockedListCache = new List<UserChara>();
            
            // 创建状态栈，基础状态是 AllUnlockedList
            var stateStack = new Stack<State>();
            stateStack.Push(State.AllUnlockedList);
            
            // 保存状态信息
            userStateMap.Add(
                userData,
                new PropertyChangeLog()
                {
                    OriginalList = userData.CharaList,  // 保存原始角色列表的引用
                    CachedOriginalSize = -1,            // -1 表示尚未缓存
                    AllUnlockedListCache = allUnlockedListCache,
                    StateStack = stateStack
                });
            
            // 将用户的角色列表临时指向全解锁列表（稍后会填充）
            // 真正的填充会在第一次切换回 AllUnlockedList 状态时进行
            userData.CharaList = allUnlockedListCache;
        }

        // 压入新状态到状态栈
        // 如果新状态与栈顶状态不同，则应用新状态（切换用户的 CharaList 引用）
        private static void PushState(string method, State state)
        {
#if DEBUG
            MelonLogger.Msg($"[Unlock.CharacterHook] {method}: {state} push");
#endif
            if (UserDataManager.Instance == null) return;
            
            // 遍历两个玩家位
            for (int i = 0; i < 2; i++)
            {
                var userData = UserDataManager.Instance.GetUserData(i);
                if (!userData.IsEntry || !userStateMap.TryGetValue(userData, out var userState) || userState == null) continue;
                
                var oldStackTop = userState.StateStack.Peek();
                userState.StateStack.Push(state);
                
                // 只有当状态真正改变时才需要切换列表
                if (state != oldStackTop)
                {
                    ApplyState(userData, state);
                }
            }
        }

        // 从状态栈中弹出状态
        // 如果弹出后栈顶状态改变，则应用新的栈顶状态
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
                
                // 检查状态栈是否正常（防止其他 Mod 干扰）
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
                
                // 弹出状态
                userState.StateStack.Pop();
                var newStackTop = userState.StateStack.Peek();
                
                // 只有当状态真正改变时才需要切换列表
                if (state != newStackTop)
                {
                    ApplyState(userData, newStackTop);
                }
            }
        }

        // 应用指定的状态：切换用户的 CharaList 到对应的列表
        private static void ApplyState(UserData userData, State state)
        {
            if (!userStateMap.TryGetValue(userData, out var userState))
            {
                throw new KeyNotFoundException("User data not found in the user state map. This should not happen.");
            }

            var originalList = userState.OriginalList;
            
            if (state == State.OriginalList)
            {
                // 切换到原始列表：让游戏正常修改用户数据
                userData.CharaList = originalList;
            }
            else if (state == State.ExportList)
            {
                // 切换到导出列表：只包含应该上传到服务器的角色
                // 包括：
                // 1. 用户原本拥有的角色
                // 2. 用户原本不拥有，但通过解锁功能使用并升级过的角色
                Dictionary<int, UserChara> originalDict = originalList.ToDictionary(chara => chara.ID);
                userData.CharaList = MaybeMergeUserCharaList(userData, originalList)
                    .Where(chara => originalDict.ContainsKey(chara.ID) || chara.Level > 1)
                    .ToList();
            }
            else if (state == State.AllUnlockedList)
            {
                // 切换到全解锁列表：让界面显示所有角色
                userData.CharaList = MaybeMergeUserCharaList(userData, originalList);
            }
        }

        // 合并原始列表和全解锁列表
        // 
        // 为什么需要合并：
        // - 原始列表中的角色可能被游戏修改（升级、获得新角色等）
        // - 全解锁列表中的角色可能被玩家使用并升级
        // - 需要将两者的数据同步，确保：
        //   1. 原始列表中的角色数据得到保留（玩家真正拥有的）
        //   2. 全解锁列表中被升级的角色数据也得到保留（之后可能上传到服务器）
        // 
        // 通过检查原始列表的大小变化来判断是否需要重新合并（性能优化）
        private static List<UserChara> MaybeMergeUserCharaList(UserData userData, List<UserChara> originalList)
        {
            if (!userStateMap.TryGetValue(userData, out var userState))
            {
                throw new KeyNotFoundException("User data not found in the user state map. This should not happen.");
            }

            // 第一次调用：初始化全解锁列表
            var allUnlockedListEmpty = userState.AllUnlockedListCache.Count == 0;
            if (allUnlockedListEmpty)
            {
                // 从 DataManager 获取所有角色，创建 UserChara 实例
                userState.AllUnlockedListCache.AddRange(DataManager.Instance
                    .GetCharas()
                    .Select(pair => pair.Value)
                    .Select(chara => new UserChara(chara.GetID())));
            }

            // 如果原始列表没有变化，直接返回缓存的全解锁列表（避免重复合并）
            if (!allUnlockedListEmpty && userState.CachedOriginalSize == originalList.Count)
            {
                return userState.AllUnlockedListCache;
            }

            // 原始列表发生了变化（或刚初始化），需要重新合并
            Dictionary<int, UserChara> originalDict = originalList.ToDictionary(chara => chara.ID);
            Dictionary<int, UserChara> cachedDict = userState.AllUnlockedListCache.ToDictionary(chara => chara.ID);
            
            // 将全解锁列表中角色的升级数据同步到原始列表中
            // （如果原始列表中也有该角色）
            foreach (var (id, chara) in cachedDict)
            {
                if (originalDict.TryGetValue(id, out var originalChara) &&
                    originalChara != chara &&                    // 不是同一个实例
                    originalChara.Level < chara.Level)           // 全解锁列表中的等级更高
                {
                    // 复制所有属性（升级数据）到原始角色实例
                    foreach (var property in typeof(UserChara).GetProperties())
                    {
                        if (property.CanWrite)
                        {
                            property.SetValue(originalChara, property.GetValue(chara));
                        }
                    }
                }
            }

            // 重建全解锁列表：
            // - 如果角色在原始列表中存在，使用原始列表的实例（保持引用一致性）
            // - 否则使用缓存中的实例
            userState.AllUnlockedListCache = cachedDict
                .Select(pair =>
                    originalDict.TryGetValue(pair.Key, out var originalChara)
                    ? originalChara
                    : pair.Value)
                .ToList();
            
            // 更新缓存的原始列表大小
            userState.CachedOriginalSize = originalList.Count;
            return userState.AllUnlockedListCache;
        }
    }
}
