using System;
using System.Runtime.InteropServices;
using AquaMai.Config.Attributes;
using AquaMai.Config.Types;
using AquaMai.Core.Helpers;
using HarmonyLib;
using Main;
using MelonLoader;

namespace AquaMai.Mods.UX;

public enum VKCode // Windows上使用的Virtual-Key键盘码值表（又称标准键盘码值表），调用Windows API时使用
{
    None = 0,
    Alpha0 = 0x30,   // 0 键
    Alpha1 = 0x31,
    Alpha2 = 0x32,
    Alpha3 = 0x33,
    Alpha4 = 0x34,
    Alpha5 = 0x35,
    Alpha6 = 0x36,
    Alpha7 = 0x37,
    Alpha8 = 0x38,
    Alpha9 = 0x39,
    Keypad0 = 0x60,  // 数字键盘 0 键
    Keypad1 = 0x61,
    Keypad2 = 0x62,
    Keypad3 = 0x63,
    Keypad4 = 0x64,
    Keypad5 = 0x65,
    Keypad6 = 0x66,
    Keypad7 = 0x67,
    Keypad8 = 0x68,
    Keypad9 = 0x69,
    F1 = 0x70,
    F2 = 0x71,
    F3 = 0x72,
    F4 = 0x73,
    F5 = 0x74,
    F6 = 0x75,
    F7 = 0x76,
    F8 = 0x77,
    F9 = 0x78,
    F10 = 0x79,
    F11 = 0x7A,
    F12 = 0x7B,
    Enter = 0x0D,    // VK_RETURN 输入键
    Space = 0x20,    // 空格键
    Backspace = 0x08, // VK_BACK
    Tab = 0x09,
    Esc = 0x1B,      // VK_ESCAPE
    Insert = 0x2D,
    Delete = 0x2E,
    Home = 0x24,
    End = 0x23,
    Pause = 0x13,
    PageUp = 0x21,   // VK_PRIOR
    PageDown = 0x22, // VK_NEXT
    UpArrow = 0x26,  // VK_UP
    DownArrow = 0x28, // VK_DOWN
    LeftArrow = 0x25, // VK_LEFT
    RightArrow = 0x27, // VK_RIGHT
}

// 收编自 https://github.com/Starrah/StarrahMai/blob/master/MaimollerCoin.cs
[ConfigSection(
    name: "自定义功能键映射",
    en: "Map custom function keys to system-level keyboard keys. By mapping them to keys such as Enter, you can use cabinet buttons for actions like \"pressing Enter to swipe AIME card\". Note: you should set the corresponding button to \"Custom Function Key\" in your controller's IO settings first.",
    zh: "将自定义功能键映射为系统级键盘按键。可以通过把自定义功能键映射为Enter等按键，实现用机台按键回车刷卡等功能。注意：使用前需要在对应的控制器IO设置中，将相应的物理按键功能设置为“自定义功能键”。")]
public static class CustomFnToKeyboard
{
    [ConfigEntry(name: "自定义按键1")]
    public static readonly VKCode CustomFn1 = VKCode.None; // 把自定义功能键1映射为键盘上的哪个键。默认均为禁用。
    [ConfigEntry(name: "自定义按键2")]
    public static readonly VKCode CustomFn2 = VKCode.None;
    [ConfigEntry(name: "自定义按键3")]
    public static readonly VKCode CustomFn3 = VKCode.None;
    [ConfigEntry(name: "自定义按键4")]
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