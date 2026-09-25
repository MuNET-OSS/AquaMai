using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AMDaemon;
using AquaMai.Config.Attributes;
using AquaMai.Config.Types;
using AquaMai.Core.Attributes;
using AquaMai.Core.Helpers;
using AquaMai.Mods.Fix;
using AquaMai.Mods.GameSystem.Lib;
using AquaMai.Mods.Tweaks;
using Comio;
using HarmonyLib;
using HidLibrary;
using Main;
using Manager;
using Mecha;
using MelonLoader;
using UnityEngine;

namespace AquaMai.Mods.GameSystem.AdxHidInput;

[ConfigSection(
    name: "ADX / NPRO HID",
    defaultOn: true,
    en: "Input using ADX / NPRO HID (If you are not using ADX / NPRO, enabling this won't do anything)",
    zh: "使用 ADX / NPRO 的自定义输入（没有 ADX / NPRO 的话开了也不会加载，也没有坏处）")]
[ConfigCollapseNamespace]
public class AdxHidInput
{
    private const int NoronDxVid = 0x2E3C;
    private const int NoronDx1PProductId = 0x5751;
    private const int NoronDx2PProductId = 0x5752;
    private const int NoronDxReportLength = 8;
    private const ulong TouchMask = (1UL << 34) - 1;

    private static readonly int[] buttonBitMap = [5, 4, 3, 2, 9, 8, 7, 6];
    private static HidDevice[] adxController = new HidDevice[2];
    private static byte[][] readBuffer = [null, null];
    private static readonly InputLatch[] inputLatch = [new(), new()];
    private static readonly InputLatch[] touchLatch = [new(), new()];
    private static double[] td = [0, 0];
    private static bool tdEnabled, keyEnabled, pipeEnabled;
    private static bool[] hidThreadRunning = [false, false];
    private static bool[] connected = [false, false];
    private static bool[] useNoronDxProtocol = [false, false];
    private static bool[] touchProviderPending = [false, false];
    private static bool[] touchProviderRegistered = [false, false];
    private static bool[] ledBrightnessAdjusted = [false, false];

    /// <summary>
    /// NPro 自定义固件走 WinUSB（GAME 管道），与 ADX/IO4 的 HID 并列。
    /// 每个玩家槽位同一时刻只会是其中一种，上层输入/灯光逻辑共用。
    /// </summary>
    private static readonly NproDevice[] nproDevice = [null, null];

    /// <summary>该槽位当前使用的传输方式。</summary>
    private enum DeviceKind
    {
        None,
        Hid,
        Npro,
    }

    private static readonly DeviceKind[] deviceKind = [DeviceKind.None, DeviceKind.None];

    /// <summary>1P/2P 的 VID/PID；NPro 的 GAME 管道按 PID 区分玩家。</summary>
    private static int NoronPidFor(int p) => p == 0 ? NoronDx1PProductId : NoronDx2PProductId;

    private static void HandleNproInput(int p, ulong touch, byte buttons, byte extButtons)
    {
        touchLatch[p].Update(touch & TouchMask);

        ulong buttonState = 0;
        if (IsButtonInputEnabled(p))
        {
            for (int i = 0; i < buttonBitMap.Length; i++)
            {
                if ((buttons & (1 << i)) != 0)
                {
                    buttonState |= 1UL << buttonBitMap[i];
                }
            }

            buttonState |= (ulong)(extButtons & 0x0F) << 10;
        }

        inputLatch[p].Update(buttonState);
    }

    private static bool TryConnectNpro(int p)
    {
        try
        {
            var device = new NproDevice(p, (touch, buttons, ext) => HandleNproInput(p, touch, buttons, ext));
            if (!device.TryConnect())
            {
                device.Dispose();
                return false;
            }

            nproDevice[p] = device;
            deviceKind[p] = DeviceKind.Npro;
            connected[p] = true;
            touchProviderPending[p] = true;
            MelonLogger.Msg($"[HidInput] Device {p + 1}P connected (NPro WinUSB)");
            return true;
        }
        catch (Exception e)
        {
            MelonLogger.Msg($"[HidInput] NPro {p + 1}P 连接失败: {e.Message}");
            return false;
        }
    }

    private static bool TryConnectDevice(int p)
    {
        // 新固件的 NPro 不再暴露 HID，优先尝试 WinUSB GAME 管道；
        // 旧固件仍是 HID，回落到下面的枚举逻辑即可。
        if (deviceKind[p] == DeviceKind.None && TryConnectNpro(p)) return true;

        var device = p == 0
            ? HidDevices.Enumerate(NoronDxVid, NoronDx1PProductId)
                .Concat(HidDevices.Enumerate(0x2E3C, [0x5750, 0x5767]))
                .FirstOrDefault(it => !it.DevicePath.EndsWith("kbd"))
            : HidDevices.Enumerate(NoronDxVid, NoronDx2PProductId)
                .Concat(HidDevices.Enumerate(0x2E4C, 0x5750))
                .Concat(HidDevices.Enumerate(0x2E3C, 0x5768))
                .FirstOrDefault(it => !it.DevicePath.EndsWith("kbd"));

        if (device == null) return false;

        adxController[p] = device;
        device.OpenDevice();
        readBuffer[p] = new byte[device.Capabilities.InputReportByteLength];
        useNoronDxProtocol[p] = device.Attributes.ProductId is NoronDx1PProductId or NoronDx2PProductId;
        connected[p] = true;
        deviceKind[p] = DeviceKind.Hid;
        if (useNoronDxProtocol[p])
        {
            touchProviderPending[p] = true;
        }
        MelonLogger.Msg($"[HidInput] Device {p + 1}P connected{(useNoronDxProtocol[p] ? " (NPro)" : "")}");

        return true;
    }

    private static bool IsDeviceConnected(int p)
    {
        if (deviceKind[p] == DeviceKind.Npro)
            return nproDevice[p] != null && nproDevice[p].IsConnected;
        return adxController[p] != null && connected[p];
    }

    private static void RegisterPendingTouchProviders()
    {
        for (int p = 0; p < touchProviderPending.Length; p++)
        {
            if (!touchProviderPending[p]) continue;
            touchProviderPending[p] = false;
            if (touchProviderRegistered[p]) continue;
            TouchStatusProvider.RegisterTouchStatusProvider(p, GetTouchState);
            touchProviderRegistered[p] = true;
        }
    }

    private static void DisconnectDevice(int p)
    {
        var npro = nproDevice[p];
        if (npro != null)
        {
            try
            {
                npro.Dispose();
            }
            catch
            {
                // ignore
            }

            nproDevice[p] = null;
        }

        var device = adxController[p];
        if (device != null)
        {
            try
            {
                device.CloseDevice();
            }
            catch
            {
                // ignore
            }
        }

        connected[p] = false;
        adxController[p] = null;
        readBuffer[p] = null;
        deviceKind[p] = DeviceKind.None;

        inputLatch[p].Clear();
        touchLatch[p].Clear();
        useNoronDxProtocol[p] = false;
        touchProviderPending[p] = false;

        MelonLogger.Msg($"[HidInput] Device {p + 1}P disconnected");
    }

    private static bool NeedsButtonInput(int p)
    {
        // NPro 的 GAME 管道始终可用于输入。
        if (deviceKind[p] == DeviceKind.Npro) return true;

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

            // NPro：输入由 NproDevice 自己的读线程处理，这里只负责保活与断线检测。
            if (deviceKind[p] == DeviceKind.Npro)
            {
                var npro = nproDevice[p];
                if (npro == null)
                {
                    if (!RealHotPlugSupport) return;
                    Thread.Sleep(500);
                    continue;
                }

                if (!npro.IsConnected)
                {
                    DisconnectDevice(p);
                    if (!RealHotPlugSupport) return;
                    continue;
                }

                npro.TickKeepAlive();
                Thread.Sleep(50);
                continue;
            }

            if (!useNoronDxProtocol[p] && !IsButtonInputEnabled(p))
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
                if (!HidRawIO.Read(device, buf, out var bytesRead))
                {
                    DisconnectDevice(p);
                    if (!RealHotPlugSupport) return;
                    continue;
                }

                if (useNoronDxProtocol[p])
                {
                    if (!TryProcessNoronDxReport(p, buf, bytesRead))
                    {
                        DisconnectDevice(p);
                        if (!RealHotPlugSupport) return;
                    }
                    continue;
                }

                if (bytesRead <= 13)
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

    private static bool TryProcessNoronDxReport(int p, byte[] buffer, int bytesRead)
    {
        if (bytesRead < NoronDxReportLength) return false;

        var offset = bytesRead >= NoronDxReportLength + 1 && buffer[0] == 0 ? 1 : 0;
        if (bytesRead < offset + NoronDxReportLength) return false;

        ulong touchState = 0;
        for (int i = 0; i < 6; i++)
        {
            touchState |= (ulong)buffer[offset + i] << (i * 8);
        }
        var activeTouchState = touchState & TouchMask;
        touchLatch[p].Update(activeTouchState);

        ulong buttonState = 0;
        if (IsButtonInputEnabled(p))
        {
            var buttons = buffer[offset + 6];
            for (int i = 0; i < buttonBitMap.Length; i++)
            {
                if ((buttons & (1 << i)) != 0)
                {
                    buttonState |= 1UL << buttonBitMap[i];
                }
            }

            buttonState |= (ulong)(buffer[offset + 7] & 0x0F) << 10;
        }
        inputLatch[p].Update(buttonState);
        return true;
    }

    private static ulong GetTouchState(int p)
    {
        var hasTouch = deviceKind[p] == DeviceKind.Npro || useNoronDxProtocol[p];
        return hasTouch ? touchLatch[p].ReadBits(TouchMask) : 0;
    }

    private static void TdInit(int p)
    {
        if (useNoronDxProtocol[p]) return;

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
        if (!ledBrightnessAdjusted[p])
        {
            ledBrightnessAdjusted[p] = true;
            LedBrightnessControl.shouldEnableImplicitly = true;
            if(p == 0)
            {
                LedBrightnessControl.button1p *= 0.8f;
                LedBrightnessControl.cabinet1p *= 0.8f;
            }
            else
            {
                LedBrightnessControl.button2p *= 0.8f;
                LedBrightnessControl.cabinet2p *= 0.8f;
            }
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
        InitKeyMaps();
        TryConnectDevice(0);
        TryConnectDevice(1);

        for (int i = 0; i < 2; i++)
        {
            if (adxController[i] != null)
            {
                TdInit(i);
            }
        }

        for (int i = 0; i < 2; i++)
        {
            var buttonInputEnabled = IsButtonInputEnabled(i);
            if (!buttonInputEnabled && !useNoronDxProtocol[i] && deviceKind[i] != DeviceKind.Npro && !RealHotPlugSupport) continue;
            if (hidThreadRunning[i]) continue;
            if (!RealHotPlugSupport && adxController[i] == null && deviceKind[i] != DeviceKind.Npro) continue;
            if (!RealHotPlugSupport && !NeedsButtonInput(i)) continue;

            keyEnabled |= buttonInputEnabled;
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
        RegisterPendingTouchProviders();
        if (!keyEnabled) return;
        JvsSwitchHook.RegisterButtonChecker(IsButtonPushed);
        JvsSwitchHook.RegisterAuxiliaryStateProvider(GetAuxiliaryState);
        JvsSwitchHook.RegisterCustomFnStateProvider(GetCustomFnState);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameMain), "Update")]
    public static void PreGameMainUpdate()
    {
        RegisterPendingTouchProviders();
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

    [ConfigEntry(name: "1P 按钮 1", zh: "向上的三角键")]
    private static readonly IOKeyMap p1Button1 = IOKeyMap.Select1P;

    [ConfigEntry(name: "1P 按钮 2", zh: "三角键中间的圆形按键")]
    private static readonly IOKeyMap p1Button2 = IOKeyMap.Service;

    [ConfigEntry(name: "1P 按钮 3", zh: "向下的三角键")]
    private static readonly IOKeyMap p1Button3 = IOKeyMap.Select2P;

    [ConfigEntry(name: "1P 按钮 4", zh: "最下方的圆形按键")]
    private static readonly IOKeyMap p1Button4 = IOKeyMap.Test;

    [ConfigEntry("1P 禁用外键输入")]
    private static readonly bool p1DisableButtons = false;

    [ConfigEntry(name: "2P 按钮 1", zh: "向上的三角键")]
    private static readonly IOKeyMap p2Button1 = IOKeyMap.Select1P;

    [ConfigEntry(name: "2P 按钮 2", zh: "三角键中间的圆形按键")]
    private static readonly IOKeyMap p2Button2 = IOKeyMap.Service;

    [ConfigEntry(name: "2P 按钮 3", zh: "向下的三角键")]
    private static readonly IOKeyMap p2Button3 = IOKeyMap.Select2P;

    [ConfigEntry(name: "2P 按钮 4", zh: "最下方的圆形按键")]
    private static readonly IOKeyMap p2Button4 = IOKeyMap.Test;

    [ConfigEntry("2P 禁用外键输入")]
    private static readonly bool p2DisableButtons = false;

    private static readonly IOKeyMap[][] keyMaps = new IOKeyMap[2][];

    private static void InitKeyMaps()
    {
        keyMaps[0] = [p1Button1, p1Button2, p1Button3, p1Button4];
        keyMaps[1] = [p2Button1, p2Button2, p2Button3, p2Button4];
    }

    private static bool IsButtonInputEnabled(int p)
    {
        return p == 0 ? !p1DisableButtons : !p2DisableButtons;
    }

    private static void ApplyAuxiliaryInput(ref AuxiliaryState state, IOKeyMap keyMap, bool isPushed, int playerNo)
    {
        switch (keyMap)
        {
            case IOKeyMap.Select1P:
                state.select1P |= isPushed;
                break;
            case IOKeyMap.Select2P:
                state.select2P |= isPushed;
                break;
            case IOKeyMap.Select:
                if (playerNo == 0) state.select1P |= isPushed;
                else state.select2P |= isPushed;
                break;
            case IOKeyMap.Service:
                state.service |= isPushed;
                break;
            case IOKeyMap.Test:
                state.test |= isPushed;
                break;
        }
    }

    private static AuxiliaryState GetAuxiliaryState()
    {
        var auxiliaryState = new AuxiliaryState();
        for (int p = 0; p < 2; p++)
        {
            var maps = keyMaps[p];
            for (int i = 0; i < 4; i++)
            {
                var keyIndex = 10 + i;
                var isPushed = inputLatch[p].ReadBit(keyIndex);
                ApplyAuxiliaryInput(ref auxiliaryState, maps[i], isPushed, p);
            }
        }
        return auxiliaryState;
    }

    private static CustomFnState GetCustomFnState()
    {
        var result = new CustomFnState();
        for (int p = 0; p < 2; p++)
        {
            var maps = keyMaps[p];
            for (int i = 0; i < 4; i++)
            {
                var keyIndex = 10 + i;
                var isPushed = inputLatch[p].ReadBit(keyIndex);
                switch (maps[i])
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

    /// <summary>
    /// NPro 的灯光接管：在串口发送层转发游戏的原始 SEGA 灯板帧。
    ///
    /// 组包与游戏 IoCtrl 完全一致，fade 仍由板子插值，因此视觉行为与真机 1:1；
    /// mod 只负责"有个东西在发"，从而避免游戏静置时 LED 通道静默、固件 5s 超时
    /// 回落到 idle 灯效。
    /// </summary>
    /// <remarks>
    /// 类级 <see cref="HarmonyPatch"/> 是必需的：Startup 收集嵌套补丁时，
    /// 会跳过没有任何特性的嵌套类（见 AquaMai.Core/Startup.cs）。
    /// </remarks>
    [HarmonyPatch]
    public static class NproLedPatches
    {
        private static readonly FieldInfo ControlHostField = AccessTools.Field(typeof(Bd15070_4Control), "_host");
        private static readonly FieldInfo RequestQueueField = AccessTools.Field(typeof(Host), "_reqPacketQueue");
        private static readonly FieldInfo SendQueueField = AccessTools.Field(typeof(Host), "_sendPacketQueue");
        private static readonly FieldInfo WriteBufferField = AccessTools.Field(typeof(Host), "_writeBuffer");
        private static readonly ConcurrentDictionary<Host, int> hostPlayers = new();

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Bd15070_4Control), MethodType.Constructor, typeof(string), typeof(int))]
        public static void PostControlConstructor(Bd15070_4Control __instance, int index)
        {
            if (index < 0 || index > 1) return;
            if (ControlHostField.GetValue(__instance) is Host host)
            {
                hostPlayers[host] = index;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Host), "_send")]
        public static void PreHostSend(Host __instance)
        {
            if (!hostPlayers.TryGetValue(__instance, out var p)) return;
            if (deviceKind[p] != DeviceKind.Npro) return;

            var npro = nproDevice[p];
            if (npro == null) return;

            // 续写上一帧时包已经转发过，避免部分写重试造成重复。
            var writeBuffer = (Packet)WriteBufferField.GetValue(__instance);
            if (writeBuffer.Count > 0) return;

            var requestQueue = (BoardCtrlBase.PacketQueue)RequestQueueField.GetValue(__instance);
            var sendQueue = (BoardCtrlBase.PacketQueue)SendQueueField.GetValue(__instance);
            var packet = sendQueue.Count > 0
                ? sendQueue.Peek()
                : requestQueue.Count > 0
                    ? requestQueue.Peek()
                    : null;

            if (packet != null)
            {
                npro.SendLedPacket(packet);
            }
        }
    }
}
