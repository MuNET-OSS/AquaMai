using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AquaMai.Config.Attributes;
using AquaMai.Config.Types;
using AquaMai.Core.Helpers;
using AquaMai.Core.Resources;
using HarmonyLib;
using MAI2.Util;
using Manager;
using MelonLoader;
using Process;
using Util;

namespace AquaMai.Mods.UX;

[ConfigSection(
    name: "自制谱隐藏",
    en: "One key to hide all self-made charts in the music select process. Or hide for some users.",
    zh: "在选曲界面一键隐藏所有自制谱，或对一部分用户进行隐藏")]
public class HideSelfMadeCharts
{
    [ConfigEntry(
        name: "默认隐藏",
        en: "Hide self-made charts by default when login.",
        zh: "登录时默认隐藏自制谱")]
    public static readonly bool defaultHide = false;

    [ConfigEntry(
        en: "Key to toggle self-made charts.",
        zh: "切换自制谱显示的按键")]
    public static readonly KeyCodeOrName key = KeyCodeOrName.Test;

    [ConfigEntry(name: "长按")] public static readonly bool longPress = false;

    [ConfigEntry(
        name: "黑名单",
        en: "One user ID per line in the file. Hide self-made charts when these users login.",
        zh: "该文件中每行一个用户 ID，当这些用户登录时隐藏自制谱")]
    private static readonly string selfMadeChartsDenyUsersFile = "LocalAssets/SelfMadeChartsDenyUsers.txt";

    [ConfigEntry(
        name: "白名单",
        en: "One user ID per line in the file. Only show self-made charts when these users login.",
        zh: "该文件中每行一个用户 ID，只有这些用户登录时才显示自制谱")]
    private static readonly string selfMadeChartsWhiteListUsersFile = "LocalAssets/SelfMadeChartsWhiteListUsers.txt";

    private static Safe.ReadonlySortedDictionary<int, Manager.MaiStudio.MusicData> _lastInput;
    private static Safe.ReadonlySortedDictionary<int, Manager.MaiStudio.MusicData> _lastFiltered;

    private static bool isShowSelfMadeCharts = true;
    private static bool isForceDisable;

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(DataManager), "GetMusics")]
    public static void GetMusics(ref Safe.ReadonlySortedDictionary<int, Manager.MaiStudio.MusicData> __result, List<string> ____targetDirs)
    {
        if (__result.Count == 0) return;

        var stackFrames = new StackTrace().GetFrames();
        if (stackFrames.All(it => it.GetMethod().DeclaringType.Name != "MusicSelectProcess")) return;
        if (isShowSelfMadeCharts && !isForceDisable) return;

        if (!ReferenceEquals(_lastInput, __result))
        {
            var officialDirs = ____targetDirs
                .Where(it => File.Exists(Path.Combine(it, "DataConfig.xml")) || File.Exists(Path.Combine(it, "OfficialChartsMark.txt")))
                .ToList();
            var nonSelfMadeList = new SortedDictionary<int, Manager.MaiStudio.MusicData>();
            foreach (var music in __result)
            {
                var path = MusicDirHelper.LookupPath(music.Value);
                if (path == null || officialDirs.Any(d => path.StartsWith(d)))
                {
                    nonSelfMadeList.Add(music.Key, music.Value);
                }
            }
            _lastFiltered = new Safe.ReadonlySortedDictionary<int, Manager.MaiStudio.MusicData>(nonSelfMadeList);
            _lastInput = __result;
            MelonLogger.Msg($"[HideSelfMadeCharts] Rebuilt filter: input={__result.Count}, non-self-made={_lastFiltered.Count}");
        }

        __result = _lastFiltered;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MusicSelectProcess), "OnUpdate")]
    public static void MusicSelectProcessOnUpdate(ref MusicSelectProcess __instance)
    {
        if (isForceDisable) return;
        if (!KeyListener.GetKeyDownOrLongPress(key, longPress)) return;
        isShowSelfMadeCharts = !isShowSelfMadeCharts;
        MelonLogger.Msg($"[HideSelfMadeCharts] isShowSelfMadeCharts: {isShowSelfMadeCharts}");
        SharedInstances.ProcessDataContainer.processManager.AddProcess(new FadeProcess(SharedInstances.ProcessDataContainer, __instance, new MusicSelectProcess(SharedInstances.ProcessDataContainer)));
        Task.Run(async () =>
        {
            await Task.Delay(1000);
            MessageHelper.ShowMessage(isShowSelfMadeCharts ? Locale.SelfMadeChartsShow : Locale.SelfMadeChartsHide);
        });
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MusicSelectProcess), "OnStart")]
    public static void MusicSelectProcessOnStart(ref MusicSelectProcess __instance)
    {
        var denyPath = FileSystem.ResolvePath(selfMadeChartsDenyUsersFile);
        if (File.Exists(denyPath))
        {
            var userIds = File.ReadAllLines(denyPath);
            for (var i = 0; i < 2; i++)
            {
                var user = Singleton<UserDataManager>.Instance.GetUserData(i);
                if (!user.IsEntry) continue;
                if (!userIds.Contains(user.Detail.UserID.ToString())) continue;
                isForceDisable = true;
                return;
            }
        }

        var whiteListPath = FileSystem.ResolvePath(selfMadeChartsWhiteListUsersFile);
        if (File.Exists(whiteListPath))
        {
            var userIds = File.ReadAllLines(whiteListPath);
            for (var i = 0; i < 2; i++)
            {
                var user = Singleton<UserDataManager>.Instance.GetUserData(i);
                if (!user.IsEntry) continue;
                if (userIds.Contains(user.Detail.UserID.ToString())) continue;
                isForceDisable = true;
                return;
            }
        }

        isForceDisable = false;
    }


    [HarmonyPostfix]
    [HarmonyPatch(typeof(EntryProcess), "OnStart")]
    public static void EntryProcessOnStart(ref EntryProcess __instance)
    {
        // reset status on login
        isShowSelfMadeCharts = !defaultHide;
    }
}