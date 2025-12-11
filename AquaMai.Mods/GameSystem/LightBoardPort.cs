using AquaMai.Config.Attributes;
using HarmonyLib;
using IO;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "灯光串口",
    en: """
        Adjust the port of the Light Boards' serial port, default value is COM21 COM23.
        Requires configuration by Device Manager. If you are unsure, don't use it.
        """,
    zh: """
        调整灯板串口号，默认值 COM21 COM23
        需要设备管理器配置，如果你不清楚你是否可以使用，请不要使用
        """)]
public class LightBoardPort
{
    [ConfigEntry(
        en: "Port for 1P LED.",
        name: "1P灯光串口号")]
    private static readonly string portName_1PLED = "COM21";

    [ConfigEntry(
        en: "Port for 2P LED.",
        name: "2P灯光串口号")]
    private static readonly string portName_2PLED = "COM23";
    [HarmonyPatch]
    public static class MechaManagerPatch
    {
        [HarmonyPatch(typeof(MechaManager), "Initialize")]
        [HarmonyPrefix]
        static bool PrefixInitialize(MechaManager.InitParam initParam)
        {

            if (initParam != null &&
                initParam.LedParam != null &&
                initParam.LedParam.Length > 0)
            {
                initParam.LedParam[0].ComName = portName_1PLED;
                initParam.LedParam[1].ComName = portName_2PLED;
            }

            return true;
        }
    }
}
