using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using AquaMai.Config.Attributes;
using HarmonyLib;
using MAI2.Util;
using Manager;
using MelonLoader;
using Monitor;
using Process;
using UnityEngine;
using Util;
using BuildInfo = AquaMai.Core.BuildInfo;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "定数排序",
    en: "Add a constant difficulty sort tab to the music selection screen.",
    zh: "在选曲界面添加按谱面定数排序的标签页")]
public class SongConstantSort
{
    public static void OnBeforePatch()
    {
        ConstNameStore.SpriteProvider = ConstSpriteCache.GetSprite;
        MelonLogger.Msg("[SongConstantSort] Initialized");
    }

    private static class ConstNameStore
    {
        public static readonly Dictionary<int, string> NameMap = new Dictionary<int, string>();
        public static Func<int, Sprite> SpriteProvider;
        public static bool IsActive;
    }

    private static class ConstSpriteCache
    {
        private static Dictionary<int, Sprite> _sprites;
        private static bool _loaded;

        private static Stream GetAssetBundleStream()
        {
            var s = BuildInfo.ModAssembly.Assembly.GetManifestResourceStream("level.ab");
            if (s != null)
            {
                return s;
            }

            s = BuildInfo.ModAssembly.Assembly.GetManifestResourceStream("level.ab.compressed");
            return new DeflateStream(s, CompressionMode.Decompress);
        }

        public static Sprite GetSprite(int categoryId)
        {
            if (!_loaded)
            {
                _loaded = true;
                _sprites = new Dictionary<int, Sprite>();
                try
                {
                    using var stream = GetAssetBundleStream();
                    if (stream == null) return null;
                    var bundle = AssetBundle.LoadFromStream(stream);
                    if (bundle == null) return null;

                    foreach (string assetName in bundle.GetAllAssetNames())
                    {
                        var sprite = bundle.LoadAsset<Sprite>(assetName);
                        if (sprite == null) continue;
                        string fname = Path.GetFileNameWithoutExtension(assetName);
                        string numPart = fname.Replace("const_", "").Replace("_", "");
                        if (int.TryParse(numPart, out int id))
                            _sprites[id] = sprite;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning("[SongConstantSort] Failed to load AB: " + ex.Message);
                }
            }
            return _sprites.TryGetValue(categoryId, out Sprite s) ? s : null;
        }
    }

    [HarmonyPatch]
    public static class SortTabPatches
    {
        [HarmonyPatch(typeof(DB.SortTabIDEnum), "GetEnd", [])]
        [HarmonyPrefix]
        public static bool GetEnd_Static(ref int __result) { __result = 7; return false; }

        [HarmonyPatch(typeof(DB.SortTabIDEnum), "GetEnd", [typeof(DB.SortTabID)])]
        [HarmonyPrefix]
        public static bool GetEnd_Instance(ref int __result) { __result = 7; return false; }

        [HarmonyPatch(typeof(DB.SortTabIDEnum), "IsValid")]
        [HarmonyPrefix]
        public static bool IsValid(DB.SortTabID self, ref bool __result)
        {
            __result = self >= DB.SortTabID.Genre && (int)self <= 6;
            return false;
        }

        [HarmonyPatch(typeof(DB.SortTabIDEnum), "GetName")]
        [HarmonyPrefix]
        public static bool GetName(DB.SortTabID self, ref string __result)
        { if ((int)self == 6) { __result = "定数"; return false; } return true; }

        [HarmonyPatch(typeof(DB.SortTabIDEnum), "GetDetail")]
        [HarmonyPrefix]
        public static bool GetDetail(DB.SortTabID self, ref string __result)
        { if ((int)self == 6) { __result = "譜面定数でタブ分けされます"; return false; } return true; }

        [HarmonyPatch(typeof(DB.SortTabIDEnum), "GetFilePath")]
        [HarmonyPrefix]
        public static bool GetFilePath(DB.SortTabID self, ref string __result)
        { if ((int)self == 6) { __result = "UI_MSS_Tabimage_01_03"; return false; } return true; }

        [HarmonyPatch(typeof(DB.SortTabIDEnum), "GetEnumName")]
        [HarmonyPrefix]
        public static bool GetEnumName(DB.SortTabID self, ref string __result)
        { if ((int)self == 6) { __result = "Constant"; return false; } return true; }

        [HarmonyPatch(typeof(DB.SortTabIDEnum), "GetEnumValue")]
        [HarmonyPrefix]
        public static bool GetEnumValue(DB.SortTabID self, ref int __result)
        { if ((int)self == 6) { __result = 6; return false; } return true; }
    }

    [HarmonyPatch]
    public static class NavigationPatches
    {
        private static readonly FieldInfo _beforeSortField =
            typeof(Process.MusicSelectProcess).GetField("_beforeCategorySortSetting",
                BindingFlags.NonPublic | BindingFlags.Instance);

        [HarmonyPatch(typeof(Process.MusicSelectProcess), "AddSort")]
        [HarmonyPrefix]
        public static bool AddSort_Prefix(Process.MusicSelectProcess __instance, DB.SortRootID root)
        {
            if (root != DB.SortRootID.Tab) return true;
            int next = (int)(DB.SortTabID)_beforeSortField.GetValue(__instance) + 1;
            if (next >= 7) next = 0;
            _beforeSortField.SetValue(__instance, (DB.SortTabID)next);
            return false;
        }

        [HarmonyPatch(typeof(Process.MusicSelectProcess), "SubSort")]
        [HarmonyPrefix]
        public static bool SubSort_Prefix(Process.MusicSelectProcess __instance, DB.SortRootID root)
        {
            if (root != DB.SortRootID.Tab) return true;
            int next = (int)(DB.SortTabID)_beforeSortField.GetValue(__instance) - 1;
            if (next < 0) next = 6;
            _beforeSortField.SetValue(__instance, (DB.SortTabID)next);
            return false;
        }
    }

    [HarmonyPatch]
    public static class CategoryTabPatches
    {
        private static FieldInfo _combineMusicDataListField;
        private static PropertyInfo _categoryNameListProp;
        private static FieldInfo _categorySortSettingField;
        private static MethodInfo _setSortListMethod;
        private static MethodInfo _addRandomDataMethod;

        [HarmonyPatch(typeof(Process.MusicSelectProcess), "CategoryTabSort")]
        [HarmonyPrefix]
        public static bool CategoryTabSort_Prefix(
            Process.MusicSelectProcess __instance, int player,
            ref SortedList<int, List<Process.MusicSelectProcess.CombineMusicSelectData>> __result)
        {
            if (_categorySortSettingField == null)
                _categorySortSettingField = typeof(Process.MusicSelectProcess).GetField(
                    "_categorySortSetting", BindingFlags.NonPublic | BindingFlags.Instance);

            var sortSetting = (DB.SortTabID)_categorySortSettingField.GetValue(__instance);

            if ((int)sortSetting != 6) return true;

            ConstNameStore.IsActive = true;
            __result = DoCategoryTabConstant(__instance, player);
            ConstNameStore.IsActive = false;
            return false;
        }

        private static SortedList<int, List<Process.MusicSelectProcess.CombineMusicSelectData>>
            DoCategoryTabConstant(Process.MusicSelectProcess instance, int player)
        {
            InitReflection();

            var musicList = (List<ReadOnlyCollection<Process.MusicSelectProcess.CombineMusicSelectData>>)
                _combineMusicDataListField.GetValue(instance);
            var categoryNameList = (List<string>)_categoryNameListProp.GetValue(instance);

            var grouped = new SortedList<int, List<Process.MusicSelectProcess.CombineMusicSelectData>>();
            var nameMap = new SortedList<int, string>();
            var seen = new HashSet<int>();

            for (int diff = 0; diff <= 4; diff++)
            {
                for (int j = 0; j < musicList.Count; j++)
                {
                    for (int k = 0; k < musicList[j].Count; k++)
                    {
                        var cd = musicList[j][k];

                        for (MAI2System.ConstParameter.ScoreKind sk =
                             MAI2System.ConstParameter.ScoreKind.Standard;
                             sk <= MAI2System.ConstParameter.ScoreKind.Deluxe; sk++)
                        {
                            var msd = cd.musicSelectData[(int)sk];
                            if (msd == null || msd.Difficulty != 0) continue;

                            int musicId = cd.GetID(sk);
                            if (musicId >= 100000) continue;

                            var notesDict = Singleton<NotesListManager>.Instance.GetNotesList();
                            if (!notesDict.TryGetValue(musicId, out var nw)) continue;

                            if (diff >= 4 && (nw.IsEnable.Count <= 4 || !nw.IsEnable[4]))
                                continue;

                            Manager.MaiStudio.Notes notes = nw.NotesList[diff];
                            if (notes == null || !notes.isEnable) continue;

                            int uniqueKey = musicId * 100 + diff;
                            if (seen.Contains(uniqueKey)) continue;
                            seen.Add(uniqueKey);

                            int constKey = notes.level * 10 + notes.levelDecimal;

                            var detail = new MusicSelectProcess.MusicSelectDetailData
                            {
                                musicId = musicId,
                                difficultyId = diff,
                                startLife = cd.msDetailData.startLife,
                                challengeUnlockDiff = cd.msDetailData.challengeUnlockDiff,
                                nextRelaxDay = cd.msDetailData.nextRelaxDay,
                                infoEnable = cd.msDetailData.infoEnable,
                                jumpOtherCategoryStandard = cd.msDetailData.jumpOtherCategoryStandard,
                                jumpOtherCategoryDeluxe = cd.msDetailData.jumpOtherCategoryDeluxe,
                                jumpOtherCategoryLevelStd = cd.msDetailData.jumpOtherCategoryLevelStd,
                                jumpOtherCategoryLevelDx = cd.msDetailData.jumpOtherCategoryLevelDx,
                            };

                            int dummy = -1;
                            if (!grouped.ContainsKey(constKey))
                            {
                                nameMap[constKey] = string.Format("Lv.{0}.{1}",
                                    notes.level, notes.levelDecimal);
                                grouped[constKey] = new List<Process.MusicSelectProcess.CombineMusicSelectData>();
                            }
                            grouped[constKey].Add(
                                new Process.MusicSelectProcess.CombineMusicSelectData(detail, ref dummy, false));
                        }
                    }
                }
            }

            ConstNameStore.NameMap.Clear();
            foreach (var kv in nameMap)
                ConstNameStore.NameMap[kv.Key] = kv.Value;

            categoryNameList.Clear();
            foreach (int key in nameMap.Keys)
                categoryNameList.Add(nameMap[key]);

            var result = new SortedList<int, List<Process.MusicSelectProcess.CombineMusicSelectData>>();
            foreach (int key in grouped.Keys)
            {
                var list = new List<Process.MusicSelectProcess.CombineMusicSelectData>(grouped[key]);

                object[] sortArgs = { player, key + 1000, list, false };
                _setSortListMethod.Invoke(instance, sortArgs);
                list = (List<Process.MusicSelectProcess.CombineMusicSelectData>)sortArgs[2];

                object[] randArgs = { list };
                _addRandomDataMethod.Invoke(instance, randArgs);
                list = (List<Process.MusicSelectProcess.CombineMusicSelectData>)randArgs[0];

                result[key] = list;
            }
            return result;
        }

        private static void InitReflection()
        {
            if (_combineMusicDataListField == null)
                _combineMusicDataListField = typeof(Process.MusicSelectProcess).GetField(
                    "_combineMusicDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            if (_categoryNameListProp == null)
                _categoryNameListProp = typeof(Process.MusicSelectProcess).GetProperty(
                    "CategoryNameList", BindingFlags.Public | BindingFlags.Instance);
            if (_setSortListMethod == null)
                _setSortListMethod = typeof(Process.MusicSelectProcess).GetMethod(
                    "SetSortList", BindingFlags.NonPublic | BindingFlags.Instance);
            if (_addRandomDataMethod == null)
                _addRandomDataMethod = typeof(Process.MusicSelectProcess).GetMethod(
                    "AddRandomData", BindingFlags.NonPublic | BindingFlags.Instance);
        }
    }

    [HarmonyPatch]
    public static class UIPatches
    {
        private static FieldInfo _musicSelectField;
        private static PropertyInfo _categorySortSettingProp;

        private static DB.SortTabID GetCategorySortSetting(MusicSelectMonitor monitor)
        {
            if (_musicSelectField == null)
                _musicSelectField = typeof(MusicSelectMonitor).GetField("_musicSelect",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            var msp = _musicSelectField.GetValue(monitor);

            if (_categorySortSettingProp == null)
                _categorySortSettingProp = msp.GetType().GetProperty("CategorySortSetting",
                    BindingFlags.Public | BindingFlags.Instance);
            return (DB.SortTabID)_categorySortSettingProp.GetValue(msp);
        }

        private static int ConstKeyToLevelEnum(int categoryID)
        {
            int level = categoryID / 10;
            if (level < 1) level = 1;
            if (level > 15) level = 15;
            bool isPlus = (categoryID % 10) >= 7 && level >= 7;
            if (level < 7) return level;
            if (isPlus) return 2 * level - 6;
            return 2 * level - 7;
        }

        [HarmonyPatch(typeof(MusicSelectMonitor), "getTabString")]
        [HarmonyPrefix]
        public static bool getTabString_Prefix(MusicSelectMonitor __instance,
            Process.MusicSelectProcess.GenreSelectData data, ref string __result)
        {
            if ((int)GetCategorySortSetting(__instance) != 6) return true;
            if (data.isExtra) return true;
            if (ConstNameStore.NameMap.TryGetValue(data.categoryID, out string name))
            {
                __result = name;
                return false;
            }
            __result = "?";
            return false;
        }

        [HarmonyPatch(typeof(MusicSelectMonitor), "getTabColor")]
        [HarmonyPrefix]
        public static bool getTabColor_Prefix(MusicSelectMonitor __instance,
            Process.MusicSelectProcess.GenreSelectData data, ref Color __result)
        {
            if ((int)GetCategorySortSetting(__instance) != 6) return true;
            if (data.isExtra) return true;
            int levelEnum = ConstKeyToLevelEnum(data.categoryID);
            var levelData = Singleton<DataManager>.Instance.GetMusicLevel(levelEnum);
            if (levelData != null)
            {
                __result = Utility.ConvertColor(levelData.Color);
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(MusicSelectMonitor), "GetTabSprite")]
        [HarmonyPrefix]
        public static bool GetTabSprite_Prefix(MusicSelectMonitor __instance,
            Process.MusicSelectProcess.GenreSelectData data, ref Sprite __result)
        {
            if ((int)GetCategorySortSetting(__instance) != 6) return true;
            if (data.isExtra) return true;

            if (ConstNameStore.SpriteProvider != null)
            {
                Sprite custom = ConstNameStore.SpriteProvider(data.categoryID);
                if (custom != null)
                {
                    __result = custom;
                    return false;
                }
            }
            return true;
        }
    }
}
