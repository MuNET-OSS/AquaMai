using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Emit;
using AquaMai.Config.Attributes;
using AquaMai.Core.Attributes;
using HarmonyLib;
using IO;
using Manager;
using PartyLink;
using Process;

namespace AquaMai.Mods.Tweaks;
[ConfigSection(
    "拦截AMDaemon错误",
    zh: """
        阻止游戏在不使用Dummy时向AMDaemon发送错误状态，避免AMDaemon锁点数以及忽略Coin Signal等问题
        """,
    en: """
        Prevents the game from sending error states to AMDaemon when Dummy is not in use, thereby avoiding issues such as AMDaemon locking points and ignoring Coin Signals.
        """)]

public class BlockAmDaemonError
{
    [ConfigEntry(
        "拦截触摸错误",
        "Do not send touch panel errors to AMDaemon.",
        "不向AMDaemon发送触摸错误"
    )]
    public static readonly bool TouchPanel = false;

    [ConfigEntry(
        "拦截相机管理器错误",
        "Do not send camera manager errors to AMDaemon.",
        "不向AMDaemon发送相机管理器错误"
    )]
    public static readonly bool CameraManager = false;

    [ConfigEntry(
        "拦截QrCamera错误",
        "No error reported from the QR camera to AMDaemon.",
        "不向AMDaemon发送二维码相机的错误"
    )]
    public static readonly bool QrCamera = false;

    [ConfigEntry(
        "拦截PhotoCamera错误",
        "Do not send photo camera errors to AMDaemon.",
        "不向AMDaemon发送照片相机错误"
    )]
    public static readonly bool PhotoCamera = false;

    [ConfigEntry(
        "拦截基准机错误",
        "Do not send error 3201 related to the benchmark machine to AMDaemon.",
        "不向AMDaemon发送基准机相关的错误(3201)"
    )]
    public static readonly bool Error3201 = false;

    [EnableIf(nameof(TouchPanel))]
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(StartupProcess), "OnUpdate")]
    public static IEnumerable<CodeInstruction> TouchPanelTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var codeMatcher = new CodeMatcher(instructions);

        codeMatcher.MatchStartForward(
                new CodeMatch(i => i.Calls(AccessTools.Method(
                    typeof(NewTouchPanel),
                    nameof(NewTouchPanel.GetLastErrorPs)))),
                new CodeMatch(i => i.Calls(AccessTools.Method(
                    typeof(AMDaemon.Error), "Set", new[] { typeof(int) })))
            )
            .ThrowIfInvalid("Could not find call to TouchPanel AMDeamon.Error.Set")
            .SetOpcodeAndAdvance(OpCodes.Nop)
            .SetOpcodeAndAdvance(OpCodes.Nop);

        return codeMatcher.Instructions();
    }

    [EnableIf(nameof(CameraManager))]
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(StartupProcess), "OnUpdate")]
    public static IEnumerable<CodeInstruction> CameraManagerTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var errorSet = AccessTools.Method(
            typeof(AMDaemon.Error),
            "Set",
            new[] { typeof(int) });

        var startMethod = AccessTools.Method(
            typeof(Stopwatch),
            nameof(Stopwatch.Start));

        var matcher = new CodeMatcher(instructions);

        matcher.MatchStartForward(
            new CodeMatch(i => i.Calls(startMethod))
        );
        if (!matcher.IsValid)
            return instructions;
        matcher.Advance(1);
        matcher.MatchStartForward(
            new CodeMatch(i => i.Calls(errorSet))
        );
        if (!matcher.IsValid)
            return instructions;
        matcher.SetOpcodeAndAdvance(OpCodes.Nop);

        return matcher.Instructions();
    }

    [EnableIf(nameof(Error3201))]
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(StartupProcess), "OnUpdate")]
    [HarmonyPatch(typeof(AdvertiseProcess), "UpdateSetting")]
    public static IEnumerable<CodeInstruction> Error3201Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var errorSet = AccessTools.Method(
            typeof(AMDaemon.Error),
            "Set",
            new[] { typeof(int) });

        var startMethod = AccessTools.Method(
            typeof(Setting.IManager),
            nameof(Setting.IManager.terminate));

        var matcher = new CodeMatcher(instructions);

        matcher.MatchStartForward(
            new CodeMatch(i => i.Calls(startMethod))
        );
        if (!matcher.IsValid)
            return instructions;
        matcher.Advance(1);
        matcher.MatchStartForward(
            new CodeMatch(i => i.Calls(errorSet))
        );
        if (!matcher.IsValid)
            return instructions;
        matcher.SetOpcodeAndAdvance(OpCodes.Nop);

        return matcher.Instructions();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AMDaemon.Error), "Set", new[] { typeof(int) })]
    public static bool CameraPrefix(int __0)
    {
        if (__0 == 3101 || QrCamera)
            return false;
        if (__0 == 3102 || PhotoCamera)
            return false;
        return true;
    }

}