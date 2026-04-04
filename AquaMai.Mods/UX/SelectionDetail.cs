using System.Collections.Generic;
using System.Linq;
using AquaMai.Core.Attributes;
using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using AquaMai.Core.Resources;
using DB;
using HarmonyLib;
using JetBrains.Annotations;
using MAI2.Util;
using Manager;
using Manager.MaiStudio;
using Manager.UserDatas;
using Monitor;
using Process;
using UnityEngine;

namespace AquaMai.Mods.UX;

[EnableGameVersion(23500)]
[ConfigSection(
    name: "歌曲详情",
    en: "Show detail of selected song in music selection screen.",
    zh: "选歌界面显示选择的歌曲的详情")]
public class SelectionDetail
{
    [ConfigEntry(
        en: "Show friend battle target achievement",
        zh: "显示友人对战目标分数")]
    private static readonly bool showBattleAchievement = false;

    private static readonly Window[] window = new Window[2];
    private static MusicSelectProcess.MusicSelectData SelectData { get; set; }
    private static readonly int[] difficulty = new int[2];
    [CanBeNull] private static UserGhost userGhost;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MusicSelectMonitor), "UpdateRivalScore")]
    public static void ScrollUpdate(MusicSelectProcess ____musicSelect, MusicSelectMonitor __instance)
    {
        int player;
        if (__instance == ____musicSelect.MonitorArray[0])
        {
            player = 0;
        }
        else if (__instance == ____musicSelect.MonitorArray[1])
        {
            player = 1;
        }
        else
        {
            return;
        }

        if (window[player] != null)
        {
            window[player].Close();
        }

        var userData = Singleton<UserDataManager>.Instance.GetUserData(player);
        if (!userData.IsEntry) return;

        if (____musicSelect.IsRandomIndex() && !____musicSelect.IsRandomSelected()) return;

        SelectData = ____musicSelect.GetMusic(0);
        if (SelectData == null) return;
        difficulty[player] = ____musicSelect.GetDifficulty(player);
        var ghostTarget = ____musicSelect.GetCombineMusic(0).musicSelectData[(int)____musicSelect.ScoreType].GhostTarget;
        userGhost = Singleton<GhostManager>.Instance.GetGhostToEnum(ghostTarget);

        window[player] = player == 0 ? __instance.gameObject.AddComponent<P1Window>() : __instance.gameObject.AddComponent<P2Window>();
    }

    // 在随机选歌后， 不会调用 UpdateRivalScore，但是会调用 SetRivalScore
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MusicSelectMonitor), "SetRivalScore")]
    public static void AfterSetRivalScore(MusicSelectMonitor __instance, MusicSelectProcess ____musicSelect)
    {
        // 仅在随机选歌时生效
        if (!____musicSelect.IsRandomSelected()) return;
        // 手动触发窗口更新
        ScrollUpdate(____musicSelect, __instance);
    }

    private class P1Window : Window
    {
        protected override int player => 0;
    }

    private class P2Window : Window
    {
        protected override int player => 1;
    }

    private abstract class Window : MonoBehaviour
    {
        protected abstract int player { get; }
        private UserData userData => Singleton<UserDataManager>.Instance.GetUserData(player);

        public void OnGUI()
        {
            var dataToShow = new List<string>();
            dataToShow.Add($"ID: {SelectData.MusicData.name.id}");
            dataToShow.Add(MusicDirHelper.LookupPath(SelectData.MusicData.name.id).Split('/').Reverse().ToArray()[3]);
            if (SelectData.MusicData.genreName is not null) // SelectData.MusicData.genreName.str may not correct
                dataToShow.Add(Singleton<DataManager>.Instance.GetMusicGenre(SelectData.MusicData.genreName.id)?.genreName);
            if (SelectData.MusicData.AddVersion is not null)
                dataToShow.Add(Singleton<DataManager>.Instance.GetMusicVersion(SelectData.MusicData.AddVersion.id)?.genreName);
            
            var difficulty = SelectionDetail.difficulty[player];
            var notesData = SelectData.MusicData.notesData[difficulty];
            // Fix for player choosing Re:master but the music doesn't have Re:master
            if (!notesData.isEnable && difficulty == 4)
            {
                difficulty = 3;
                notesData = SelectData.MusicData.notesData[difficulty];
            }
            
            dataToShow.Add($"{notesData?.level}.{notesData?.levelDecimal}");

            if (userGhost != null)
            {
                dataToShow.Add(string.Format(Locale.UserGhostAchievement, $"{userGhost.Achievement / 10000m:0.0000}"));
            }

            var (sssUp, ssspUp, apUp) = CalcScoreIncrements(SelectData.MusicData, difficulty);
            if (sssUp > 0) dataToShow.Add(string.Format(Locale.RatingUpWhenSSS, sssUp));
            else if (ssspUp > 0) dataToShow.Add(string.Format(Locale.RatingUpWhenSSSp, ssspUp));
            else if (apUp > 0) dataToShow.Add(string.Format(Locale.RatingUpWhenAP, apUp));

            var playCount = Shim.GetUserScoreList(userData)[difficulty].FirstOrDefault(it => it.id == SelectData.MusicData.name.id)?.playcount ?? 0;
            if (playCount > 0)
            {
                dataToShow.Add(string.Format(Locale.PlayCount, playCount));
            }


            var width = GuiSizes.FontSize * 15f;
            var x = GuiSizes.PlayerCenter - width / 2f + GuiSizes.PlayerWidth * player;
            var y = Screen.height * 0.87f;

            var labelStyle = GUI.skin.GetStyle("label");
            labelStyle.fontSize = GuiSizes.FontSize;
            labelStyle.alignment = TextAnchor.MiddleCenter;

            GUI.Box(new Rect(x, y, width, dataToShow.Count * GuiSizes.LabelHeight + 2 * GuiSizes.Margin), "");
            for (var i = 0; i < dataToShow.Count; i++)
            {
                GUI.Label(new Rect(x, y + GuiSizes.Margin + i * GuiSizes.LabelHeight, width, GuiSizes.LabelHeight), dataToShow[i]);
            }
        }

        /* 返回值：(推到SSS上的分, 推到SSS+上的分, 推到AP上的分) */
        private (int sss, int sssp, int ap) CalcScoreIncrements(MusicData musicData, int difficulty)
        {
            if (musicData.GetID() >= 100000) return (0, 0, 0); // 宴谱不计分
            
            var musicId = musicData.name.id;
            var aimRates = ( // 假定推到了某个目标时，对应的UserRate
                Shim.CreateUserRate(musicId, difficulty, 1000000, (uint)musicData.version, PlayComboflagID.None),
                Shim.CreateUserRate(musicId, difficulty, 1005000, (uint)musicData.version, PlayComboflagID.None),
                Shim.CreateUserRate(musicId, difficulty, 1010000, (uint)musicData.version, PlayComboflagID.AllPerfectPlus)
            );
            
            var list = aimRates.Item1.OldFlag ? userData.RatingList.RatingList : userData.RatingList.NewRatingList;
            var maxCount = aimRates.Item1.OldFlag ? 35 : 15;
            int userLowRate = 0;
            if (list.Count == maxCount)
            {
                var rate = list.Last();
                userLowRate = (int)rate.SingleRate;
            }

            var userSongRate = list.FirstOrDefault(it => it.MusicId == musicId && it.Level == difficulty);
            if (!userSongRate.Equals(default(UserRate))) userLowRate = (int)userSongRate.SingleRate;

            return ((int)aimRates.Item1.SingleRate - userLowRate, (int)aimRates.Item2.SingleRate - userLowRate, (int)aimRates.Item3.SingleRate - userLowRate);
        }

        public void Close()
        {
            Destroy(this);
        }
    }
}