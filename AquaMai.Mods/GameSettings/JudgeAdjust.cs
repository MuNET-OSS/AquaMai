using System.Threading;
using AquaMai.Config.Attributes;
using AquaMai.Core.Attributes;
using HarmonyLib;
using IO;
using MAI2.Util;
using Manager;
using Manager.UserDatas;

namespace AquaMai.Mods.GameSettings;

[ConfigSection(
    name: "判定调整",
    en: "Globally adjust A/B judgment per player (unit same as in-game options) or increase touch delay.",
    zh: "全局分玩家调整 A/B 判（单位和游戏里一样）或增加触摸延迟")]
[EnableImplicitlyIf(nameof(shouldEnableImplicitly))]
public class JudgeAdjust
{
    public static bool shouldEnableImplicitly = false;

    [ConfigEntry(
        name: "1P A判",
        en: "Adjust A judgment for 1P.")]
    public static readonly double a_1P = 0;

    [ConfigEntry(
        name: "2P A判",
        en: "Adjust A judgment for 2P.")]
    public static readonly double a_2P = 0;

    [ConfigEntry(
        name: "1P B判",
        en: "Adjust B judgment for 1P.")]
    public static double b_1P = 0;

    [ConfigEntry(
        name: "2P B判",
        en: "Adjust B judgment for 2P.")]
    public static double b_2P = 0;

    [ConfigEntry(
        name: "触摸延迟",
        en: "Increase touch delay.",
        zh: "增加触摸延迟（不建议使用）")]
    public static readonly uint touchDelay = 0;

    private static int ResolvePlayerIndex(UserOption __instance)
    {
        try
        {
            var gpm = Singleton<GamePlayManager>.Instance;
            if (gpm == null) return -1;

            for (int i = 0; i < 2; i++)
            {
                var gameScore = gpm.GetGameScore(i);
                if (gameScore != null && ReferenceEquals(gameScore.UserOption, __instance))
                {
                    return i;
                }
            }
        }
        catch
        {
            // GamePlayManager not ready
        }

        return -1;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UserOption), "GetAdjustMSec")]
    public static void GetAdjustMSec(UserOption __instance, ref float __result)
    {
        int playerIndex = ResolvePlayerIndex(__instance);
        double adjustValue = playerIndex switch
        {
            0 => a_1P,
            1 => a_2P,
            _ => a_1P
        };

        if (adjustValue == 0) return;

        float delta = (float)(adjustValue * 16.666666d);
        __result += delta;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UserOption), "GetJudgeTimingFrame")]
    public static void GetJudgeTimingFrame(UserOption __instance, ref float __result)
    {
        int playerIndex = ResolvePlayerIndex(__instance);
        double adjustValue = playerIndex switch
        {
            0 => b_1P,
            1 => b_2P,
            _ => b_1P
        };

        if (adjustValue == 0) return;

        float delta = (float)adjustValue;
        __result += delta;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NewTouchPanel), "Recv")]
    public static void NewTouchPanelRecv()
    {
        if (touchDelay <= 0) return;
        Thread.Sleep((int)touchDelay);
    }
}
