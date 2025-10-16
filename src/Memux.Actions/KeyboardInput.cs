using System.Runtime.InteropServices;

namespace Memux.Actions;

/// <summary>
/// Simulates keyboard input using Windows SendInput API
/// </summary>
public class KeyboardInput
{
    public static void PressKey(string key, int durationMs = 50)
    {
        ushort vkCode = GetVirtualKeyCode(key);
        
        // Key down
        SendKeyEvent(vkCode, false);
        
        // Hold
        if (durationMs > 0)
        {
            Thread.Sleep(durationMs);
        }
        
        // Key up
        SendKeyEvent(vkCode, true);
    }
    
    public static void KeyDown(string key)
    {
        ushort vkCode = GetVirtualKeyCode(key);
        SendKeyEvent(vkCode, false);
    }
    
    public static void KeyUp(string key)
    {
        ushort vkCode = GetVirtualKeyCode(key);
        SendKeyEvent(vkCode, true);
    }
    
    private static void SendKeyEvent(ushort vkCode, bool keyUp)
    {
        INPUT input = new INPUT
        {
            type = INPUT_KEYBOARD
        };
        
        input.union.ki.wVk = vkCode;
        input.union.ki.dwFlags = keyUp ? KEYEVENTF_KEYUP : 0;
        
        SendInput(1, ref input, Marshal.SizeOf<INPUT>());
    }
    
    private static ushort GetVirtualKeyCode(string key)
    {
        // Map common key names to virtual key codes
        return key.ToUpper() switch
        {
            // Letters
            "A" => 0x41, "B" => 0x42, "C" => 0x43, "D" => 0x44,
            "E" => 0x45, "F" => 0x46, "G" => 0x47, "H" => 0x48,
            "I" => 0x49, "J" => 0x4A, "K" => 0x4B, "L" => 0x4C,
            "M" => 0x4D, "N" => 0x4E, "O" => 0x4F, "P" => 0x50,
            "Q" => 0x51, "R" => 0x52, "S" => 0x53, "T" => 0x54,
            "U" => 0x55, "V" => 0x56, "W" => 0x57, "X" => 0x58,
            "Y" => 0x59, "Z" => 0x5A,
            
            // Numbers
            "0" => 0x30, "1" => 0x31, "2" => 0x32, "3" => 0x33,
            "4" => 0x34, "5" => 0x35, "6" => 0x36, "7" => 0x37,
            "8" => 0x38, "9" => 0x39,
            
            // Function keys
            "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73,
            "F5" => 0x74, "F6" => 0x75, "F7" => 0x76, "F8" => 0x77,
            "F9" => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
            
            // Special keys
            "SPACE" => 0x20,
            "ENTER" => 0x0D,
            "ESCAPE" or "ESC" => 0x1B,
            "TAB" => 0x09,
            "SHIFT" => 0x10,
            "CTRL" or "CONTROL" => 0x11,
            "ALT" => 0x12,
            "BACKSPACE" => 0x08,
            "DELETE" => 0x2E,
            
            // Arrow keys
            "LEFT" => 0x25,
            "UP" => 0x26,
            "RIGHT" => 0x27,
            "DOWN" => 0x28,
            
            _ => throw new ArgumentException($"Unknown key: {key}")
        };
    }
    
    // Windows API
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, ref INPUT pInputs, int cbSize);
    
    private const int INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public InputUnion union;
    }
    
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}

