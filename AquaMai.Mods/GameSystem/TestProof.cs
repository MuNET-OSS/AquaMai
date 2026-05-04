using System.Linq;
using AquaMai.Config.Attributes;
using AquaMai.Config.Types;
using AquaMai.Core;
using AquaMai.Core.Attributes;
using AquaMai.Core.Helpers;
using AquaMai.Mods.Tweaks;
using AquaMai.Mods.UX;
using AquaMai.Mods.UX.PracticeMode;
using HarmonyLib;
using Main;
using Manager;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "测试键长按",
    en: """
        When enabled, test button must be long pressed to enter game test mode.
        When test button is bound to other features, this option is enabled automatically.
        """,
    zh: """
        启用后，测试键必须长按才能进入游戏测试模式
        当测试键被绑定到其它功能时，此选项自动开启
        """)]
[EnableImplicitlyIf(nameof(ShouldEnableImplicitly))]
public class TestProof
{
    public static bool ShouldEnableImplicitly
    {
        get
        {
            (System.Type section, KeyCodeOrName key)[] featureKeys =
            [
                (typeof(OneKeyEntryEnd), OneKeyEntryEnd.key),
                (typeof(OneKeyRetrySkip), OneKeyRetrySkip.retryKey),
                (typeof(OneKeyRetrySkip), OneKeyRetrySkip.skipKey),
                (typeof(HideSelfMadeCharts), HideSelfMadeCharts.key),
                (typeof(PracticeMode), PracticeMode.key),
                (typeof(ResetTouch), ResetTouch.key),
            ];
            var keyMapEnabled = ConfigLoader.Config.GetSectionState(typeof(KeyMap)).Enabled;
            return featureKeys.Any(it =>
                // The feature is enabled and...
                ConfigLoader.Config.GetSectionState(it.section).Enabled &&
                (
                    // and the key is test, or...
                    it.key == KeyCodeOrName.Test ||
                    // or the key have been mapped to the same key as test.
                    (keyMapEnabled && it.key.ToString() == KeyMap.Test.ToString())));
        }
    }

    [ConfigEntry(
        name: "测试模式按键",
        en: "Change it to a value other than Test to enable long pressing of a specific key to enter the game test mode, so that the Test key can be fully used to implement custom functions",
        zh: "修改为 Test 以外的值来实现长按特定的键进入游戏测试模式，这样 Test 键就可以完全用来实现自定义功能了")]
    private static readonly KeyCodeOrName testKey = KeyCodeOrName.Test;

    private static bool _inGameMainObjectUpdate;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameMainObject), "Update")]
    public static void PreGameMainObjectUpdate()
    {
        _inGameMainObjectUpdate = true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameMainObject), "Update")]
    public static void PostGameMainObjectUpdate()
    {
        _inGameMainObjectUpdate = false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InputManager), "GetSystemInputDown")]
    public static bool GetSystemInputDown(ref bool __result, InputManager.SystemButtonSetting button, bool[] ___SystemButtonDown)
    {
        __result = ___SystemButtonDown[(int)button];
        if (button != InputManager.SystemButtonSetting.ButtonTest)
            return false;

        if (_inGameMainObjectUpdate)
        {
            __result = KeyListener.GetKeyDownOrLongPress(testKey, true);
        }

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameMain), "Update")]
    public static void Workaround() { }
    /*
     * 似乎是 0Harmony.dll 的 Bug，导致在 Maimoller 的 Mod Patch GameMain:Update 之后，
     * 原本 GameMain:Update 调用的 InputManager:GetSystemInputDown 变回了未 Patch 过的原始版本
     * 我觉得这是玄学 Bug，应该用玄学方法来修
     * 尝试性放了一个这个在这里，诶，好了！
     * 我觉得应该是有某种 Patch 顺序相关的问题
     */
}
