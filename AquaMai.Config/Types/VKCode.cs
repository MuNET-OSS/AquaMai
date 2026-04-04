namespace AquaMai.Config.Types;

// Windows上使用的Virtual-Key键盘码值表（又称标准键盘码值表），调用Windows API时使用
public enum VKCode
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