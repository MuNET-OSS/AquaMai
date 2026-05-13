using AquaMai.Config.Attributes;
using HarmonyLib;
using MelonLoader;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(defaultOn: true)]
public class Debug
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(IO.NewTouchPanel), "Execute")]
    public static void UpdateThread(uint ____monitorIndex, IO.NewTouchPanel.StatusEnum ___Status, ref IO.NewTouchPanel.TouchpanelLevelState ___StateWrite)
    {
        if (____monitorIndex != 0) return;
        if(___StateWrite == IO.NewTouchPanel.TouchpanelLevelState.WriteIdle)
        {
            ___StateWrite = IO.NewTouchPanel.TouchpanelLevelState.WriteAllOver;
        }
    }
}