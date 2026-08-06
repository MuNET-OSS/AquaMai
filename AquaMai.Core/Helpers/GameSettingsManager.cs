using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using AquaMai.Core.Resources;
using AquaMai.Core.Types;
using DB;
using HarmonyLib;
using Manager.UserDatas;
using MelonLoader;
using Monitor;
using Monitor.Game;
using Process.SubSequence;
using UnityEngine;
using Util;

namespace AquaMai.Core.Helpers;

public static class GameSettingsManager
{
    /// <summary>
    /// &lt;#ffffff>
    /// </summary>
    public static readonly string DefaultTag = OptionRootID.DefaultColorTag.GetName();
    /// <summary>
    /// &lt;#02f6ff>
    /// </summary>
    public static readonly string NormalTag = OptionRootID.NormalColorTag.GetName();

    private static bool isPatched = false;
    private static readonly object patchLock = new();

    private static readonly List<IPlayerSettingsItem> settings = [];
    public static void RegisterSetting(IPlayerSettingsItem setting)
    {
        settings.Add(setting);
        settings.Sort((a, b) => a.Sort.CompareTo(b.Sort));
        lock (patchLock)
        {
            if (!isPatched)
            {
                isPatched = true;
                Startup.ApplyPatch(typeof(GameSettingsManager));
                Startup.ApplyPatch(typeof(PatchOptionCategoryIDExtension));
            }
        }
    }

    public static void UnregisterSetting(IPlayerSettingsItem setting)
    {
        settings.Remove(setting);
    }

    public static TabDataBase tab;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MusicSelectMonitor), nameof(MusicSelectMonitor.Initialize))]
    private static void Init(Sprite ____otherSprite)
    {
        if (tab != null) return;
        tab = new TabDataBase(new Color(172 / 255f, 181 / 255f, 250 / 255f), ____otherSprite, Locale.GameSettingsMod);
    }

    /// <summary>
    /// 由于在进入设置界面时 UserOption 会被复制一份，所以我们在这边存储对应关系，方便后面查询
    /// </summary>
    private static Dictionary<UserOption, int> userOptionToPlayer = new();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(OptionSelectSequence), nameof(OptionSelectSequence.OnStartSequence))]
    private static void OnStartSequence(UserOption ____userOption, int ___PlayerIndex)
    {
        userOptionToPlayer[____userOption] = ___PlayerIndex;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(OptionSelectSequence), nameof(OptionSelectSequence.OnGameStart))]
    private static void OnGameStart(UserOption ____userOption)
    {
        userOptionToPlayer.Remove(____userOption);
    }

    private const int ModCategoryId = 5;
    private const int CategoryIdMax = ModCategoryId + 1;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MusicSelectMonitor), nameof(MusicSelectMonitor.SetOptionCard))]
    private static void SetOptionCard(GenreSelectController ____genreTabController, Dictionary<int, Sprite> ____optionSprite)
    {
        List<TabDataBase> list = new List<TabDataBase>();
        for (OptionCategoryID optionCategoryID = OptionCategoryID.SpeedSetting; optionCategoryID < OptionCategoryID.End; optionCategoryID++)
        {
            Sprite sprite = ____optionSprite[(int)optionCategoryID];
            string title = optionCategoryID.GetName();
            Color baseColor = Utility.ConvertColor(optionCategoryID.GetMainColor());
            list.Add(new TabDataBase(baseColor, sprite, title));
        }
        list.Add(tab);
        ____genreTabController.SortType2Genre(list, 0);
        ____genreTabController.Change(0);
    }

    // 改字节码，有意思
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(OptionSelectSequence), "Update")]
    private static IEnumerable<CodeInstruction> OptionSelectSequenceUpdate(IEnumerable<CodeInstruction> instructions)
    {
        var insts = instructions.ToArray();
        var load5s = insts.Where(inst => inst.opcode == OpCodes.Ldc_I4_5).ToArray();
        if (load5s.Length < 9)
        {
            throw new Exception("无法找到 OptionSelectSequence.Update 的 ldc.i4.5 指令");
        }
        // TODO 应该有更好的寻找方式
        load5s[1].opcode = OpCodes.Ldc_I4_6;
        load5s[load5s.Length - 1].opcode = OpCodes.Ldc_I4_6; // 最后一个
        var load4s = insts.Where(inst => inst.opcode == OpCodes.Ldc_I4_4).ToArray();
        if (load4s.Length < 2)
        {
            throw new Exception("无法找到 OptionSelectSequence.Update 的 ldc.i4.4 指令");
        }
        load4s[0].opcode = OpCodes.Ldc_I4_5;
        load4s[1].opcode = OpCodes.Ldc_I4_5;
        return insts;
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(OptionSelectSequence), nameof(OptionSelectSequence.IsOptionBoundary))]
    private static IEnumerable<CodeInstruction> IsOptionBoundary(IEnumerable<CodeInstruction> instructions)
    {
        var insts = instructions.ToArray();
        insts.First(inst => inst.opcode == OpCodes.Ldc_I4_5).opcode = OpCodes.Ldc_I4_6;
        insts.First(inst => inst.opcode == OpCodes.Ldc_I4_4).opcode = OpCodes.Ldc_I4_5;
        return insts;
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(OptionSelectSequence), nameof(OptionSelectSequence.GetCategory))]
    private static IEnumerable<CodeInstruction> GetCategory(IEnumerable<CodeInstruction> instructions)
    {
        var insts = instructions.ToArray();
        insts.First(inst => inst.opcode == OpCodes.Ldc_I4_5).opcode = OpCodes.Ldc_I4_6;
        insts.First(inst => inst.opcode == OpCodes.Ldc_I4_4).opcode = OpCodes.Ldc_I4_5;
        return insts;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(OptionSelectSequence), nameof(OptionSelectSequence.GetCategoryName))]
    private static bool GetCategoryName(int diffIndex, ref string __result, OptionSelectSequence __instance)
    {
        int optionCategoryID = (int)__instance.CurrentOptionCategory + diffIndex;
        if (optionCategoryID >= CategoryIdMax)
        {
            optionCategoryID -= CategoryIdMax;
        }
        if (optionCategoryID < 0)
        {
            optionCategoryID = CategoryIdMax + optionCategoryID;
        }
        if (optionCategoryID == ModCategoryId)
        {
            __result = Locale.GameSettingsMod;
        }
        __result = ((OptionCategoryID)optionCategoryID).GetName();
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(OptionSelectSequence), nameof(OptionSelectSequence.GetCategory))]
    private static void GetCategory(ref bool isLeftButtonActive, ref bool isRightButtonActive, string __result, UserOption __instance)
    {
        var option = settings.FirstOrDefault(it => it.Name == __result);
        if (option == null) return;
        if (!userOptionToPlayer.TryGetValue(__instance, out var player))
        {
            return;
        }
        isLeftButtonActive = option.GetIsLeftButtonActive(player);
        isRightButtonActive = option.GetIsRightButtonActive(player);
    }

    /// <summary>
    /// mai 的代码会传入无效的 index（GetCategory 处）
    /// 所以接下来的访问都必须判断 index
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    private static bool IsValidIndex(int index)
    {
        return index >= 0 && index < settings.Count;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UserOption), "GetOptionName")]
    private static bool GetOptionName(OptionCategoryID category, int optionIndex, ref string __result)
    {
        if ((int)category != ModCategoryId) return true;
        if (!IsValidIndex(optionIndex)) return false;
        __result = settings[optionIndex].Name;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UserOption), "GetOptionDetail")]
    private static bool GetOptionDetail(OptionCategoryID category, int optionIndex, ref string __result)
    {
        if ((int)category != ModCategoryId) return true;
        if (!IsValidIndex(optionIndex)) return false;
        __result = settings[optionIndex].Detail;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UserOption), "GetFilePath")]
    private static bool GetFilePath(OptionCategoryID category, int currentOptionIndex, ref string __result, UserOption __instance)
    {
        if ((int)category != ModCategoryId) return true;
        if (!IsValidIndex(currentOptionIndex)) return false;
        if (!userOptionToPlayer.TryGetValue(__instance, out var player))
        {
            __result = "错误的玩家 ID";
            return false;
        }
        __result = settings[currentOptionIndex].GetSpriteFile(player);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UserOption), "GetOptionValueIndex")]
    private static bool GetOptionValueIndex(OptionCategoryID category, int currentOptionIndex, ref int __result, UserOption __instance)
    {
        if ((int)category != ModCategoryId) return true;
        if (!IsValidIndex(currentOptionIndex) || !userOptionToPlayer.TryGetValue(__instance, out var player))
        {
            __result = 114514;
            return false;
        }
        __result = settings[currentOptionIndex].GetOptionValueIndex(player);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UserOption), "GetOptionMax")]
    private static bool GetOptionMax(OptionCategoryID category, int currentOptionIndex, ref int __result, UserOption __instance)
    {
        if ((int)category != ModCategoryId) return true;
        if (!IsValidIndex(currentOptionIndex) || !userOptionToPlayer.TryGetValue(__instance, out var player))
        {
            __result = 1919810;
            return false;
        }
        __result = settings[currentOptionIndex].GetOptionMax(player);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UserOption), "GetOptionValue")]
    private static bool GetOptionValue(OptionCategoryID category, int optionIndex, ref string __result, string ___DefaultTag, string ___NormalTag, UserOption __instance)
    {
        if ((int)category != ModCategoryId) return true;
        if (!IsValidIndex(optionIndex)) return false;
        if (!userOptionToPlayer.TryGetValue(__instance, out var player))
        {
            __result = "错误的玩家 ID";
            return false;
        }
        __result = settings[optionIndex].GetOptionValue(player);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UserOption), "AddOption")]
    private static bool AddOption(OptionCategoryID category, int currentOptionIndex, UserOption __instance)
    {
        if ((int)category != ModCategoryId) return true;
        if (!IsValidIndex(currentOptionIndex)) return false;
        if (!userOptionToPlayer.TryGetValue(__instance, out var player))
        {
            return false;
        }
        settings[currentOptionIndex].AddOption(player);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UserOption), "SubOption")]
    private static bool SubOption(OptionCategoryID category, int currentOptionIndex, UserOption __instance)
    {
        if ((int)category != ModCategoryId) return true;
        if (!IsValidIndex(currentOptionIndex)) return false;
        if (!userOptionToPlayer.TryGetValue(__instance, out var player))
        {
            return false;
        }
        settings[currentOptionIndex].SubOption(player);
        return false;
    }

    [HarmonyPatch]
    private class PatchOptionCategoryIDExtension
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            // Origin typo
            var clas = Assembly.GetAssembly(typeof(GameCtrl)).GetType("Manager.UserDatas.OptionCategoryIDExcenstion");
            yield return clas.GetMethod("GetCategoryMax");
        }

        public static bool Prefix(ref int __result, OptionCategoryID category)
        {
            if ((int)category != ModCategoryId) return true;
            __result = settings.Count;
            return false;
        }
    }
}