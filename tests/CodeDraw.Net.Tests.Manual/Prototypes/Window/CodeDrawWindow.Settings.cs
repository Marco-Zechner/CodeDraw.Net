using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Window;

public partial class CodeDrawWindow
{
    private WindowSettingsSnapshot _settings;
    private readonly Lock _settingsLock = new();

    public unsafe WindowSettingsSnapshot Settings
    {
        get { lock (_settingsLock) return _settings; }
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

            if (dirty != WindowDirty.None)
                _host.ApplyWindowSettingsSync(_win, WindowId, newSnap, dirty);
        }
    }

    public Vector2<int> WindowPosition
    {
        get { lock (_settingsLock) return _settings.WindowPosition; }
        set => Settings = Settings with { WindowPosition = value };
    }

    public Vector2<int> Size
    {
        get { lock (_settingsLock) return _settings.Size; }
        set => Settings = Settings with { Size = value };
    }

    public int Width
    {
        get { lock (_settingsLock) return _settings.Size.X; }
        set => Settings = Settings with { Size = Settings.Size.WithX(value) };
    }

    public int Height
    {
        get { lock (_settingsLock) return _settings.Size.Y; }
        set => Settings = Settings with { Size = Settings.Size.WithY(value) };
    }

    public WindowState State
    {
        get { lock (_settingsLock) return _settings.State; }
        set => Settings = Settings with { State = value };
    }

    public WindowFrameMode FrameMode
    {
        get { lock (_settingsLock) return _settings.FrameMode; }
        set => Settings = Settings with { FrameMode = value };
    }

    public WindowResizeMode ResizeMode
    {
        get { lock (_settingsLock) return _settings.ResizeMode; }
        set => Settings = Settings with { ResizeMode = value };
    }

    public bool AlwaysOnTop
    {
        get { lock (_settingsLock) return _settings.AlwaysOnTop; }
        set => Settings = Settings with { AlwaysOnTop = value };
    }

    public bool ClickThrough
    {
        get { lock (_settingsLock) return _settings.ClickThrough; }
        set => Settings = Settings with { ClickThrough = value };
    }

    public bool TransparentAlpha
    {
        get { lock (_settingsLock) return _settings.TransparentAlpha; }
        set => Settings = Settings with { TransparentAlpha = value };
    }

    public string Title
    {
        get { lock (_settingsLock) return _settings.Title; }
        set => Settings = Settings with { Title = value };
    }
}