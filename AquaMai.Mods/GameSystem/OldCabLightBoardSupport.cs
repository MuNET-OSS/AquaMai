using AquaMai.Config.Attributes;
using Comio.BD15070_4;
using HarmonyLib;
using Mecha;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "旧框灯板支持",
    en: "Skip BoardNo check to use the old cab light board 837-15070-02, and remapping the Billboard LED to woofer LED, roof LED and center LED",
    zh: "跳过 BoardNo 检查以使用 837-15070-02（旧框灯板），并映射新框顶板 LED 至重低音扬声器 LED、顶板 LED 以及中央 LED")]
public class OldCabLightBoardSupport
{
    private static readonly FieldInfo _controlField;
    private static readonly FieldInfo _boardField;
    private static readonly FieldInfo _ctrlField;
    private static readonly FieldInfo _ioCtrlField;
    private static readonly FieldInfo _setLedGs8BitCommandField;
    private static readonly FieldInfo _gsUpdateField;
    private static readonly MethodInfo _sendForceCommandMethod;

    [HarmonyPatch(typeof(BoardCtrl15070_4), "_md_initBoard_GetBoardInfo")]
    public class BoardCtrl15070_4__md_initBoard_GetBoardInfo_Patch
    {
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler1(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool patched = false;
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Callvirt &&
                    codes[i].operand != null &&
                    codes[i].operand.ToString().Contains("IsEqual"))
                {
                    codes[i] = new CodeInstruction(OpCodes.Pop);
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Pop));
                    codes.Insert(i + 2, new CodeInstruction(OpCodes.Ldc_I4_1));

                    patched = true;
                    MelonLoader.MelonLogger.Msg("[OldCabLightBoardSupport] 修补 BoardCtrl15070_4._md_initBoard_GetBoardInfo 方法成功");
                    break;
                }
            }
            if (!patched)
            {
                MelonLoader.MelonLogger.Warning("[OldCabLightBoardSupport] 未找到需要修补的代码位置：BoardCtrl15070_4._md_initBoard_GetBoardInfo");
            }
            return codes;
        }
    }

    [HarmonyPatch(typeof(Bd15070_4Control), "IsNeedFirmUpdate")]
    public class Bd15070_4Control_IsNeedFirmUpdate_Patch
    {
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler2(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool patched = false;
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Callvirt &&
                    codes[i].operand != null &&
                    codes[i].operand.ToString().Contains("IsEqual"))
                {
                    codes[i] = new CodeInstruction(OpCodes.Pop);
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Pop));
                    codes.Insert(i + 2, new CodeInstruction(OpCodes.Ldc_I4_1));

                    patched = true;
                    MelonLoader.MelonLogger.Msg("[OldCabLightBoardSupport] 修补 Bd15070_4Control.IsNeedFirmUpdate 方法成功");
                    break;
                }
            }
            if (!patched)
            {
                MelonLoader.MelonLogger.Warning("[OldCabLightBoardSupport] 未找到需要修补的代码位置：Bd15070_4Control.IsNeedFirmUpdate");
            }
            return codes;
        }
    }

    static OldCabLightBoardSupport()
    {
        var ledIfType = typeof(Bd15070_4IF);
        _controlField = ledIfType.GetField("_control", BindingFlags.NonPublic | BindingFlags.Instance);
        _gsUpdateField = ledIfType.GetField("_gsUpdate", BindingFlags.NonPublic | BindingFlags.Instance);

        var controlType = typeof(Mecha.Bd15070_4Control);
        _boardField = controlType.GetField("_board", BindingFlags.NonPublic | BindingFlags.Instance);

        var boardType = typeof(Comio.BD15070_4.Board15070_4);
        _ctrlField = boardType.GetField("_ctrl", BindingFlags.NonPublic | BindingFlags.Instance);

        var boardCtrlType = typeof(Comio.BD15070_4.BoardCtrl15070_4);
        _ioCtrlField = boardCtrlType.GetField("_ioCtrl", BindingFlags.NonPublic | BindingFlags.Instance);
        _sendForceCommandMethod = boardCtrlType.GetMethod("SendForceCommand", BindingFlags.Public | BindingFlags.Instance);

        var ioCtrlType = typeof(IoCtrl);
        _setLedGs8BitCommandField = ioCtrlType.GetField("SetLedGs8BitCommand", BindingFlags.Public | BindingFlags.Instance);

        if (_controlField == null)
        {
            MelonLoader.MelonLogger.Error("[OldCabLightBoardSupport] Failed to cache _control field");
        }
        if (_setLedGs8BitCommandField == null)
        {
            MelonLoader.MelonLogger.Error("[OldCabLightBoardSupport] Failed to cache SetLedGs8BitCommand field");
        }
        if (_sendForceCommandMethod == null)
        {
            MelonLoader.MelonLogger.Error("[OldCabLightBoardSupport] Failed to cache SendForceCommand method");
        }
    }

    [HarmonyPatch(typeof(Bd15070_4IF), "_construct")]
    public class Bd15070_4IF_Construct_Patch
    {
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            int patchedNum = 0;
            for (int i = 0; i < codes.Count && patchedNum < 2; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_I4_8 &&
                    codes[i].operand == null)
                {
                    codes[i].opcode = OpCodes.Ldc_I4_S;
                    codes[i].operand = (sbyte)10;
                    patchedNum++;
                }
            }
            if (patchedNum == 2)
            {
                MelonLoader.MelonLogger.Msg("[OldCabLightBoardSupport] Extended Bd15070_4IF._switchParam size and its initialize for loop from 8 to 10!");
            }
            else
            {
                MelonLoader.MelonLogger.Warning($"[OldCabLightBoardSupport] Bd15070_4IF._switchParam patching failed (patched {patchedNum}/2)");
            }
            return codes;
        }
    }

    [HarmonyPatch]
    public class JvsOutputPwmPatch
    {
        [HarmonyTargetMethod]
        static MethodBase TargetMethod()
        {
            var type = typeof(IO.Jvs).GetNestedType("JvsOutputPwm", BindingFlags.NonPublic | BindingFlags.Instance);
            if (type == null)
            {
                MelonLoader.MelonLogger.Error("[OldCabLightBoardSupport] JvsOutputPwm type not found");
                return null;
            }
            return type.GetMethod("Set", new[] { typeof(byte), typeof(Color32), typeof(bool) });
        }

        [HarmonyPrefix]
        public static bool Prefix(object __instance, byte index, Color32 color, bool update)
        {
            RedirectToButtonLedMechanism(index, color);

            return false;
        }
    }

    private static void RedirectToButtonLedMechanism(byte playerIndex, Color32 color)
    {
        // Check if MechaManager is initialized
        if (!IO.MechaManager.IsInitialized)
        {
            MelonLoader.MelonLogger.Warning("[OldCabLightBoardSupport] MechaManager not initialized, cannot set woofer LED");
            return;
        }

        // Get the LED interface for the player
        var ledIf = IO.MechaManager.LedIf;
        if (ledIf == null || playerIndex >= ledIf.Length || ledIf[playerIndex] == null)
        {
            MelonLoader.MelonLogger.Warning($"[OldCabLightBoardSupport] LED interface not available for player {playerIndex}");
            return;
        }

        // Use cached reflection FieldInfo and MethodInfo to access the IoCtrl and call SetLedGs8BitCommand[8] & SetLedGs8BitCommand[9] directly
        // Then set _gsUpdate flag so PreExecute() sends the update command (just like buttons do)
        try
        {
            if (_controlField == null)
            {
                MelonLoader.MelonLogger.Error("[OldCabLightBoardSupport] _control field not found in Bd15070_4IF");
                return;
            }

            var control = _controlField.GetValue(ledIf[playerIndex]);
            if (control == null)
            {
                MelonLoader.MelonLogger.Error("[OldCabLightBoardSupport] Control object is null");
                return;
            }

            if (_boardField == null)
            {
                MelonLoader.MelonLogger.Error("[OldCabLightBoardSupport] _board field not found in Bd15070_4Control");
                return;
            }

            var board = _boardField.GetValue(control);
            if (board == null)
            {
                MelonLoader.MelonLogger.Error("[OldCabLightBoardSupport] Board object is null");
                return;
            }

            if (_ctrlField == null)
            {
                MelonLoader.MelonLogger.Error("[OldCabLightBoardSupport] _ctrl field not found in Board15070_4");
                return;
            }

            var boardCtrl = _ctrlField.GetValue(board);
            if (boardCtrl == null)
            {
                MelonLoader.MelonLogger.Error("[OldCabLightBoardSupport] BoardCtrl object is null");
                return;
            }

            if (_ioCtrlField == null)
            {
                MelonLoader.MelonLogger.Error("[OldCabLightBoardSupport] _ioCtrl field not found in BoardCtrl15070_4");
                return;
            }

            var ioCtrl = _ioCtrlField.GetValue(boardCtrl);
            if (ioCtrl == null)
            {
                MelonLoader.MelonLogger.Error("[OldCabLightBoardSupport] IoCtrl object is null");
                return;
            }

            if (_setLedGs8BitCommandField == null)
            {
                MelonLoader.MelonLogger.Error("[OldCabLightBoardSupport] SetLedGs8BitCommand field not found in IoCtrl");
                return;
            }

            var setLedGs8BitCommandArray = _setLedGs8BitCommandField.GetValue(ioCtrl) as SetLedGs8BitCommand[];
            if (setLedGs8BitCommandArray == null || setLedGs8BitCommandArray.Length <= 9)
            {
                MelonLoader.MelonLogger.Error("[OldCabLightBoardSupport] SetLedGs8BitCommand array is null or too small");
                return;
            }

            if (_sendForceCommandMethod == null)
            {
                MelonLoader.MelonLogger.Error("[OldCabLightBoardSupport] SendForceCommand method not found in BoardCtrl15070_4");
                return;
            }

            // Use SetLedGs8BitCommand[8] and SetLedGs8BitCommand[9] directly (same as buttons 0-7, but for ledPos = 8 and 9)
            // This bypasses the FET command path in IoCtrl.SetLedData(), as they are not via FET, they are like buttons
            // ledPos = 8 == woofer & roof
            // ledPos = 9 == center
            setLedGs8BitCommandArray[8].setColor(8, color);
            setLedGs8BitCommandArray[9].setColor(9, color);
            _sendForceCommandMethod.Invoke(boardCtrl, new object[] { setLedGs8BitCommandArray[8] });
            _sendForceCommandMethod.Invoke(boardCtrl, new object[] { setLedGs8BitCommandArray[9] });

            if (_gsUpdateField != null)
            {
                _gsUpdateField.SetValue(ledIf[playerIndex], true);
            }
            else
            {
                MelonLoader.MelonLogger.Warning("[OldCabLightBoardSupport] _gsUpdate field not found, LED may not update");
            }
        }
        catch (System.Exception ex)
        {
            MelonLoader.MelonLogger.Error($"[OldCabLightBoardSupport] Failed to set woofer LED: {ex.Message}\n{ex.StackTrace}");
        }
    }
}