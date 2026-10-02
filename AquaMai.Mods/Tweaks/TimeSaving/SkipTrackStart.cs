using AquaMai.Config.Attributes;
using AquaMai.Mods.Fancy;
using HarmonyLib;
using Manager;
using Process;

namespace AquaMai.Mods.Tweaks.TimeSaving;

[ConfigSection(
    name: "跳过乐曲开始",
    en: "Skip TrackStart screen.",
    zh: "跳过乐曲开始界面")]
public class SkipTrackStart
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof (TrackStartProcess), "OnStart")]
    public static void OnStart(ref TrackStartProcess.TrackStartSequence ____state)
    {
        ____state = TrackStartProcess.TrackStartSequence.DispEnd;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof (MusicSelectProcess), "GameStart")]
    public static bool GameStart(MusicSelectProcess __instance, ProcessDataContainer ___container)
    {
        if (CustomIntroCinematic.WillTakeOver) return true;

        ___container.processManager.AddProcess(new TrackStartProcess(___container), 50);
        ___container.processManager.ReleaseProcess(__instance);
        SoundManager.PreviewEnd();
        return false;
    }
}
