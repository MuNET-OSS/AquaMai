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
using AquaMai.Mods.Fix;
using AquaMai.Mods.Tweaks;
using HarmonyLib;
using HidLibrary;
using Main;
using Manager;
using MelonLoader;
using UnityEngine;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "ADX / NPRO HID",
    defaultOn: true,
    en: "Input using ADX / NPRO HID (If you are not using ADX / NPRO, enabling this won't do anything)",
    zh: "使用 ADX / NPRO 的自定义输入（没有 ADX / NPRO 的话开了也不会加载，也没有坏处）")]
public class AdxHidInput
{
    private static HidDevice[] adxController = new HidDevice[2];
    private static byte[][] readBuffer = [null, null];
    private static readonly InputLatch[] inputLatch = [new(), new()];
    private static double[] td = [0, 0];
    private static bool tdEnabled, keyEnabled, pipeEnabled;
    private static bool[] hidThreadRunning = [false, false];
    private static bool[] connected = [false, false];

    private static bool TryConnectDevice(int p)
    {
        var device = p == 0
            ? HidDevices.Enumerate(0x2E3C, [0x5750, 0x5767]).FirstOrDefault(it => !it.DevicePath.EndsWith("kbd"))
            : HidDevices.Enumerate(0x2E4C, 0x5750).Concat(HidDevices.Enumerate(0x2E3C, 0x5768)).FirstOrDefault(it => !it.DevicePath.EndsWith("kbd"));

        if (device == null) return false;

        adxController[p] = device;
        device.OpenDevice();
        readBuffer[p] = new byte[device.Capabilities.InputReportByteLength];
        connected[p] = true;
        MelonLogger.Msg($"[HidInput] Device {p + 1}P connected");

        return true;
    }

    private static bool IsDeviceConnected(int p)
    {
        return adxController[p] != null && connected[p];
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

        connected[p] = false;
        adxController[p] = null;
        readBuffer[p] = null;

        inputLatch[p].Clear();

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

    [ConfigEntry("热插拔支持")]
    private static readonly bool hotPlugSupport = true;

    private static bool RealHotPlugSupport => hotPlugSupport && MaimollerFix.shit == null;

    private static void HidInputThread(int p)
    {
        while (true)
        {
            if (RealHotPlugSupport)
            {
                while (!IsDeviceConnected(p))
                {
                    Thread.Sleep(500);
                    TryConnectDevice(p);
                }
            }
            else
            {
                if (!IsDeviceConnected(p)) return;
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
                var buf = readBuffer[p];
                if (buf == null) continue;
                if (!HidRawIO.Read(device, buf, out var bytesRead) || bytesRead <= 13)
                {
                    DisconnectDevice(p);
                    if (!RealHotPlugSupport) return;
                    continue;
                }
                ulong state = 0;
                for (int i = 0; i < 14; i++)
                {
                    if (buf[i] == 1)
                        state |= (1UL << i);
                }
                inputLatch[p].Update(state);
            }
            catch
            {
                DisconnectDevice(p);
                if (!RealHotPlugSupport) return;
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
            if (!RealHotPlugSupport && adxController[i] == null) continue;
            if (!RealHotPlugSupport && !NeedsButtonInput(i)) continue;

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
        JvsSwitchHook.RegisterCustomFnStateProvider(GetCustomFnState);
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

        return inputLatch[playerNo].ReadBit(bufIndex);
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
            var is1PPushed = inputLatch[0].ReadBit(keyIndex);
            var is2PPushed = inputLatch[1].ReadBit(keyIndex);
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

    private static CustomFnState GetCustomFnState()
    {
        var result = new CustomFnState();
        IOKeyMap[] keyMaps = [button1, button2, button3, button4];
        for (int i = 0; i < 4; i++)
        {
            var keyIndex = 10 + i;
            var is1PPushed = inputLatch[0].ReadBit(keyIndex);
            var is2PPushed = inputLatch[1].ReadBit(keyIndex);
            var isPushed = is1PPushed || is2PPushed;
            switch (keyMaps[i])
            {
                case IOKeyMap.CustomFn1:
                    result.CustomFn1 |= isPushed;
                    break;
                case IOKeyMap.CustomFn2:
                    result.CustomFn2 |= isPushed;
                    break;
                case IOKeyMap.CustomFn3:
                    result.CustomFn3 |= isPushed;
                    break;
                case IOKeyMap.CustomFn4:
                    result.CustomFn4 |= isPushed;
                    break;
            }
        }
        return result;
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