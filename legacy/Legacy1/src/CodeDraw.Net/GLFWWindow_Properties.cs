using MarcoZechner.MathDotNet;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet;

public unsafe partial class GlfwWindow
{
    #region Window Settings
    /// <summary>
    /// If true, the window will automatically swap buffers after each render call.
    /// </summary>
    public bool AutoRender { get; set; } = true;
    /// <summary>
    /// If true, the window will clear the last frame before drawing the next with the set clearcolor
    /// Shapes drawn via cd.Shapes.xyz will persist until cd.Clear is called.
    /// </summary>
    public bool AutoClear { get; set; } = true;
    public int TargetFramerate { get; set; } = 60;
    public double TargetFrameTime => TargetFramerate > 0 ? 1000.0 / TargetFramerate : 0;
    private string _title;
    public string Title
    {
        get
        {
            return _title;
        }
        set
        {
            _mgr.Glfw.SetWindowTitle(_windowHandle, value); // not on render thread
            _title = value;
        }
    }

    public bool Decorated
    {
        get
        {
            return _mgr.Glfw.GetWindowAttrib(_windowHandle, WindowAttributeGetter.Decorated);
        }
        set
        {
            _mgr.Glfw.SetWindowAttrib(_windowHandle, WindowAttributeSetter.Decorated, value);  // not on render thread
        }
    }

    public bool AlwaysOnTop
    {
        get
        {
            return _mgr.Glfw.GetWindowAttrib(_windowHandle, WindowAttributeGetter.Floating);
        }
        set
        {
            _mgr.Glfw.SetWindowAttrib(_windowHandle, WindowAttributeSetter.Floating, value);  // not on render thread
        }
    }

    public Vector2<int> Position
    {
        get
        {
            _mgr.Glfw.GetWindowPos(_windowHandle, out int x, out int y);
            return new Vector2<int>(x, y);
        }
        set
        {
            _mgr.Glfw.SetWindowPos(_windowHandle, value.X, value.Y);
        }
    }
    public Vector2<int> Size
    {
        get
        {
            _mgr.Glfw.GetWindowSize(_windowHandle, out int w, out int h);
            return new Vector2<int>(w, h);
        }
        set
        {

            _mgr.Glfw.SetWindowSize(_windowHandle, value.X, value.Y);  // not on render thread
        }
    }

    public float AspectRatio
    {
        get
        {
            var size = Size;
            return (float)size.X / size.Y;
        }
    }

    public bool Resizable
    {
        get
        {
            return _mgr.Glfw.GetWindowAttrib(_windowHandle, WindowAttributeGetter.Resizable);
        }
        set
        {
            _mgr.Glfw.SetWindowAttrib(_windowHandle, WindowAttributeSetter.Resizable, value);  // not on render thread
        }
    }

    public string? Clipboard
    {
        get
        {
            var str = _mgr.Glfw.GetClipboardString(_windowHandle);
            return str;
        }
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value), "Clipboard value cannot be set to null.");
            _mgr.Glfw.SetClipboardString(_windowHandle, value.ToString());
        }
    }

    public long FrameCount { private set; get; } = 0;

    #endregion

}