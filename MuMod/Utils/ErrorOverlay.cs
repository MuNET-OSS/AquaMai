using Main;
using MelonLoader;
using UnityEngine;

namespace MuMod.Utils;

/// <summary>
/// 出错时在屏幕左上角显示错误信息，并阻止游戏继续加载
/// </summary>
public static class ErrorOverlay
{
    public static string ErrorMessage { get; private set; }
    public static bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    private static GUIStyle _errorStyle;

    public static void SetError(string message)
    {
        ErrorMessage = message;
        MelonLogger.Error($"[MuMod] {message}");
    }

    // 用 Harmony 把 GameMainObject.Awake 里的实例销毁，阻止游戏继续运行
    public static void BlockGame(HarmonyLib.Harmony harmony)
    {
        if (!HasError) return;
        harmony.PatchAll(typeof(GameBlocker));
    }

    public static void Render()
    {
        if (!HasError) return;

        if (_errorStyle == null)
        {
            _errorStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 25,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
            };
        }

        GUI.Label(new Rect(50, 50, Screen.width / 2f, Screen.height), ErrorMessage, _errorStyle);
    }

    [HarmonyLib.HarmonyPatch(typeof(GameMainObject), "Awake")]
    private static class GameBlocker
    {
        private static void Postfix(GameMainObject __instance)
        {
            Object.Destroy(__instance);
        }
    }
}
