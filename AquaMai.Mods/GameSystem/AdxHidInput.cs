using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AMDaemon;
using AquaMai.Config.Attributes;
using AquaMai.Config.Types;
using AquaMai.Core.Attributes;
using AquaMai.Core.Helpers;
using AquaMai.Mods.Tweaks;
using HarmonyLib;
using HidLibrary;
using Main;
using Manager;
using MelonLoader;
using UnityEngine;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "ADX HID 输入",
    defaultOn: true,
    en: "Input using ADX HID firmware (If you are not using ADX's HID firmware, enabling this won't do anything)",
    zh: "使用 ADX HID 固件的自定义输入（没有 ADX 的话开了也不会加载，也没有坏处）")]
public class AdxHidInput
{
    private static HidDevice[] adxController = new HidDevice[2];
    private static byte[,] inputBuf = new byte[2, 32];
    private static byte[,] inputBufPending = new byte[2, 32];
    private static double[] td = [0, 0];
    private static bool tdEnabled, keyEnabled, pipeEnabled;
    private static bool[] hidThreadRunning = [false, false];

    private static bool TryConnectDevice(int p)
    {
        var device = p == 0
            ? HidDevices.Enumerate(0x2E3C, [0x5750, 0x5767]).FirstOrDefault(it => !it.DevicePath.EndsWith("kbd"))
            : HidDevices.Enumerate(0x2E4C, 0x5750).Concat(HidDevices.Enumerate(0x2E3C, 0x5768)).FirstOrDefault(it => !it.DevicePath.EndsWith("kbd"));

        if (device == null) return false;

        adxController[p] = device;
        device.OpenDevice();
        MelonLogger.Msg($"[HidInput] Device {p + 1}P connected");

        return true;
    }

    private static bool IsDeviceAvailable(int p)
    {
        var device = adxController[p];
        if (device == null) return false;

        try
        {
            return device.IsConnected;
        }
        catch
        {
            return false;
        }
    }

    private static void DisconnectDevice(int p)
    {
        var device = adxController[p];
        if (device == null) return;

        try
        {
            device.CloseDevice();
        }
        catch
        {
            // ignore
        }

        adxController[p] = null;

        for (int i = 0; i < 32; i++)
        {
            inputBuf[p, i] = 0;
            inputBufPending[p, i] = 0;
        }

        MelonLogger.Msg($"[HidInput] Device {p + 1}P disconnected");
    }

    private static bool NeedsButtonInput(int p)
    {
        var device = adxController[p];
        if (device == null) return false;
        try
        {
            return device.Attributes.ProductId is not (0x5767 or 0x5768);
        }
        catch
        {
            return false;
        }
    }

    private static void HidInputThread(int p)
    {
        while (true)
        {
            while (!IsDeviceAvailable(p))
            {
                Thread.Sleep(500);
                TryConnectDevice(p);
            }

            if (!NeedsButtonInput(p))
            {
                Thread.Sleep(500);
                continue;
            }

            var device = adxController[p];
            if (device == null) continue;

            try
            {
                var report = device.Read();

                if (report.Status != HidDeviceData.ReadStatus.Success)
                {
                    DisconnectDevice(p);
                    continue;
                }

                if (report.Data.Length <= 13) continue;

                for (int i = 0; i < 14; i++)
                {
                    var newState = report.Data[i];
                    if (newState == 1 && inputBuf[p, i] == 0)
                    {
                        inputBufPending[p, i] = 1;
                    }
                    inputBuf[p, i] = newState;
                }
            }
            catch
            {
                DisconnectDevice(p);
            }
        }
    }

    private static void TdInit(int p)
    {
        adxController[p].OpenDevice();
        var arr = new byte[64];
        arr[0] = 71;
        adxController[p].WriteReportSync(new HidReport(64)
        {
            ReportId = 1,
            Data = arr,
        });
        Thread.Sleep(100);
        var rpt = adxController[p].ReadReportSync(1);
        if (rpt.Data[0] != 71)
        {
            MelonLogger.Msg($"[HidInput] TD Init {p} Failed");
            return;
        }
        if (rpt.Data[5] < 110) return;
        pipeEnabled = true;
        if (!LedBrightnessControl.shouldEnableImplicitly)
        {
            LedBrightnessControl.shouldEnableImplicitly = true;
            LedBrightnessControl.button1p *= 0.8f;
            LedBrightnessControl.button2p *= 0.8f;
            LedBrightnessControl.cabinet1p *= 0.8f;
            LedBrightnessControl.cabinet2p *= 0.8f;
        }
        arr[0] = 0x73;
        adxController[p].WriteReportSync(new HidReport(64)
        {
            ReportId = 1,
            Data = arr,
        });
        Thread.Sleep(100);
        rpt = adxController[p].ReadReportSync(1);
        if (rpt.Data[0] != 0x73)
        {
            MelonLogger.Msg($"[HidInput] TD Init {p} Failed");
            return;
        }
        if (rpt.Data[2] == 0) return;
        td[p] = rpt.Data[2] * 0.25;
        tdEnabled = true;
        MelonLogger.Msg($"[HidInput] TD Init {p} OK, {td[p]} ms");
    }

    public static void OnBeforeEnableCheck()
    {
        TryConnectDevice(0);
        TryConnectDevice(1);

        for (int i = 0; i < 2; i++)
        {
            if (adxController[i] != null)
            {
                TdInit(i);
            }
        }

        if (disableButtons) return;

        for (int i = 0; i < 2; i++)
        {
            if (hidThreadRunning[i]) continue;

            keyEnabled = true;
            hidThreadRunning[i] = true;
            var p = i;
            var hidThread = new Thread(() => HidInputThread(p))
            {
                IsBackground = true
            };
            hidThread.Start();
        }
    }

    public static void OnAfterPatch()
    {
        if (!keyEnabled) return;
        JvsSwitchHook.RegisterButtonChecker(IsButtonPushed);
        JvsSwitchHook.RegisterAuxiliaryStateProvider(GetAuxiliaryState);
    }

    private static bool IsButtonPushed(int playerNo, int buttonIndex1To8)
    {
        int bufIndex = buttonIndex1To8 switch
        {
            1 => 5,
            2 => 4,
            3 => 3,
            4 => 2,
            5 => 9,
            6 => 8,
            7 => 7,
            8 => 6,
            _ => -1,
        };
        if (bufIndex < 0) return false;

        if (inputBufPending[playerNo, bufIndex] == 1)
        {
            inputBufPending[playerNo, bufIndex] = 0;
            return true;
        }
        return inputBuf[playerNo, bufIndex] == 1;
    }

    [ConfigEntry(name: "按钮 1（向上的三角键）")]
    private static readonly IOKeyMap button1 = IOKeyMap.Select1P;

    [ConfigEntry(name: "按钮 2（三角键中间的圆形按键）")]
    private static readonly IOKeyMap button2 = IOKeyMap.Service;

    [ConfigEntry(name: "按钮 3（向下的三角键）")]
    private static readonly IOKeyMap button3 = IOKeyMap.Select2P;

    [ConfigEntry(name: "按钮 4（最下方的圆形按键）")]
    private static readonly IOKeyMap button4 = IOKeyMap.Test;

    [ConfigEntry("禁用外键输入")]
    private static readonly bool disableButtons = false;

    private static AuxiliaryState GetAuxiliaryState()
    {
        var auxiliaryState = new AuxiliaryState();
        IOKeyMap[] keyMaps = [button1, button2, button3, button4];
        for (int i = 0; i < 4; i++)
        {
            var keyIndex = 10 + i;
            var is1PPushed = inputBufPending[0, keyIndex] == 1 || inputBuf[0, keyIndex] == 1;
            var is2PPushed = inputBufPending[1, keyIndex] == 1 || inputBuf[1, keyIndex] == 1;
            inputBufPending[0, keyIndex] = 0;
            inputBufPending[1, keyIndex] = 0;
            switch (keyMaps[i])
            {
                case IOKeyMap.Select1P:
                    auxiliaryState.select1P |= is1PPushed || is2PPushed;
                    break;
                case IOKeyMap.Select2P:
                    auxiliaryState.select2P |= is1PPushed || is2PPushed;
                    break;
                case IOKeyMap.Select:
                    auxiliaryState.select1P |= is1PPushed;
                    auxiliaryState.select2P |= is2PPushed;
                    break;
                case IOKeyMap.Service:
                    auxiliaryState.service = is1PPushed || is2PPushed;
                    break;
                case IOKeyMap.Test:
                    auxiliaryState.test = is1PPushed || is2PPushed;
                    break;
            }
        }
        return auxiliaryState;
    }

    private static readonly Dictionary<uint, Queue<TouchData>> _queue = new();
    private static readonly object _lockObject = new object();

    private struct TouchData
    {
        public ulong Data;
        public uint Counter;
        public DateTimeOffset Timestamp;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Manager.InputManager), "SetNewTouchPanel")]
    [EnableIf(nameof(tdEnabled))]
    public static bool SetNewTouchPanel(uint index, ref ulong inputData, ref uint counter, ref bool __result)
    {
        var d = td[index];
        if (d <= 0)
        {
            return true;
        }

        lock (_lockObject)
        {
            var currentTime = DateTimeOffset.UtcNow;
            var dequeueCount = 0;

            if (!_queue.ContainsKey(index))
            {
                _queue[index] = new Queue<TouchData>();
            }

            _queue[index].Enqueue(new TouchData
            {
                Data = inputData,
                Counter = counter,
                Timestamp = currentTime,
            });

            var ret = false;
            foreach (var data in _queue[index])
            {
                if ((currentTime - data.Timestamp).TotalMilliseconds < d) break;
                ret = true;
                dequeueCount++;

                inputData = data.Data;
                counter = data.Counter;
            }

            for (var i = 0; i < dequeueCount; i++)
            {
                _queue[index].Dequeue();
            }

            return ret;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameMainObject), "Awake")]
    [EnableIf(nameof(pipeEnabled))]
    public static void OnGameMainObjectAwake(GameMainObject __instance)
    {
        __instance.gameObject.AddComponent<Pipe>();
    }

    private class Pipe : MonoBehaviour
    {
        private NamedPipeServerStream pipeServer;
        private bool isConnecting;

        private void Start()
        {
            StartPipeServer();
        }

        private void StartPipeServer()
        {
            if (isConnecting || (pipeServer != null && pipeServer.IsConnected))
            {
                return;
            }

            isConnecting = true;

            new Thread(() =>
            {
                try
                {
                    try
                    {
                        pipeServer?.Dispose();
                    }
                    catch
                    {
                    }

                    pipeServer = new NamedPipeServerStream(
                        "AquaMai.AdxHidInput",
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Byte
                    );

                    pipeServer.WaitForConnection();
                }
                catch (Exception e)
                {
                    pipeServer = null;
                    MelonLogger.Msg($"[HidInput] Pipe Server Error: {e.Message}");
                }
                finally
                {
                    isConnecting = false;
                }
            })
            {
                IsBackground = true
            };
        }

        private void Update()
        {
            if (pipeServer == null || !pipeServer.IsConnected)
            {
                if (!isConnecting)
                {
                    StartPipeServer();
                }
                return;
            }

            try
            {
                var report = new byte[34 * 2 + 1];
                report[0] = 1;
                for (var player = 0; player < 2; player++)
                {
                    for (var area = 0; area < 34; area++)
                    {
                        report[1 + player * 34 + area] =
                            InputManager.GetTouchPanelAreaPush(player, (InputManager.TouchPanelArea)area)
                                ? (byte)1
                                : (byte)0;
                    }
                }

                pipeServer.Write(report, 0, report.Length);
            }
            catch
            {
                try
                {
                    pipeServer?.Dispose();
                }
                catch
                {
                }

                pipeServer = null;
            }
        }

        private void OnDestroy()
        {
            try
            {
                pipeServer?.Dispose();
            }
            catch
            {
            }
            pipeServer = null;
        }
    }
}