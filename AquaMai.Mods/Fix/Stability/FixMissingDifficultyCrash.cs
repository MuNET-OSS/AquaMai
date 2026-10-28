using System;
using System.Collections.Generic;
using System.Linq;
using AquaMai.Config.Attributes;
using HarmonyLib;
using MAI2.Util;
using Manager;
using Manager.MaiStudio;
using MelonLoader;
using Process;

namespace AquaMai.Mods.Fix.Stability;

/**
 * Self-made charts may not have every difficulty (e.g. only Master was imported).
 * The music select screen then cannot select the missing difficulties, but the selected
 * difficulty index is state that survives across songs and across the select countdown,
 * so the game can still start with a difficulty that does not exist for that chart.
 *
 * Such a chart has no notes at all, which makes GameScoreList.TheoryDxScore zero and
 * crashes the game with a DivideByZeroException in AchieveTrackData.Create when the
 * track ends (this is the crash reported for charts imported by maiChartManager).
 *
 * Before the game actually starts, check the selected difficulty and randomly switch to
 * a difficulty that does exist for that chart.
 */
[ConfigSection(
    name: "缺失难度修正",
    en: "When the selected difficulty does not exist for the chart (e.g. self-made charts with missing difficulties), randomly switch to a difficulty that does exist, instead of starting an empty chart and crashing while creating the track result.",
    zh: "当选中的谱面（例如自制谱）不存在所选难度时，随机切换到一个存在的难度，避免以空谱面开局并在结算时除零崩溃。",
    exampleHidden: true,
    defaultOn: true)]
public static class FixMissingDifficultyCrash
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MusicSelectProcess), "GameStart")]
    public static void GameStartPrefix(MusicSelectProcess __instance)
    {
        var difficultySelectIndex = __instance.DifficultySelectIndex;

        for (var player = 0; player < 2; player++)
        {
            var musicId = GameManager.SelectMusicID[player];
            var difficulty = GameManager.SelectDifficultyID[player];

            // Utage charts use a virtual difficulty (Strong) and their note data always lives
            // at index 0 (see TrackStartProcess.OnStart / CustomIntroCinematic). Not a real
            // notesData index, so never treat it as missing and never replace it.
            if (difficulty >= (int)MusicDifficultyID.Strong) continue;

            var music = Singleton<DataManager>.Instance.GetMusic(musicId);
            if (music == null) continue;
            if (IsDifficultyExists(music, difficulty)) continue;

            var candidates = GetFallbackDifficulties(__instance, music, musicId);
            if (candidates.Count == 0)
            {
                // Nothing to fall back to. Let the game do whatever it would do.
                MelonLogger.Warning($"[FixMissingDifficultyCrash] Music {musicId} has no difficulty at all, cannot fix difficulty {difficulty}");
                continue;
            }

            var fallback = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            MelonLogger.Msg($"[FixMissingDifficultyCrash] Music {musicId} has no difficulty {difficulty}, switching to {fallback}");

            GameManager.SelectDifficultyID[player] = fallback;
            if (difficultySelectIndex != null && player < difficultySelectIndex.Length)
            {
                difficultySelectIndex[player] = fallback;
            }
        }
    }

    private static bool IsDifficultyExists(MusicData music, int difficulty)
    {
        if (difficulty < 0 || difficulty >= music.notesData.Count) return false;

        var notes = music.notesData[difficulty];
        if (notes == null || !notes.isEnable) return false;

        // maxNotes is filled by the official data pipeline. Self-made data may leave it empty,
        // so only use it as a positive hint and fall back to the score file otherwise.
        if (notes.maxNotes > 0) return true;
        return HasScoreFile(music, difficulty);
    }

    private static bool HasScoreFile(MusicData music, int difficulty)
    {
        try
        {
            var manager = Singleton<DLMusicScoreManager>.Instance;
            return manager.ExistsMusicScore(music, difficulty, GameManager.TargetID.Left, out _)
                || manager.ExistsMusicScore(music, difficulty, GameManager.TargetID.Right, out _);
        }
        catch (Exception e)
        {
            MelonLogger.Warning($"[FixMissingDifficultyCrash] Failed to check score file of music {music.GetID()} difficulty {difficulty}: {e.Message}");
            return false;
        }
    }

    private static List<int> GetFallbackDifficulties(MusicSelectProcess process, MusicData music, int musicId)
    {
        var existing = new List<int>();
        for (var difficulty = (int)MusicDifficultyID.Basic; difficulty < (int)MusicDifficultyID.Strong; difficulty++)
        {
            if (IsDifficultyExists(music, difficulty)) existing.Add(difficulty);
        }

        if (existing.Count <= 1) return existing;

        // Prefer difficulties that the player can select right now (unlock state, long music rules, ...).
        try
        {
            var playable = process.IsPlayableMusic(musicId);
            var selectable = existing.Where(difficulty => difficulty < playable.Length && playable[difficulty]).ToList();
            if (selectable.Count > 0) return selectable;
        }
        catch (Exception e)
        {
            MelonLogger.Warning($"[FixMissingDifficultyCrash] Failed to get playable difficulties of music {musicId}: {e.Message}");
        }

        return existing;
    }
}
