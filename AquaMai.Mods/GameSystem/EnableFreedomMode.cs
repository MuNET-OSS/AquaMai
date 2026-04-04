using System.Collections.Generic;
using AquaMai.Config.Attributes;
using HarmonyLib;
using Monitor.ModeSelect;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "SDGA 启用自由模式",
    en: "Enable Freedom Mode on SDGA. If you have installed a separate FreedomUnlocker, please delete it.",
    zh: "在 SDGA 上启用自由模式。如果你安装了独立的 FreedomUnlocker，请删除它")]
public class EnableFreedomMode
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ModeSelectMonitor), "Initialize")]
    public static void PostModeSelectMonitorInitialize(List<int> ____modeSelectBaseType, List<bool> ____modeSelectTypeEnable)
    {
        if (____modeSelectBaseType.Contains(2)) return;
        ____modeSelectBaseType.Add(2);
        ____modeSelectTypeEnable.Add(true);
    }
}
