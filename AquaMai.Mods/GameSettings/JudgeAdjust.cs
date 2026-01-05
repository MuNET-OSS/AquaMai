using System.Threading;
using AquaMai.Config.Attributes;
using HarmonyLib;
using IO;
using Manager.UserDatas;

namespace AquaMai.Mods.GameSettings;

[ConfigSection(
    name: "判定调整",
    en: "Globally adjust A/B judgment (unit same as in-game options) or increase touch delay.",
    zh: "全局调整 A/B 判（单位和游戏里一样）或增加触摸延迟")]
public class JudgeAdjust
{
    [ConfigEntry(
        name: "A判",
        en: "Adjust A judgment.")]
    public static readonly double a = 0;

    [ConfigEntry(
        name: "B判",
        en: "Adjust B judgment.")]
    public static readonly double b = 0;

    [ConfigEntry(
        name: "触摸延迟",
        en: "Increase touch delay.",
        zh: "增加触摸延迟（不建议使用）")]
    public static readonly uint touchDelay = 0;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UserOption), "GetAdjustMSec")]
    public static void GetAdjustMSec(ref float __result)
    {
        __result += (float)(a * 16.666666d);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UserOption), "GetJudgeTimingFrame")]
    public static void GetJudgeTimingFrame(ref float __result)
    {
        __result += (float)b;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NewTouchPanel), "Recv")]
    public static void NewTouchPanelRecv()
    {
        if (touchDelay <= 0) return;
        Thread.Sleep((int)touchDelay);
    }
}
