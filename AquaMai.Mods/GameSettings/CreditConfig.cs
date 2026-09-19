using System.Diagnostics;
using System.Linq;
using AquaMai.Config.Attributes;
using AquaMai.Core.Attributes;
using HarmonyLib;
using MelonLoader;

namespace AquaMai.Mods.GameSettings;

[ConfigSection(
    name: "点数设置",
    en: "Set the game to Paid Play (lock credits) or Free Play.",
    zh: "设置游戏为付费游玩（锁定可用点数）或免费游玩")]
public class CreditConfig
{
    [ConfigEntry(
        name: "免费游玩",
        en: "Set to Free Play (set to false for Paid Play).",
        zh: "是否免费游玩（设为 false 时为付费游玩）")]
    private static readonly bool isFreePlay = true;

    [ConfigEntry(
        name: "免费时购票",
        en: "Allow purchasing paid feature tickets in Free Play mode.",
        zh: "可以在免费游玩时购买付费功能票")]
    private static readonly bool allowTicketInFreePlay = false;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Manager.Credit), "IsFreePlay")]
    public static bool PreIsFreePlay(ref bool __result)
    {
        if (allowTicketInFreePlay)
        {
            var stackTrace = new StackTrace();
            var stackFrames = stackTrace.GetFrames();
            if (stackFrames.Any(f => f.GetMethod() is { DeclaringType.Name: "TicketSelectMonitor", Name: "Initialize" }
                                                   or { DeclaringType.Name: "ModeSelectProcess", Name: "OnStart" }
                                                   or { DeclaringType.Name: "TicketSelectMonitor", Name: "UpdateOkButtonByTicketStatus" }
            ))
            {
                __result = false;
                return false;
            }
        }
        __result = isFreePlay;
        return false;
    }

    [ConfigEntry(
        name: "锁定点数",
        en: "Lock credits amount (only valid in Paid Play). Set to 0 to disable.",
        zh: "锁定可用点数数量（仅在付费游玩时有效），设为 0 以禁用")]
    private static readonly uint lockCredits = 24u;

    private static bool ShouldLockCredits => !isFreePlay && lockCredits > 0;

    [EnableIf(nameof(ShouldLockCredits))]
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Manager.Credit), "IsGameCostEnough")]
    public static bool PreIsGameCostEnough(ref bool __result)
    {
        __result = true;
        return false;
    }

    [EnableIf(nameof(ShouldLockCredits))]
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AMDaemon.CreditUnit), "Credit", MethodType.Getter)]
    public static bool PreCredit(ref uint __result)
    {
        __result = lockCredits;
        return false;
    }

    [EnableIf(nameof(allowTicketInFreePlay))]
    [EnableGameVersion(27000, noWarn: true)]
    [HarmonyPrefix]
    [HarmonyPatch("Manager.MagicalPassManager", "GetNeedCredit")]
    public static bool GetNeedCredit(ref int needCredit, ref int needCreditNumerator, ref int needCreditDenominator)
    {
        needCredit = 0;
        needCreditNumerator = 0;
        needCreditDenominator = 0;
        return false;
    }
}