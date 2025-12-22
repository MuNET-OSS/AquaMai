using AquaMai.Config.Attributes;
using Comio.BD15070_4;
using HarmonyLib;
using System.Reflection.Emit;
using Mecha;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "舊版燈板映射",
    en: "Remapping Billboard LED to 837-15070-02 Woofer LED, Roof LED, Center LED",
    zh: "重新映射新框頂板 LED 至 837-15070-02 (舊版燈板) 的重低音喇叭 LED、頂板 LED 以及中央 LED")]
public class LegacyBoardMapping
{
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
                MelonLoader.MelonLogger.Msg("[LegacyBoardMapping] Extended Bd15070_4IF._switchParam size and its initialize for loop from 8 to 10!");
            }
            else
            {
                MelonLoader.MelonLogger.Warning($"[LegacyBoardMapping] Bd15070_4IF._switchParam patching failed (patched {patchedNum}/2)");
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
                MelonLoader.MelonLogger.Error("[LegacyBoardMapping] JvsOutputPwm type not found");
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
            MelonLoader.MelonLogger.Warning("[LegacyLedBoardMapping] MechaManager not initialized, cannot set woofer LED");
            return;
        }

        // Get the LED interface for the player
        var ledIf = IO.MechaManager.LedIf;
        if (ledIf == null || playerIndex >= ledIf.Length || ledIf[playerIndex] == null)
        {
            MelonLoader.MelonLogger.Warning($"[LegacyLedBoardMapping] LED interface not available for player {playerIndex}");
            return;
        }

        // Use reflection to access the IoCtrl and call SetLedGs8BitCommand[8] directly
        // Then set _gsUpdate flag so PreExecute() sends the update command (just like buttons do)
        try
        {
            var ledIfType = typeof(Bd15070_4IF);
            var controlField = ledIfType.GetField("_control", BindingFlags.NonPublic | BindingFlags.Instance);

            if (controlField == null)
            {
                MelonLoader.MelonLogger.Error("[LegacyLedBoardMapping] _control field not found in Bd15070_4IF");
                return;
            }

            var control = controlField.GetValue(ledIf[playerIndex]);
            if (control == null)
            {
                MelonLoader.MelonLogger.Error("[LegacyLedBoardMapping] Control object is null");
                return;
            }

            // Get _board field from Bd15070_4Control
            var controlType = control.GetType();
            var boardField = controlType.GetField("_board", BindingFlags.NonPublic | BindingFlags.Instance);
            if (boardField == null)
            {
                MelonLoader.MelonLogger.Error("[LegacyLedBoardMapping] _board field not found in Bd15070_4Control");
                return;
            }

            var board = boardField.GetValue(control);
            if (board == null)
            {
                MelonLoader.MelonLogger.Error("[LegacyLedBoardMapping] Board object is null");
                return;
            }

            // Get _ctrl field from Board15070_4
            var boardType = board.GetType();
            var ctrlField = boardType.GetField("_ctrl", BindingFlags.NonPublic | BindingFlags.Instance);
            if (ctrlField == null)
            {
                MelonLoader.MelonLogger.Error("[LegacyLedBoardMapping] _ctrl field not found in Board15070_4");
                return;
            }

            var boardCtrl = ctrlField.GetValue(board);
            if (boardCtrl == null)
            {
                MelonLoader.MelonLogger.Error("[LegacyLedBoardMapping] BoardCtrl object is null");
                return;
            }

            // Get _ioCtrl field from BoardCtrl15070_4
            var boardCtrlType = boardCtrl.GetType();
            var ioCtrlField = boardCtrlType.GetField("_ioCtrl", BindingFlags.NonPublic | BindingFlags.Instance);
            if (ioCtrlField == null)
            {
                MelonLoader.MelonLogger.Error("[LegacyLedBoardMapping] _ioCtrl field not found in BoardCtrl15070_4");
                return;
            }

            var ioCtrl = ioCtrlField.GetValue(boardCtrl);
            if (ioCtrl == null)
            {
                MelonLoader.MelonLogger.Error("[LegacyLedBoardMapping] IoCtrl object is null");
                return;
            }

            // Get SetLedGs8BitCommand array from IoCtrl (public field)
            var ioCtrlType = typeof(IoCtrl);
            var setLedGs8BitCommandField = ioCtrlType.GetField("SetLedGs8BitCommand", BindingFlags.Public | BindingFlags.Instance);
            if (setLedGs8BitCommandField == null)
            {
                MelonLoader.MelonLogger.Error("[LegacyLedBoardMapping] SetLedGs8BitCommand field not found in IoCtrl");
                return;
            }

            var setLedGs8BitCommandArray = setLedGs8BitCommandField.GetValue(ioCtrl) as SetLedGs8BitCommand[];
            if (setLedGs8BitCommandArray == null || setLedGs8BitCommandArray.Length <= 8)
            {
                MelonLoader.MelonLogger.Error("[LegacyLedBoardMapping] SetLedGs8BitCommand array is null or too small");
                return;
            }

            // Get SendForceCommand method from BoardCtrl15070_4
            var sendForceCommandMethod = boardCtrlType.GetMethod("SendForceCommand", BindingFlags.Public | BindingFlags.Instance);
            if (sendForceCommandMethod == null)
            {
                MelonLoader.MelonLogger.Error("[LegacyLedBoardMapping] SendForceCommand method not found in BoardCtrl15070_4");
                return;
            }

            // Use SetLedGs8BitCommand[8] and SetLedGs8BitCommand[9] directly (same as buttons 0-7, but for ledPos = 8 and 9)
            // This bypasses the FET command path in IoCtrl.SetLedData(), as they are not via FET, they are like buttons
            // ledPos = 8 == woofer & roof
            // ledPos = 9 == center
            setLedGs8BitCommandArray[8].setColor(8, color);
            setLedGs8BitCommandArray[9].setColor(9, color);
            sendForceCommandMethod.Invoke(boardCtrl, new object[] { setLedGs8BitCommandArray[8] });

            // Set the _gsUpdate flag on Bd15070_4IF so PreExecute() sends the update command
            // This matches exactly how buttons work - they set _gsUpdate = true
            var gsUpdateField = ledIfType.GetField("_gsUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
            if (gsUpdateField != null)
            {
                gsUpdateField.SetValue(ledIf[playerIndex], true);
            }
            else
            {
                MelonLoader.MelonLogger.Warning("[LegacyLedBoardMapping] _gsUpdate field not found, LED may not update");
            }
        }
        catch (System.Exception ex)
        {
            MelonLoader.MelonLogger.Error($"[LegacyLedBoardMapping] Failed to set woofer LED: {ex.Message}\n{ex.StackTrace}");
        }
    }
}

