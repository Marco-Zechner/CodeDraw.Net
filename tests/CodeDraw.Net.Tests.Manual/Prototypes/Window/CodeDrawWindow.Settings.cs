using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Window;

public partial class CodeDrawWindow
{
    private WindowSettingsSnapshot _settings;
    private readonly Lock _settingsLock = new();

    private WindowSettingsSnapshot RawSettings
    {
        get { lock (_settingsLock) return _settings; }
    }
    
    public unsafe WindowSettingsSnapshot Settings
    {
        get
        {
            lock (_settingsLock)
            {
                var s = _settings;
                var cs = s.ClientSize;
                return s with { Size = cs };
            }
        }
        set
        {
            WindowSettingsSnapshot oldSnap;
            WindowSettingsSnapshot newSnap;

            lock (_settingsLock)
            {
                oldSnap = _settings;
                newSnap = value.Normalize();
                _settings = newSnap;
            }

            var dirty = oldSnap.ComputeDirty(newSnap);
            if (dirty == WindowDirty.None) return;

            var win = Win;
            if (win == null) return; // closed => just store snapshot; Open() will apply later

            _host.ApplyWindowSettingsSync(win, WindowId, newSnap, dirty);
        }
    }

    public Vector2<int> WindowPosition
    {
        get => Settings.WindowPosition;
        set
        {
            var raw = RawSettings;
            if (raw.State != WindowState.Windowed)
                Settings = raw with { State = WindowState.Windowed, WindowPosition = value };
            else
                Settings = raw with { WindowPosition = value };
        }
    }

    public Vector2<int> Size
    {
        get => Settings.Size;
        set
        {
            var raw = RawSettings;
            if (raw.State != WindowState.Windowed)
                Settings = raw with { State = WindowState.Windowed, Size = value };
            else
                Settings = raw with { Size = value };
        }
    }

    public int Width
    {
        get => Size.X;
        set => Size = Size.WithX(value);
    }

    public int Height
    {
        get => Size.Y;
        set => Size = Size.WithY(value);
    }

    public WindowState State
    {
        get => Settings.State;
        set => Settings = RawSettings with { State = value };
    }

    public WindowFrameMode FrameMode
    {
        get => Settings.FrameMode;
        set => Settings = RawSettings with { FrameMode = value };
    }

    public WindowResizeMode ResizeMode
    {
        get => Settings.ResizeMode;
        set => Settings = RawSettings with { ResizeMode = value };
    }

    public bool AlwaysOnTop
    {
        get => Settings.AlwaysOnTop;
        set => Settings = RawSettings with { AlwaysOnTop = value };
    }

    public bool ClickThrough
    {
        get => Settings.ClickThrough;
        set => Settings = RawSettings with { ClickThrough = value };
    }

    public bool TransparentAlpha
    {
        get => Settings.TransparentAlpha;
        set => Settings = RawSettings with { TransparentAlpha = value };
    }

    public string Title
    {
        get => Settings.Title;
        set => Settings = RawSettings with { Title = value };
    }
}