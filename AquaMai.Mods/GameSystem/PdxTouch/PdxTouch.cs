using AquaMai.Config.Attributes;
using AquaMai.Mods.GameSystem.ExclusiveTouch;

namespace AquaMai.Mods.GameSystem.PdxTouch;

[ConfigCollapseNamespace]
[ConfigSection("PDX 独占触摸")]
public class PdxTouch
{
    [ConfigEntry("触摸体积半径", zh: "基准是 1440x1440")]
    public static readonly int radius = 20;

    [ConfigEntry("A 区额外半径",
        en: "Extra radius for A area (outer ring buttons). Can be negative to shrink.",
        zh: "A 区（外圈按键）的额外半径，可以为负值来缩小")]
    public static readonly float aAreaExtraRadius = 0;

    [ConfigEntry("B 区额外半径",
        en: "Extra radius for B area (middle ring sensors). Can be negative to shrink.",
        zh: "B 区（中圈传感器）的额外半径，可以为负值来缩小")]
    public static readonly float bAreaExtraRadius = 25;

    [ConfigEntry("C 区额外半径",
        en: "Extra radius for C area (center sensors). Can be negative to shrink.",
        zh: "C 区（中心传感器）的额外半径，可以为负值来缩小")]
    public static readonly float cAreaExtraRadius = 0;

    [ConfigEntry("D 区额外半径",
        en: "Extra radius for D area (inner ring sensors). Can be negative to shrink.",
        zh: "D 区（内圈传感器）的额外半径，可以为负值来缩小")]
    public static readonly float dAreaExtraRadius = 0;

    [ConfigEntry("E 区额外半径",
        en: "Extra radius for E area (innermost ring sensors). Can be negative to shrink.",
        zh: "E 区（最内圈传感器）的额外半径，可以为负值来缩小")]
    public static readonly float eAreaExtraRadius = 30;

    [ConfigEntry("1P 设备标识", zh: "USB 序列号或端口路径，例如 2.2。请使用配置工具中显示的标识。留空则使用第一个检测到的设备作为 1P")]
    public static readonly string path1p = "";

    [ConfigEntry("2P 设备标识")]
    public static readonly string path2p = "";

    [ConfigEntry("触摸诊断日志",
        en: "Write touch report diagnostics to UserData/AquaMaiTouch.log.",
        zh: "将触摸报告诊断信息写入 UserData/AquaMaiTouch.log")]
    public static readonly bool diagnosticLog = false;

    public static void OnBeforeEnableCheck()
    {
        ExclusiveTouchDiagnostics.Configure(diagnosticLog);
        ExclusiveTouchHost.StartDevices("PdxTouch", path1p, path2p,
            (playerNo, path) => new PdxTouchDevice(playerNo, path),
            (playerNo, path) => new FlTouchDevice(playerNo, path));
    }
}
