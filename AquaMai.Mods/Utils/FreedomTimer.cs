using System.Reflection;
using AquaMai.Config.Attributes;
using AquaMai.Config.Types;
using AquaMai.Core.Attributes;
using AquaMai.Core.Helpers;
using HarmonyLib;
using Main;
using Manager;
using MelonLoader;

namespace AquaMai.Mods.Utils;

[ConfigSection("自由模式时间修改", "Freedom Timer Modification")]
public static class FreedomTimer
{
    [ConfigEntry("秒数")]
    public static long seconds = 600;

    [ConfigEntry("无限时间")]
    public static bool infinityTime = false;

    [ConfigEntry(
        name: "追加时间按键",
        en: "Key to add time in Freedom Mode. Set to None to disable.",
        zh: "可通过指定按键，随时为自由模式追加时间。设为 None 则禁用")]
    public static readonly KeyCodeOrName addTimeKey = KeyCodeOrName.None;

    [ConfigEntry(name: "追加时间长按", en: "Require long press to add time", zh: "上述“追加时间按键”是否需要长按")]
    public static readonly bool addTimeLongPress = false;

    [ConfigEntry(
        name: "每次追加秒数",
        en: "Seconds added each time the key is pressed",
        zh: "每次按上述“追加时间按键”，所追加的秒数")]
    public static readonly long addTimeSeconds = 120;
    
    private static bool IsAddTimeEnabled => addTimeKey != KeyCodeOrName.None;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.GetFreedomStartTime))]
    public static bool GetFreedomStartTime(ref long __result)
    {
        __result = seconds * 1000;
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.IsFreedomTimerPause), MethodType.Getter)]
    [EnableIf(nameof(infinityTime))]
    public static void IsFreedomTimerPause(ref bool __result)
    {
        __result = true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameMainObject), "Update")]
    [EnableIf(nameof(IsAddTimeEnabled))]
    public static void OnGameMainObjectUpdate()
    {
        if (!GameManager.IsFreedomMode) return;
        if (!KeyListener.GetKeyDownOrLongPress(addTimeKey, addTimeLongPress)) return;
        if (addTimeSeconds <= 0) return;

        // 设置GameManager._freedomTime（私有属性）
        var FreedomTimeField = AccessTools.Field(typeof(GameManager), "_freedomTime");
        var current = (long)FreedomTimeField.GetValue(null);
        if (current < 0) current = 0;
        FreedomTimeField.SetValue(null, current + addTimeSeconds * 1000L);

        // 时间归零会停止倒计时；重新启动以便追加的时间能继续倒数。
        if (!GameManager.IsFreedomCountDown) AccessTools.Property(typeof(GameManager), "IsFreedomCountDown").SetValue(null, true); // private set，所以需要反射
        GameManager.IsFreedomTimeUp = false;
        
        MelonLogger.Msg($"[FreedomTimer] 已将自由模式的时间增加{addTimeSeconds}秒");
    }
}
