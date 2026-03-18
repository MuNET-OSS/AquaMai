using System;
using System.Runtime.InteropServices;
using AquaMai.Config.Attributes;
using AquaMai.Config.Types;
using AquaMai.Core.Helpers;
using HarmonyLib;
using Main;
using MelonLoader;

namespace AquaMai.Mods.UX;

// 收编自 https://github.com/Starrah/StarrahMai/blob/master/MaimollerCoin.cs
[ConfigSection(
    name: "自定义功能键映射",
    en: "Map custom function keys to system-level keyboard keys. By mapping them to keys such as Enter, you can use cabinet buttons for actions like \"pressing Enter to swipe AIME card\". Note: you should set the corresponding button to \"Custom Function Key\" in your controller's IO settings first.",
    zh: "将控制器上的自定义功能键映射为系统级键盘按键。可以通过把功能键映射为Enter等按键，实现用机台按键回车刷卡等功能。注意：使用前需要在对应的控制器IO设置中，将相应的物理按键功能设置为“自定义功能键”。")]
public static class CustomFnToKeyboard
{
    [ConfigEntry(name: "自定义功能键1")]
    public static readonly VKCode CustomFn1 = VKCode.None; // 把自定义功能键1映射为键盘上的哪个键。默认均为禁用。
    [ConfigEntry(name: "自定义功能键2")]
    public static readonly VKCode CustomFn2 = VKCode.None;
    [ConfigEntry(name: "自定义功能键3")]
    public static readonly VKCode CustomFn3 = VKCode.None;
    [ConfigEntry(name: "自定义功能键4")]
    public static readonly VKCode CustomFn4 = VKCode.None;
    
    // 与发送系统级按键事件有关的结构体/外部接口等。
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public int type; public INPUTUNION U; }
    [StructLayout(LayoutKind.Explicit, Size = 32, Pack = 8)] // 重要，以符合Win32对INPUT事件结构体的内存排列要求。
    private struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameMainObject), "Update")]
    public static void CheckCustomFnKey()
    {
        if (CustomFn1 > 0) SendInputEvent(KeyCodeOrName.CustomFn1, CustomFn1);
        if (CustomFn2 > 0) SendInputEvent(KeyCodeOrName.CustomFn2, CustomFn2);
        if (CustomFn3 > 0) SendInputEvent(KeyCodeOrName.CustomFn3, CustomFn3);
        if (CustomFn4 > 0) SendInputEvent(KeyCodeOrName.CustomFn4, CustomFn4);
    }
    
    private static void SendInputEvent(KeyCodeOrName keyCode, VKCode vkCode)
    {
        uint dwFlags;
        if (KeyListener.GetKeyJustDown(keyCode))
        {
            dwFlags = 0; // 按下事件的flag
        } else if (KeyListener.GetKeyJustUp(keyCode))
        {
            dwFlags = 2; // 抬起事件的flag
        }
        else return; // 没有刚刚按下或抬起，不要发送事件
        
        var eventObj = new INPUT{ type = 1 }; // INPUT_KEYBOARD
        eventObj.U.ki.wVk = (ushort)vkCode; // 键码
        eventObj.U.ki.dwFlags = dwFlags;
# if DEBUG
        MelonLogger.Msg($"[CustomFnToKeyboard] {keyCode} set to {vkCode}. Sending Keyboard Event wVk={eventObj.U.ki.wVk} dwFlags={eventObj.U.ki.dwFlags}. Diagnostic: INPUT struct size = { Marshal.SizeOf(typeof(INPUT))}, should be 40.");
# endif
        uint result = SendInput(1, [eventObj], Marshal.SizeOf(typeof(INPUT)));
        if (result == 0)
        {
            MelonLogger.Warning($"[CustomFnToKeyboard] Calling Win32 API SendInput, FAILED, result={result}, lastError={Marshal.GetLastWin32Error()}. Diagnostic: INPUT struct size = { Marshal.SizeOf(typeof(INPUT))}, should be 40.");
        }
    }
}