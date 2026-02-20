using MarcoZechner.CodeDrawDotNet.Window;
using MarcoZechner.MathDotNet;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet;

public sealed class HostInputHub
{
    private readonly SharedGlfwHost _host;

    internal HostInputHub(SharedGlfwHost host)
    {
        _host = host;
    }
    
    public event Action<CodeDrawWindow, Keys, ModifierKeys>? OnKeyDown;
    public event Action<CodeDrawWindow, Keys, ModifierKeys>? OnKeyUp;
    public event Action<CodeDrawWindow, Keys, ModifierKeys>? OnKeyRepeat;

    public event Action<CodeDrawWindow, MouseButton, ModifierKeys>? OnMouseDown;
    public event Action<CodeDrawWindow, MouseButton, ModifierKeys>? OnMouseUp;

    public event Action<CodeDrawWindow, double, double>? OnScroll;
    public event Action<CodeDrawWindow, double, double>? OnMouseMove;

    internal void Dispatch(CodeDrawWindow win, SharedGlfwHost.HostInputEvent e)
    {
        switch (e)
        {
            case SharedGlfwHost.HostKeyEvent ke:
                switch (ke.Action)
                {
                    case InputAction.Press: 
                        OnKeyDown?.Invoke(win, ke.Key, ke.Mods); 
                        OnKeyRepeat?.Invoke(win, ke.Key, ke.Mods);
                        break;
                    case InputAction.Release: 
                        OnKeyRepeat?.Invoke(win, ke.Key, ke.Mods);
                        OnKeyUp?.Invoke(win, ke.Key, ke.Mods); 
                        break;
                    case InputAction.Repeat: OnKeyRepeat?.Invoke(win, ke.Key, ke.Mods); break; //TODO: check why this is reacting with a delay...
                }
                break;

            case SharedGlfwHost.HostMouseButtonEvent mb:
                if (mb.Action == InputAction.Press) OnMouseDown?.Invoke(win, mb.Button, mb.Mods);
                else if (mb.Action == InputAction.Release) OnMouseUp?.Invoke(win, mb.Button, mb.Mods);
                break;

            case SharedGlfwHost.HostScrollEvent sc:
                OnScroll?.Invoke(win, sc.Dx, sc.Dy);
                break;

            case SharedGlfwHost.HostCursorPosEvent mv:
                OnMouseMove?.Invoke(win, mv.X, mv.Y);
                break;
        }
    }

    public unsafe Vector2<double> GetAbsoluteMousePosition()
    {
        double cx = 0, cy = 0;
        int wx = 0, wy = 0;

        _host.InvokeHostSync(() =>
        {
            LockedGlfw.GetCursorPos(_host.ShareRoot, out cx, out cy);
            LockedGlfw.GetWindowPos(_host.ShareRoot, out wx, out wy);
        });

        return new Vector2<double>(wx + cx, wy + cy);
    }
}