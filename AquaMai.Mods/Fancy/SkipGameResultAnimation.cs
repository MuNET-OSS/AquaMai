using AquaMai.Config.Attributes;
using HarmonyLib;
using Manager;
using Monitor.Game;

namespace AquaMai.Mods.Fancy;

[ConfigSection(
    name: "跳过结算特效",
    en: "Skip AP/FC/FS celebration effects on result screen during AutoPlay. Useful for chart recording.",
    zh: "在 AutoPlay 模式下跳过结算画面的 AP/FC/FS 等庆祝特效，方便录制谱面确认")]
public class SkipGameResultAnimation
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameResultEffectCtrl), "Play")]
    public static bool PrePlay(ref bool __result)
    {
        if (!GameManager.IsAutoPlay())
        {
            return true;
        }

        __result = false;
        return false;
    }
}
