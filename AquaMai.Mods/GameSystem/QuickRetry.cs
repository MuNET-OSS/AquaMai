using System.Collections.Generic;
using System.Reflection.Emit;
using AquaMai.Core.Attributes;
using AquaMai.Config.Attributes;
using HarmonyLib;
using MAI2.Util;
using Manager;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "快速重开",
    en: "Hold the bottom four buttons (3456) for quick retry (like in Freedom Mode, default non-utage only).",
    zh: "按住下方四个按钮（3456）快速重开本局游戏（像在 Freedom Mode 中一样，默认仅对非宴谱有效）")]
[EnableGameVersion(23000)]
public class QuickRetry
{
    [ConfigEntry(
        name: "更快触发",
        en: "Make quick retry faster.",
        zh: "将长按时间修改为 0.5 秒")]
    private static readonly bool quickerRetry = false;

    [ConfigEntry(
        name: "宴谱启用",
        en: "Force enable in Utage.",
        zh: "在宴谱中强制启用")]
    private static readonly bool enableInUtage = false;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Monitor.QuickRetry), "IsQuickRetryEnable")]
    public static bool OnQuickRetryIsQuickRetryEnable(ref bool __result)
    {
        if (enableInUtage)
        {
            __result = true;
        }
        else
        {
            var isUtageProperty = Traverse.Create(typeof(GameManager)).Property("IsUtage");
            __result = !isUtageProperty.PropertyExists() || !isUtageProperty.GetValue<bool>();
        }
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Monitor.QuickRetry), "Execute")]
    [EnableIf(nameof(quickerRetry))]
    public static void ExecutePostfix(double ____pushTimer)
    {
        if (____pushTimer < 500) return;
        Singleton<GamePlayManager>.Instance.SetQuickRetryFrag(flag: true);
    }

    // if (GameManager.GetFreedomModeMSec() > 0)
    public static long GetFreedomModeMSecOverride()
    {
        return 999999;
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(Monitor.QuickRetry), "Execute")]
    public static IEnumerable<CodeInstruction> ExecuteTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var original = AccessTools.Method(typeof(GameManager), nameof(GameManager.GetFreedomModeMSec));
        var replacement = AccessTools.Method(typeof(QuickRetry), nameof(GetFreedomModeMSecOverride));

        foreach (var instruction in instructions)
        {
            if (instruction.Calls(original))
            {
                yield return new CodeInstruction(OpCodes.Call, replacement);
            }
            else
            {
                yield return instruction;
            }
        }
    }
}