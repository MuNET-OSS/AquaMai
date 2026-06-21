using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using AquaMai.Config.Attributes;
using HarmonyLib;
using Manager;
using GameProcess = global::Process;

namespace AquaMai.Mods.Utils.EarlyContinue;

[ConfigCollapseNamespace]
[ConfigSection(
    name: "更早的续关",
    zh: "在最后一首歌结束的时候显示续关界面，确认续关后立即增加指定 Track 数")]
public class EarlyContinue
{
    [ConfigEntry(name: "增加的曲目数", zh: "设为 0 则为 1P 3首，2P 4首", en: "Number of tracks to add. Set to 0 to add 3 tracks for 1P and 3 tracks for 2P.")]
    public static readonly uint addTrackCount = 0;

    // ResultProcess.ToNextProcess() 里把 new MapResultProcess(container) 换成自己的 Process，
    // 并强制走 FadeProcess 分支
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(GameProcess.ResultProcess), "ToNextProcess")]
    public static IEnumerable<CodeInstruction> ToNextProcessTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var original = AccessTools.Constructor(typeof(GameProcess.MapResultProcess), new[] { typeof(GameProcess.ProcessDataContainer) });
        var replacement = AccessTools.Constructor(typeof(Process), new[] { typeof(GameProcess.ProcessDataContainer) });

        var codes = new List<CodeInstruction>(instructions);

        // 替换所有 new MapResultProcess(...) 为自己的 Process，并记录第一个出现位置
        var firstNewObj = -1;
        for (var i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Newobj && codes[i].operand as ConstructorInfo == original)
            {
                codes[i].operand = replacement;
                if (firstNewObj < 0) firstNewObj = i;
            }
        }

        // flag8==true 的分支体里有第一个 newobj，往前找守卫它的条件跳转，
        // 把跳转前加载 flag8 的指令改成 ldc.i4.0：无论 brtrue/brfalse 都会落到 else（FadeProcess）
        for (var i = firstNewObj - 1; i >= 1; i--)
        {
            if (codes[i].opcode == OpCodes.Brfalse || codes[i].opcode == OpCodes.Brfalse_S ||
                codes[i].opcode == OpCodes.Brtrue || codes[i].opcode == OpCodes.Brtrue_S)
            {
                // 原地改 opcode/operand，保留指令上的 labels 和 blocks
                codes[i - 1].opcode = OpCodes.Ldc_I4_0;
                codes[i - 1].operand = null;
                break;
            }
        }

        return codes;
    }

    public static uint currentAddTrackCount = 0;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.SetMaxTrack))]
    public static void SetMaxTrack()
    {
        GameManager.TempMaxTrackCount += currentAddTrackCount;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.Clear))]
    public static void GameManagerClear()
    {
        currentAddTrackCount = 0;
    }
}
