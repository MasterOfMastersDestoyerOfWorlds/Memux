using System.Runtime.InteropServices;

namespace Memux.Actions;

/// <summary>
/// Simulates Xbox controller input using XInput
/// Note: For full virtual controller support, consider integrating ViGEm
/// This is a simplified version using XInput state setting where available
/// </summary>
public class ControllerInput
{
    private XINPUT_STATE _currentState = new();
    
    public void PressButton(string button, int durationMs = 50)
    {
        ushort buttonFlag = GetButtonFlag(button);
        
        // Button down
        _currentState.Gamepad.wButtons |= buttonFlag;
        ApplyState();
        
        // Hold
        if (durationMs > 0)
        {
            Thread.Sleep(durationMs);
        }
        
        // Button up
        _currentState.Gamepad.wButtons &= (ushort)~buttonFlag;
        ApplyState();
    }
    
    public void SetStick(string stick, float x, float y, int durationMs)
    {
        // Convert from -1.0 to 1.0 range to -32768 to 32767 range
        short xValue = (short)(x * 32767);
        short yValue = (short)(y * 32767);
        
        if (stick.ToUpper() == "LEFT")
        {
            _currentState.Gamepad.sThumbLX = xValue;
            _currentState.Gamepad.sThumbLY = yValue;
        }
        else if (stick.ToUpper() == "RIGHT")
        {
            _currentState.Gamepad.sThumbRX = xValue;
            _currentState.Gamepad.sThumbRY = yValue;
        }
        
        ApplyState();
        
        if (durationMs > 0)
        {
            Thread.Sleep(durationMs);
        }
        
        // Reset to center
        if (stick.ToUpper() == "LEFT")
        {
            _currentState.Gamepad.sThumbLX = 0;
            _currentState.Gamepad.sThumbLY = 0;
        }
        else if (stick.ToUpper() == "RIGHT")
        {
            _currentState.Gamepad.sThumbRX = 0;
            _currentState.Gamepad.sThumbRY = 0;
        }
        
        ApplyState();
    }
    
    public void SetTrigger(string trigger, float value, int durationMs)
    {
        // Convert from 0.0 to 1.0 range to 0 to 255 range
        byte triggerValue = (byte)(value * 255);
        
        if (trigger.ToUpper() == "LEFT" || trigger.ToUpper() == "LT")
        {
            _currentState.Gamepad.bLeftTrigger = triggerValue;
        }
        else if (trigger.ToUpper() == "RIGHT" || trigger.ToUpper() == "RT")
        {
            _currentState.Gamepad.bRightTrigger = triggerValue;
        }
        
        ApplyState();
        
        if (durationMs > 0)
        {
            Thread.Sleep(durationMs);
        }
        
        // Reset
        if (trigger.ToUpper() == "LEFT" || trigger.ToUpper() == "LT")
        {
            _currentState.Gamepad.bLeftTrigger = 0;
        }
        else if (trigger.ToUpper() == "RIGHT" || trigger.ToUpper() == "RT")
        {
            _currentState.Gamepad.bRightTrigger = 0;
        }
        
        ApplyState();
    }
    
    private void ApplyState()
    {
        // Note: XInput doesn't support setting controller state directly
        // This would require ViGEm or similar virtual bus driver
        // For now, this is a placeholder that tracks the state
        // TODO: Integrate ViGEm for actual controller simulation
        Console.WriteLine($"[ControllerInput] State: Buttons={_currentState.Gamepad.wButtons:X4}, " +
                         $"LX={_currentState.Gamepad.sThumbLX}, LY={_currentState.Gamepad.sThumbLY}");
    }
    
    private static ushort GetButtonFlag(string button)
    {
        return button.ToUpper() switch
        {
            "A" => XINPUT_GAMEPAD_A,
            "B" => XINPUT_GAMEPAD_B,
            "X" => XINPUT_GAMEPAD_X,
            "Y" => XINPUT_GAMEPAD_Y,
            "LB" or "LEFT_BUMPER" => XINPUT_GAMEPAD_LEFT_SHOULDER,
            "RB" or "RIGHT_BUMPER" => XINPUT_GAMEPAD_RIGHT_SHOULDER,
            "START" => XINPUT_GAMEPAD_START,
            "BACK" or "SELECT" => XINPUT_GAMEPAD_BACK,
            "LS" or "LEFT_STICK" => XINPUT_GAMEPAD_LEFT_THUMB,
            "RS" or "RIGHT_STICK" => XINPUT_GAMEPAD_RIGHT_THUMB,
            "DPAD_UP" => XINPUT_GAMEPAD_DPAD_UP,
            "DPAD_DOWN" => XINPUT_GAMEPAD_DPAD_DOWN,
            "DPAD_LEFT" => XINPUT_GAMEPAD_DPAD_LEFT,
            "DPAD_RIGHT" => XINPUT_GAMEPAD_DPAD_RIGHT,
            _ => throw new ArgumentException($"Unknown button: {button}")
        };
    }
    
    // XInput constants
    private const ushort XINPUT_GAMEPAD_DPAD_UP = 0x0001;
    private const ushort XINPUT_GAMEPAD_DPAD_DOWN = 0x0002;
    private const ushort XINPUT_GAMEPAD_DPAD_LEFT = 0x0004;
    private const ushort XINPUT_GAMEPAD_DPAD_RIGHT = 0x0008;
    private const ushort XINPUT_GAMEPAD_START = 0x0010;
    private const ushort XINPUT_GAMEPAD_BACK = 0x0020;
    private const ushort XINPUT_GAMEPAD_LEFT_THUMB = 0x0040;
    private const ushort XINPUT_GAMEPAD_RIGHT_THUMB = 0x0080;
    private const ushort XINPUT_GAMEPAD_LEFT_SHOULDER = 0x0100;
    private const ushort XINPUT_GAMEPAD_RIGHT_SHOULDER = 0x0200;
    private const ushort XINPUT_GAMEPAD_A = 0x1000;
    private const ushort XINPUT_GAMEPAD_B = 0x2000;
    private const ushort XINPUT_GAMEPAD_X = 0x4000;
    private const ushort XINPUT_GAMEPAD_Y = 0x8000;
    
    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }
}

