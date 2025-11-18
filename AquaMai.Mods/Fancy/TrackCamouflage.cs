using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using HarmonyLib;
using MAI2.Util;
using Manager;
using Manager.UserDatas;
using MelonLoader;
using Monitor;
using Monitor.MusicSelect.ChainList;
using Process;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using AquaMai.Core.Attributes;
using Tomlet;
using UI.DaisyChainList;
using UnityEngine;
using UnityEngine.UI;

namespace AquaMai.Mods.Fancy;

[ConfigSection(
    "曲目伪装术",
    en: "Mimicking KoP final song's hidden track information feature, better paired with custom intro cinematic feature",
    zh: "仿 KoP 决赛曲揭晓时的曲目信息隐藏特性，配合自定义乐曲开场视频功能使用效果更佳")]
[EnableGameVersion(23000)]
public class TrackCamouflage
{
    [ConfigEntry(
        en: @"Track camouflage info and jacket file directory
Camouflage information filename is ""<Music ID>.toml"", the toml document contains the disguised track name (Name) and artist name (Artist)
Camouflage jacket filename is ""<Music ID>_jacket"", jpg or png image are supported",
        zh: @"曲目伪装信息和封面文件夹的路径
曲目伪装信息的文件名为 <曲目ID>.toml，TOML 文档可填入伪装后的曲目名（Name）和曲师（Artist）
伪装封面的文件名为 <曲目ID>_jacket，支持 jpg 和 png 格式")]
    private static readonly string CamouflageDir = "LocalAssets/Camouflages";

    private static readonly string[] AllowedImageExts = [".jpg", ".jpeg", ".png"];

    private static bool _initialized = false;
    private static readonly Dictionary<int, CamouflageInfo> _camouflagesDict = [];

    [HarmonyPrepare]
    public static bool Initialize()
    {
        if (_initialized)
            return true;

        var resolvedDir = FileSystem.ResolvePath(CamouflageDir.Trim());
        if (!Directory.Exists(resolvedDir))
        {
            MelonLogger.Error($"[TrackCamouflage] Camouflage directory does not exist: {resolvedDir}");
            return false;
        }

        var camouflageDefFiles = Directory.GetFiles(resolvedDir, "*.toml", SearchOption.TopDirectoryOnly);

        foreach (var defFilePath in camouflageDefFiles)
        {
            if (!int.TryParse(Path.GetFileNameWithoutExtension(defFilePath), out int musicID) || musicID <= 0)
                continue;

            MelonLogger.Msg($"[TrackCamouflage] Loading file {Path.GetFileName(defFilePath)} for music ID {musicID}");

            CamouflageInfo parsedData;
            try
            {
                var parsedDoc = TomlParser.ParseFile(defFilePath);
                parsedData = new CamouflageInfo();

                if (parsedDoc.ContainsKey("Name"))
                {
                    var str = parsedDoc.GetString("Name");
                    if (!string.IsNullOrWhiteSpace(str))
                        parsedData.Name = str;
                }

                if (parsedDoc.ContainsKey("Artist"))
                {
                    var str = parsedDoc.GetString("Artist");
                    if (!string.IsNullOrWhiteSpace(str))
                        parsedData.Artist = str;
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[TrackCamouflage] Failed to parse information for music {musicID}, skipping: {e}");
                continue;
            }

            if (parsedData == null)
                continue;

            var jacketFiles = Directory.GetFiles(resolvedDir, $"{musicID}_jacket.*", SearchOption.TopDirectoryOnly);
            foreach (var jacketFilePath in jacketFiles)
            {
                if (!AllowedImageExts.Contains(Path.GetExtension(jacketFilePath).ToLowerInvariant()))
                    continue;

                try
                {
                    var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    texture.LoadImage(File.ReadAllBytes(jacketFilePath));
                    parsedData.JacketTexture = texture;
                    break;
                }
                catch (Exception e)
                {
                    MelonLogger.Warning($"[TrackCamouflage] Failed to read jacket file {jacketFilePath}: {e}");
                }
            }

            _camouflagesDict[musicID] = parsedData;
        }

        MelonLogger.Msg($"[TrackCamouflage] Loaded {_camouflagesDict.Count} file(s)");
        _initialized = true;
        return true;
    }

    #region MusicChainCard Patch
    public static void ApplyCamouflageOnMusicChainCard(MusicChainCardObejct card, MusicSelectProcess.MusicSelectData musicSelectData, int difficulty, AssetManager assetManager, int playerIndex = 0)
    {
        if (!CamouflageCheck(musicSelectData.MusicData.GetID(), out CamouflageInfo info))
            return;

        var jacketTexture = info.JacketTexture ?? assetManager.GetJacketThumbTexture2D("Jacket_S/UI_Jacket_000000_S.png");

        card.SetMusicData(
            info.Name,
            info.Artist,
            musicSelectData.ScoreData[difficulty].notesDesigner.str,
            musicSelectData.MusicData.bpm,
            jacketTexture,
            difficulty);

        // Hide the copyright information to reduce chance to get the clue
        card.SetCopyright(null);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MusicSelectChainList), "SetChainData")]
    public static void PostSetChainData(AssetManager ___AssetManager, IMusicSelectProcess ___SelectProcess, MusicChainCardObejct card, int index, int difficulty)
    {
        var combineMusic = ___SelectProcess.GetCombineMusic(index);
        var combineMusicID = combineMusic.GetID(___SelectProcess.ScoreType);

        // Skip "WaitConnect" and "Random" card
        if (combineMusicID == 2 || combineMusicID == 3)
            return;

        ApplyCamouflageOnMusicChainCard(card, combineMusic.musicSelectData[(int)___SelectProcess.ScoreType], difficulty, ___AssetManager);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MenuChainList), "Deploy")]
    public static void PostDeployMenuChainList(IMusicSelectProcess ___SelectProcess, ChainObject[] ___SpotArray, int ___MonitorIndex, AssetManager ___AssetManager)
    {
        // Find the menu card that is for track information
        MenuCardObject spot = null;
        foreach (var spotTmp in ___SpotArray)
        {
            if (spotTmp != null && ((MenuCardObject)spotTmp).MusicCardObject.gameObject.activeInHierarchy)
                spot = (MenuCardObject)spotTmp;
        }
        if (spot == null)
            return;

        var combineMusic = ___SelectProcess.GetCombineMusic(0);

        var selectedDifficulty = ___SelectProcess.DifficultySelectIndex[___MonitorIndex];
        if (selectedDifficulty == -1)
            selectedDifficulty = 0;

        ApplyCamouflageOnMusicChainCard(spot.MusicCardObject, combineMusic.musicSelectData[(int)___SelectProcess.ScoreType], selectedDifficulty, ___AssetManager);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MenuChainList), "ChangeDifficulty")]
    public static void PostChangeDifficultyMenuChainList(IMusicSelectProcess ___SelectProcess, ChainObject[] ___SpotArray, AssetManager ___AssetManager, MusicDifficultyID difficulty)
    {
        var spot = (MenuCardObject)___SpotArray[4];
        var combineMusic = ___SelectProcess.GetCombineMusic(0);

        ApplyCamouflageOnMusicChainCard(spot.MusicCardObject, combineMusic.musicSelectData[(int)___SelectProcess.ScoreType], (int)difficulty, ___AssetManager);
    }
    #endregion

    #region MusicSelectProcess Patch
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MusicSelectProcess), "ChangeBGM")]
    public static void PostChangeBGM(MusicSelectProcess __instance, List<ReadOnlyCollection<MusicSelectProcess.CombineMusicSelectData>> ____combineMusicDataList)
    {
        // Copy the check from original code just to make sure everything is okay
        if (__instance.CurrentCategorySelect >= ____combineMusicDataList.Count || __instance.CurrentMusicSelect >= ____combineMusicDataList[__instance.CurrentCategorySelect].Count)
            return;

        var combineMusic = ____combineMusicDataList[__instance.CurrentCategorySelect][__instance.CurrentMusicSelect];
        var combineMusicID = combineMusic.GetID(__instance.ScoreType);
        if (combineMusicID == 0 || combineMusicID == 2 || combineMusicID == 3)
            return;

        // Stop the track preview if selected track have camouflage
        if (CamouflageCheck(combineMusic.musicSelectData[(int)__instance.ScoreType].MusicData.GetID(), out _))
            SoundManager.PreviewEnd();
    }

    public static Texture2D HijackGenreTexture2D(AssetManager assetManager, string origThumbnailName, MusicSelectProcess.MusicSelectData musicSelectData)
    {
        if (!CamouflageCheck(musicSelectData.MusicData.GetID(), out CamouflageInfo info))
            return assetManager.GetJacketThumbTexture2D(origThumbnailName);

        return info.JacketTexture ?? assetManager.GetJacketThumbTexture2D("Jacket_S/UI_Jacket_000000_S.png");
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(MusicSelectProcess), "GetGenreTextureList")]
    [HarmonyPatch(typeof(MusicSelectProcess), "GetGenreTexture")]
    public static IEnumerable<CodeInstruction> InjectGetGenreTexture(IEnumerable<CodeInstruction> inst, MethodBase method)
    {
        var methodName = method.Name;
        // Just to make sure
        if (!methodName.Equals("GetGenreTextureList") && !methodName.Equals("GetGenreTexture"))
            return inst;

        try
        {
            var methodBody = method.GetMethodBody();
            LocalVariableInfo combineMusicSelectDataLocal = null;
            foreach (var local in methodBody.LocalVariables)
            {
                if (local.LocalType.FullName.Contains("CombineMusicSelectData"))
                    combineMusicSelectDataLocal = local;
            }

            if (combineMusicSelectDataLocal == null)
            {
                MelonLogger.Error($"[TrackCamouflage] Can't find CombineMusicSelectData local variable in MusicSelectProcess.{methodName}, skipping!");
                if (_initialized)
                    _initialized = false;
                return inst;
            }

            var matcher = new CodeMatcher(inst);

            switch (methodName)
            {
                case "GetGenreTextureList":
                    matcher.MatchStartForward(
                            // genreTextureList
                            new CodeMatch(OpCodes.Ldloc_0),

                            // index2
                            new CodeMatch(i => i.opcode == OpCodes.Ldloc_S && ((LocalBuilder)i.operand).LocalType.Equals(typeof(int))),

                            // this.container.assetManager
                            new CodeMatch(OpCodes.Ldarg_0),
                            new CodeMatch(i => i.opcode == OpCodes.Ldfld && ((FieldInfo)i.operand).Name.Equals("container")),
                            new CodeMatch(i => i.opcode == OpCodes.Ldfld && ((FieldInfo)i.operand).Name.Equals("assetManager")),

                            // thumbnailName
                            new CodeMatch(i => i.opcode == OpCodes.Ldloc_S && ((LocalBuilder)i.operand).LocalType.Equals(typeof(string))),

                            // (assetManager).GetJacketThumbTexture2D(thumbnailName)
                            new CodeMatch(i => i.opcode == OpCodes.Callvirt && ((MethodInfo)i.operand).Name.Equals("GetJacketThumbTexture2D"))
                        )
                        .Advance(6);
                    break;
                case "GetGenreTexture":
                    matcher.MatchStartForward(
                            // this.container.assetManager
                            new CodeMatch(OpCodes.Ldarg_0),
                            new CodeMatch(i => i.opcode == OpCodes.Ldfld && ((FieldInfo)i.operand).Name.Equals("container")),
                            new CodeMatch(i => i.opcode == OpCodes.Ldfld && ((FieldInfo)i.operand).Name.Equals("assetManager")),

                            // V_8 (thumbnailName)
                            new CodeMatch(i => i.opcode == OpCodes.Ldloc_S && ((LocalBuilder)i.operand).LocalType.Equals(typeof(string))),

                            // (assetManager).GetJacketThumbTexture2D(V_8)
                            new CodeMatch(i => i.opcode == OpCodes.Callvirt && ((MethodInfo)i.operand).Name.Equals("GetJacketThumbTexture2D"))
                        )
                        .Advance(4);
                    break;
            }

            matcher.InsertAndAdvance(
                    // Add new argument to the stack: combineMusicSelectData.musicSelectData[(int)this.ScoreType]
                    new CodeInstruction(OpCodes.Ldloc_S, combineMusicSelectDataLocal.LocalIndex),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MusicSelectProcess.CombineMusicSelectData), "musicSelectData")),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(MusicSelectProcess), "ScoreType")),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(List<MusicSelectProcess.MusicSelectData>), "Item"))
                )
                // Hijack the jacket thumbnail call
                .Set(OpCodes.Call, AccessTools.Method(typeof(TrackCamouflage), nameof(HijackGenreTexture2D)));

            MelonLogger.Msg($"[TrackCamouflage] Successfully injected method MusicSelectProcess.{methodName}");
            return matcher.Instructions();
        }
        catch (Exception e)
        {
            MelonLogger.Error($"[TrackCamouflage] Failed to inject method MusicSelectProcess.{methodName}: {e}");

            // Disable the feature to prevent unexpected behavior
            if (_initialized)
                _initialized = false;

            return inst;
        }
    }
    #endregion

    #region TrackStartMonitor Patch
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TrackStartMonitor), "SetTrackStart")]
    public static void InjectTrackStartMonitor(int ___monitorIndex, AssetManager ____assetManager, CustomTextScroll ____musicName, CustomTextScroll ____artistName, RawImage ____jacket)
    {
        var musicID = GameManager.SelectMusicID[___monitorIndex];

        if (!CamouflageCheck(musicID, out CamouflageInfo info))
            return;

        ____musicName.SetData(info.Name);
        ____artistName.SetData(info.Artist);
        ____jacket.texture = info.JacketTexture ?? ____assetManager.GetJacketTexture2D("Jacket/UI_Jacket_000000.png");
    }
    #endregion

    #region AssetManager Patch
    // Most parts of the game using this method to get jackets except others mentioned from above so I think that'll do the rest

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AssetManager), "GetJacketTexture2D", argumentTypes: [typeof(int)])]
    public static bool PreGetJacketTexture2DFromID(ref Texture2D __result, int id, AssetManager __instance)
    {
        if (!CamouflageCheck(id, out CamouflageInfo info))
            return true;

        __result = info.JacketTexture ?? __instance.GetJacketTexture2D("Jacket/UI_Jacket_000000.png");
        return false;
    }

    // Seems no one is using this method but just make sure...
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AssetManager), "GetJacketThumbTexture2D", argumentTypes: [typeof(int)])]
    public static bool PreGetJacketThumbTexture2DFromID(ref Texture2D __result, int id, AssetManager __instance)
    {
        if (!CamouflageCheck(id, out CamouflageInfo info))
            return true;

        __result = info.JacketTexture ?? __instance.GetJacketThumbTexture2D("Jacket_S/UI_Jacket_000000_S.png");
        return false;
    }
    #endregion

    #region Utilities
    private static bool CamouflageCheck(int musicID, out CamouflageInfo info)
    {
        info = null;

        // Ignore if wasn't initialized
        if (!_initialized)
            return false;

        // Check ID match
        if (!_camouflagesDict.TryGetValue(musicID, out info))
            return false;

        // Check if any player already played the track
        for (int i = 0; i < 4; ++i)
        {
            var playerData = Singleton<UserDataManager>.Instance.GetUserData(i);
            if (playerData != null)
            {
                for (int j = 0; j < 6; ++j)
                {
                    playerData.ScoreDic[j].TryGetValue(musicID, out UserScore musicScore);
                    if (musicScore == null)
                        return true;
                }
            }
        }

        return false;
    }

    public class CamouflageInfo
    {
        public string Name { get; set; } = "???";
        public string Artist { get; set; } = "???";
        public Texture2D JacketTexture { get; set; } = null;
    }
    #endregion
}
