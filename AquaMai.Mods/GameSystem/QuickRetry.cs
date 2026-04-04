using AquaMai.Core.Attributes;
using AquaMai.Config.Attributes;
using HarmonyLib;
using MAI2.Util;
using Manager;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "快速重开",
    en: "Hold the bottom four buttons (3456) for quick retry (like in Freedom Mode, default non-utage only). Also works when Freedom Mode time runs out.",
    zh: "按住下方四个按钮（3456）快速重开本局游戏（像在 Freedom Mode 中一样，默认仅对非宴谱有效）。自由模式时间用完后也可使用")]
[EnableGameVersion(23000)]
public class QuickRetry
{
    [ConfigEntry(
        name: "更快触发",
        en: "Instant quick retry on simultaneous 4-button press.",
        zh: "四键同时按下即刻触发快速重开")]
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
    public static void ExecutePostfix(bool ____execQuickRetry, bool ____quickRetryTrigger)
    {
        if (____execQuickRetry) return;
        if (!____quickRetryTrigger) return;
        Singleton<GamePlayManager>.Instance.SetQuickRetryFrag(flag: true);
    }

    // Allow quick retry even when Freedom Mode time runs out.
    // Hook GetFreedomModeMSec to return a fake positive value when called from Execute,
    // so the original timer/countdown/sound logic proceeds normally.
    private static bool _inQuickRetryExecute;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Monitor.QuickRetry), "Execute")]
    public static void PreExecute()
    {
        _inQuickRetryExecute = true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Monitor.QuickRetry), "Execute")]
    public static void PostExecute()
    {
        _inQuickRetryExecute = false;
    }

    // Return 120000 to also bypass the long music check (IsLongMusic && startTime < 120000).
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameManager), "GetFreedomModeMSec")]
    public static void OnGetFreedomModeMSec(ref long __result)
    {
        if (_inQuickRetryExecute && __result <= 0)
        {
            __result = 120000;
        }
    }
}