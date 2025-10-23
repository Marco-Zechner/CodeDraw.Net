using System.Runtime.InteropServices;
using MarcoZechner.Math;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet;

public unsafe class Input
{
    private readonly GLFWWindow _window;
    internal Input(GLFWWindow window)
    {
        _window = window;

        var glfw = SharedGlManager.Instance.Glfw;

        glfw.SetInputMode(_window.WindowHandle, (StickyAttributes)0x00033004, true);
        glfw.SetKeyCallback(_window.WindowHandle, HandleKeyCallback);
        glfw.SetCharCallback(_window.WindowHandle, HandleCharCallback);
        glfw.SetCharModsCallback(_window.WindowHandle, HandleCharModCallback);
        glfw.SetScrollCallback(_window.WindowHandle, HandleScrollCallback);
        glfw.SetCursorPosCallback(_window.WindowHandle, HandleCursorPosCallback);
        glfw.SetCursorEnterCallback(_window.WindowHandle, HandleCursorEnterCallback);
        glfw.SetMouseButtonCallback(_window.WindowHandle, HandleMouseButtonCallback);
        glfw.SetJoystickCallback(HandleJoystickCallback);
        glfw.SetDropCallback(_window.WindowHandle, HandleFileDropCallback);
    }

    private readonly HashSet<Keys> _heldKeys = [];
    private readonly HashSet<MouseButton> _heldMouseButtons = [];

    public event Action<Keys>? OnKeyDown;
    public event Action<Keys>? OnKeyUp;
    public event Action<Keys>? OnKey;
    public event Action<char>? OnChar;
    public event Action<char, KeyModifiers>? OnCharMod;
    public event Action<Vector2<double>>? OnScroll;
    public event Action<Vector2<double>>? OnCursorPos;
    public event Action? OnCursorEnter;
    public event Action? OncursorLeave;
    public event Action<MouseButton>? OnMouseButtonDown;
    public event Action<MouseButton>? OnMouseButtonUp;
    public event Action<MouseButton>? OnMouseButton;
    public event Action<int, string[]>? OnFileDrop;

    public bool GetKey(Keys key) => _heldKeys.Contains(key);
    public bool GetKeyDown(Keys key)
    {
        bool down = _framePressedKeys.Contains(key);
        return down;
    }

    public bool IsMouseButtonDown(MouseButton button) => _heldMouseButtons.Contains(button);
    public Vector2<double> GetCursorPos()
    {
        SharedGlManager.Instance.Glfw.GetCursorPos(_window.WindowHandle, out double x, out double y);
        return new Vector2<double>(x, y);
    }

    public void ResetFrameInputState()
    {
        // Logger.LogLine("\t\tResetFrameInputState()");
        _framePressedKeys.Clear();
        _frameReleasedKeys.Clear();
    }

    private readonly HashSet<Keys> _framePressedKeys = [];
    private readonly HashSet<Keys> _frameReleasedKeys = [];

    #region Keyboard

    private void HandleKeyCallback(WindowHandle* windowHandle, Keys key, int scancode, InputAction inputAction, KeyModifiers keyModifiers)
    {
        switch (inputAction)
        {
            case InputAction.Repeat:
                // Console.WriteLine($"OnKey\n\tkeyModBit:\t{Convert.ToString((int)keyModifiers, 2).PadLeft(8, '0')}\n\tkeyMod\t\t{keyModifiers}\n\tKeys:");
                foreach (var heldKey in _heldKeys)
                {
                    // Console.WriteLine($"\t\t\t{heldKey}");
                    OnKey?.Invoke(heldKey);
                }
                break;

            case InputAction.Release:
                // Console.WriteLine($"OnKeyUp\n\tkeyModBit:\t{Convert.ToString((int)keyModifiers, 2).PadLeft(8, '0')}\n\tkeyMod\t\t{keyModifiers}\n\tKey:\t\t{key}");
                OnKeyUp?.Invoke(key);
                _heldKeys.Remove(key);
                _frameReleasedKeys.Add(key);
                break;

            case InputAction.Press:
                // Console.WriteLine($"OnKeyDown\n\tkeyModBit:\t{Convert.ToString((int)keyModifiers, 2).PadLeft(8, '0')}\n\tkeyMod\t\t{keyModifiers}\n\tKey:\t\t{key}");
                // Logger.LogLine($"\tPressed: {key}");
                OnKeyDown?.Invoke(key);
                _heldKeys.Add(key);
                _framePressedKeys.Add(key);
                break;
        }
    }

    internal void ClearHoldKeys()
    {
        _heldKeys.Clear();
        _heldMouseButtons.Clear();
    }

    private void HandleCharCallback(WindowHandle* windowHandle, uint codepoint)
    {
        // Console.WriteLine($"OnChar\n\tChar:\t\t{(char)codepoint}\n\tCodepoint:\t{codepoint}");
        OnChar?.Invoke((char)codepoint);
    }

    private void HandleCharModCallback(WindowHandle* windowHandle, uint codepoint, KeyModifiers keyModifiers)
    {
        // Console.WriteLine($"OnCharMod\n\tChar:\t\t{(char)codepoint}\n\tCodepoint:\t{codepoint}\n\tkeyModBit:\t{Convert.ToString((int)keyModifiers, 2).PadLeft(8, '0')}\n\tkeyMod\t\t{keyModifiers}");
        OnCharMod?.Invoke((char)codepoint, keyModifiers);
    }

    #endregion

    #region Pointer (e.g. Mouse)

    private void HandleScrollCallback(WindowHandle* windowHandle, double xOffset, double yOffset)
    {
        // Console.WriteLine("Scroll:\t" + new Vector2<double>(xOffset, yOffset));
        OnScroll?.Invoke(new Vector2<double>(xOffset, yOffset));
    }

    private void HandleCursorPosCallback(WindowHandle* window, double x, double y)
    {
        // Console.WriteLine("CursorPos:\t" + new Vector2<double>(x, y));
        OnCursorPos?.Invoke(new Vector2<double>(x, y));
    }

    private void HandleCursorEnterCallback(WindowHandle* window, bool entered)
    {
        if (entered)
        {
            // Console.WriteLine("Cursor entered Window");
            OnCursorEnter?.Invoke();
        }
        else
        {
            // Console.WriteLine("Cursor left Window");
            OncursorLeave?.Invoke();
        }
    }

    private void HandleMouseButtonCallback(WindowHandle* window, MouseButton button, InputAction inputAction, KeyModifiers keyModifiers)
    {
        switch (inputAction)
        {
            case InputAction.Repeat:
                // Console.WriteLine($"OnMouseButton\n\tkeyModBit:\t{Convert.ToString((int)keyModifiers, 2).PadLeft(8, '0')}\n\tkeyMod\t\t{keyModifiers}\n\tMouseButtons:");
                foreach (var heldMouseButton in _heldMouseButtons)
                {
                    Console.WriteLine($"\t\t\t{heldMouseButton}");
                    OnMouseButton?.Invoke(heldMouseButton);
                }
                break;

            case InputAction.Release:
                // Console.WriteLine($"OnMouseButtonUp\n\tkeyModBit:\t{Convert.ToString((int)keyModifiers, 2).PadLeft(8, '0')}\n\tkeyMod\t\t{keyModifiers}\n\tMouseButton:\t{button}");
                OnMouseButtonUp?.Invoke(button);
                _heldMouseButtons.Remove(button);
                break;

            case InputAction.Press:
                // Console.WriteLine($"OnMouseButtonDown\n\tkeyModBit:\t{Convert.ToString((int)keyModifiers, 2).PadLeft(8, '0')}\n\tkeyMod\t\t{keyModifiers}\n\tMouseButton:\t{button}");
                OnMouseButtonDown?.Invoke(button);
                _heldMouseButtons.Add(button);
                break;
        }
    }

    #endregion

    #region Joystick & Controller

    private void HandleJoystickCallback(int joystick, ConnectedState state)
    {
        // Console.WriteLine($"JoystickCallback:\n\tJoystick:\t{joystick}\n\tState:\t{state}");
    }

    #endregion

    #region Other

    private void HandleFileDropCallback(WindowHandle* window, int count, nint paths)
    {
        string[] managedPaths = new string[count];
        for (int i = 0; i < count; i++)
        {
            nint strPtr = Marshal.ReadIntPtr(paths, i * IntPtr.Size);

            managedPaths[i] = Marshal.PtrToStringUTF8(strPtr)!;
        }
        OnFileDrop?.Invoke(count, managedPaths);
    }

    #endregion
}