using System;
using System.Collections.Generic;
using AquaMai.Config.Attributes;
using HarmonyLib;
using IO;
using Manager;
using MelonLoader;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "触摸区域重映射",
    en: "Remap touch panel areas per player.",
    zh: "按玩家重映射触摸区域。",
    exampleHidden: true)]
public class TouchAreaRemapper
{
    private const int TouchAreaCount = 34;
    private const ulong TouchMask = (1UL << TouchAreaCount) - 1UL;
    private static readonly string[] AreaNames =
    [
        "A1", "A2", "A3", "A4", "A5", "A6", "A7", "A8",
        "B1", "B2", "B3", "B4", "B5", "B6", "B7", "B8",
        "C1", "C2",
        "D1", "D2", "D3", "D4", "D5", "D6", "D7", "D8",
        "E1", "E2", "E3", "E4", "E5", "E6", "E7", "E8",
    ];

    private static readonly int[][] RemapTables = new int[2][];
    private static readonly Dictionary<string, int> AreaIndexes = CreateAreaIndexes();

    [ConfigEntry(
        name: "1P 映射表",
        en: """
            Remapping table for 1P, written in the physical press order A1-A8 B1-B8 C1-C2 D1-D8 E1-E8.
            Each item is the area reported by the game when that physical area is pressed.
            The 34 items must contain every area exactly once; duplicated or missing areas are not reversible.
            Leave empty to disable remapping for 1P.
            """,
        zh: """
            1P 映射表，按实际按下 A1-A8 B1-B8 C1-C2 D1-D8 E1-E8 的顺序填写。
            每一项是按下该物理区域时游戏收到的区域名。
            34 项必须刚好包含每个区域一次；重复或缺失区域无法反推。
            留空则不重映射 1P。
            """)]
    private static readonly string map1P = "";

    [ConfigEntry(
        name: "2P 映射表",
        en: """
            Remapping table for 2P, with the same format as 1P.
            Leave empty to disable remapping for 2P.
            """,
        zh: """
            2P 映射表，格式同 1P。
            留空则不重映射 2P。
            """)]
    private static readonly string map2P = "";

    public static void OnBeforePatch()
    {
        RemapTables[0] = ParseMap(map1P, 0);
        RemapTables[1] = ParseMap(map2P, 1);
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(InputManager), "SetNewTouchPanel")]
    public static void SetNewTouchPanel(uint index, ref ulong inputData)
    {
        if (index >= RemapTables.Length)
        {
            return;
        }

        var remapTable = RemapTables[index];
        if (remapTable == null)
        {
            return;
        }

        inputData = Remap(inputData, remapTable);
    }

    public static bool TryGetReportedAreaIndex(uint playerIndex, int physicalIndex, out int reportedIndex)
    {
        reportedIndex = physicalIndex;
        if (playerIndex >= RemapTables.Length || physicalIndex < 0 || physicalIndex >= TouchAreaCount)
        {
            return false;
        }

        var remapTable = RemapTables[playerIndex];
        if (remapTable == null)
        {
            return false;
        }

        reportedIndex = remapTable[physicalIndex];
        return true;
    }

    private static ulong Remap(ulong inputData, int[] remapTable)
    {
        var remapped = inputData & ~TouchMask;
        for (var physicalIndex = 0; physicalIndex < TouchAreaCount; physicalIndex++)
        {
            var reportedIndex = remapTable[physicalIndex];
            if ((inputData & (1UL << reportedIndex)) != 0)
            {
                remapped |= 1UL << physicalIndex;
            }
        }

        return remapped;
    }

    private static int[] ParseMap(string value, int playerIndex)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var tokens = value.Split([' ', ',', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length != TouchAreaCount)
        {
            MelonLogger.Warning($"[TouchAreaRemapper] {playerIndex + 1}P map should contain {TouchAreaCount} areas, got {tokens.Length}. Remapping disabled for this player.");
            return null;
        }

        var result = new int[TouchAreaCount];
        var used = new bool[TouchAreaCount];
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i].Trim().ToUpperInvariant();
            if (!AreaIndexes.TryGetValue(token, out var areaIndex))
            {
                MelonLogger.Warning($"[TouchAreaRemapper] Unknown area \"{tokens[i]}\" in {playerIndex + 1}P map. Remapping disabled for this player.");
                return null;
            }

            if (used[areaIndex])
            {
                MelonLogger.Warning($"[TouchAreaRemapper] Duplicated area \"{token}\" in {playerIndex + 1}P map. Remapping disabled for this player.");
                return null;
            }

            used[areaIndex] = true;
            result[i] = areaIndex;
        }

        for (var i = 0; i < used.Length; i++)
        {
            if (!used[i])
            {
                MelonLogger.Warning($"[TouchAreaRemapper] Missing area \"{AreaNames[i]}\" in {playerIndex + 1}P map. Remapping disabled for this player.");
                return null;
            }
        }

        MelonLogger.Msg($"[TouchAreaRemapper] {playerIndex + 1}P map loaded");
        return result;
    }

    private static Dictionary<string, int> CreateAreaIndexes()
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < AreaNames.Length; i++)
        {
            result[AreaNames[i]] = i;
        }

        return result;
    }
}
