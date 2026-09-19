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
using Tomlet.Exceptions;
using Tomlet.Models;
using UI.DaisyChainList;
using UnityEngine;
using UnityEngine.UI;
using Manager.MaiStudio;

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
- Camouflage info filename is <Music ID>.toml. TOML fields:
  - Name: disguised track name
  - Artist: disguised artist
  - NoteDesigner: disguised note designer (optional; if omitted, the real note designer is shown)
    - Or set HideNoteDesigner = true to hide it and show ""-"" like Basic/Advanced charts
  - UnlockScore: minimum achievement % to remove camouflage, e.g. 97.0
    - Optional, default 0 — any existing score removes camouflage; set to 102 to keep camouflage forever
- Camouflage jacket filename is <Music ID>_jacket.jpg/png. Only jpg/png are supported (not .ab).",
        zh: @"曲目伪装信息和封面文件夹的路径
- 曲目伪装信息的文件名为 <曲目ID>.toml，TOML 文档可填入的字段包括：
  - Name: 伪装后的曲目名
  - Artist: 伪装后的曲师
  - NoteDesigner：伪装后的谱师（可选，如不指定则默认不伪装，显示原本的谱师）
    - 或者：亦可用 HideNoteDesigner = true 来隐藏谱师、显示为和绿黄谱一样的 ""-""
  - UnlockScore：解除伪装所需要的最低达成率，例如 97.0
    - 可选，默认为0，即只要玩家打过一次、有过成绩，就不再伪装；而通过指定为102的方式，则可以实现永远伪装
- 伪装封面的文件名为 <曲目ID>_jacket.jpg/png。目前仅支持jpg、png这两种格式，不支持ab。")]
    public static readonly string CamouflageDir = "LocalAssets/Camouflages";

    [ConfigEntry(
        en: "Always enable track camouflage, ignoring UnlockScore / play record checks",
        zh: "无视 UnlockScore / 游玩记录检测，始终显示伪装后的曲目信息")]
    public static readonly bool AlwaysShowCamouflage = false;

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
        var allJacketFiles = Directory.GetFiles(resolvedDir, $"*_jacket.*", SearchOption.TopDirectoryOnly);

        foreach (var defFilePath in camouflageDefFiles)
        {
            if (!int.TryParse(Path.GetFileNameWithoutExtension(defFilePath), out int musicID) || musicID <= 0)
                continue;

            MelonLogger.Msg($"[TrackCamouflage] Loading file {Path.GetFileName(defFilePath)} for music ID {musicID}");

            CamouflageInfo parsedData;
            try
            {
                var parsedDoc = TomlParser.ParseFile(defFilePath);
                parsedData = new CamouflageInfo(parsedDoc);
                parsedData.Load();
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[TrackCamouflage] Failed to parse information for music {musicID}, skipping: {e}");
                continue;
            }

            if (parsedData == null)
                continue;

            // 根据当前正在处理的歌曲的ID，从allJacketFiles中筛选出匹配的jacket
            var jacketFiles = allJacketFiles.Where(
                s => int.TryParse(Path.GetFileNameWithoutExtension(s).Split('_')[0], out var res) && 
                     res == musicID && 
                     AllowedImageExts.Contains(Path.GetExtension(s).ToLowerInvariant())).ToList();
            
            if (jacketFiles.Count == 0)
                MelonLogger.Msg($"[TrackCamouflage] No jacket file for music ID {musicID}");
            foreach (var jacketFilePath in jacketFiles)
            {
                try
                {
                    parsedData.LoadJacketTexture(jacketFilePath);
                    MelonLogger.Msg($"[TrackCamouflage] Loaded jacket file {Path.GetFileName(jacketFilePath)} for music ID {musicID}");
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
            info.NoteDesigner ?? musicSelectData.ScoreData[difficulty].notesDesigner.str,
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

    #region TimelineRoot Patch
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TimelineRoot), "Initialise")]
    [EnableGameVersion(23500)]
    public static void InjectTimelineRootInitialise(MusicData musicData, CustomTextScroll ____noteDesignerName_Text)
    {
        if (!CamouflageCheck(musicData.GetID(), out CamouflageInfo info))
            return;

        if (info.NoteDesigner != null)
            ____noteDesignerName_Text.SetData(info.NoteDesigner);
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

        // Hide camouflage only when every logged-in player has some difficulty ≥ UnlockScore
        if (!AlwaysShowCamouflage)
        {
            var allEnteredUnlocked = true;

            for (int i = 0; i < 4; ++i)
            {
                var playerData = Singleton<UserDataManager>.Instance.GetUserData(i);
                if (playerData == null || !playerData.IsEntry) continue;

                var playerUnlocked = false;
                for (int j = 0; j < 6; ++j)
                { // 确认是否，当前玩家已有某个难度达成了unlock分数
                    if (playerData.ScoreDic[j].TryGetValue(musicID, out UserScore musicScore)
                        && musicScore != null
                        && musicScore.achivement >= info.UnlockScore * 10000)
                    {
                        playerUnlocked = true;
                        break;
                    }
                }
                
                if (!playerUnlocked)
                { // 如果某个人，任何难度都没达到unlock条件，则设置allEnteredUnlocked为false
                    allEnteredUnlocked = false;
                    break;
                }
            }

            if (allEnteredUnlocked) return false; // 如果所有人都已解锁，则不隐藏
        }

        return true;
    }

    public class CamouflageInfo(TomlDocument source)
    {
        private readonly TomlDocument _source = source;

        private string _name;
        private string _artist;
        private bool _hideNoteDesigner;
        private string _noteDesigner;
        private float _unlockScore;
        private Texture2D _jacket;

        public string Name => _name;
        public string Artist => _artist;
        public string NoteDesigner => _noteDesigner ?? (_hideNoteDesigner ? "-" : null);
        public Texture2D JacketTexture => _jacket;
        public float UnlockScore => _unlockScore;

        public void Load()
        {
            _name = LoadString("Name") ?? "???";
            _artist = LoadString("Artist") ?? "???";
            _hideNoteDesigner = LoadBoolean("HideNoteDesigner");
            _noteDesigner = LoadString("NoteDesigner");
            _unlockScore = LoadFloat("UnlockScore");
        }

        public void LoadJacketTexture(string path)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(path));
            _jacket = texture;
        }

        private string LoadString(string key)
        {
            if (!_source.ContainsKey(key))
                return null;

            var str = _source.GetString(key);
            return !string.IsNullOrWhiteSpace(str) ? str : null;
        }
        
        private float LoadFloat(string key)
        {
            if (!_source.ContainsKey(key))
                return 0.0f;

            try
            {
                return _source.GetFloat(key);
            }
            catch (TomlTypeMismatchException)
            {
                // TOML bare numbers like 97 / 102 are integers (TomlLong); GetFloat only accepts floats.
                return _source.GetInteger(key);
            }
        }

        private bool LoadBoolean(string key)
        {
            if (!_source.ContainsKey(key))
                return false;

            return _source.GetBoolean(key);
        }
    }
    #endregion
}
