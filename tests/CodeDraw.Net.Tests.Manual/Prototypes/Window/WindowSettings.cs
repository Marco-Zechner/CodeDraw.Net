using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Window;

[Flags]
internal enum WindowDirty : uint
{
    None             = 0,
    Title            = 1u << 0,
    WindowPos        = 1u << 1,
    CanvasSize       = 1u << 2,
    Border           = 1u << 3,
    AlwaysOnTop      = 1u << 4,
    WindowState      = 1u << 5,
    ClickThrough     = 1u << 6,
    TransparentAlpha = 1u << 7,
}

public enum WindowBorder { Resizable, Fixed, Hidden }
public enum WindowState  { Normal, Minimized, Maximized, Fullscreen }

/// <summary>
/// Snapshot (value type) users can reuse.
/// </summary>
public readonly record struct WindowSettingsSnapshot(
    Vector2 WindowPosition,
    Vector2 Size,           // == CanvasSize (your decision)
    string  Title,
    bool    AlwaysOnTop,
    WindowBorder Border,
    WindowState  State,
    bool    ClickThrough,
    bool    TransparentAlpha
);

/// <summary>
/// Mutable handle that routes writes into a window-specific Desired state and dirty flags.
/// Reads always return Current state (reflects manual move/resize).
/// </summary>
public sealed class WindowSettingsHandle
{
    private readonly object _lock = new();

    private WindowSettingsSnapshot _current;
    private WindowSettingsSnapshot _desired;

    // pending dirty indicates user-requested changes that are not yet applied
    private WindowDirty _pending;
    private WindowDirty _dirtyToApply;

    internal WindowSettingsHandle(WindowSettingsSnapshot initial)
    {
        _current = initial;
        _desired = initial;
        _pending = WindowDirty.None;
        _dirtyToApply = WindowDirty.None;
    }

    // What user expects: return desired.
    public Vector2 WindowPosition
    {
        get { lock (_lock) return _desired.WindowPosition; }
        set { lock (_lock) { _desired = _desired with { WindowPosition = value }; Mark(WindowDirty.WindowPos); } }
    }

    public Vector2 Size
    {
        get { lock (_lock) return _desired.Size; }
        set { lock (_lock) { _desired = _desired with { Size = value }; Mark(WindowDirty.CanvasSize); } }
    }

    public string Title
    {
        get { lock (_lock) return _desired.Title; }
        set { lock (_lock) { _desired = _desired with { Title = value ?? "" }; Mark(WindowDirty.Title); } }
    }

    public bool AlwaysOnTop
    {
        get { lock (_lock) return _desired.AlwaysOnTop; }
        set { lock (_lock) { _desired = _desired with { AlwaysOnTop = value }; Mark(WindowDirty.AlwaysOnTop); } }
    }

    public WindowBorder Border
    {
        get { lock (_lock) return _desired.Border; }
        set { lock (_lock) { _desired = _desired with { Border = value }; Mark(WindowDirty.Border); } }
    }

    public WindowState State
    {
        get { lock (_lock) return _desired.State; }
        set { lock (_lock) { _desired = _desired with { State = value }; Mark(WindowDirty.WindowState); } }
    }

    public bool ClickThrough
    {
        get { lock (_lock) return _desired.ClickThrough; }
        set { lock (_lock) { _desired = _desired with { ClickThrough = value }; Mark(WindowDirty.ClickThrough); } }
    }

    public bool TransparentAlpha
    {
        get { lock (_lock) return _desired.TransparentAlpha; }
        set { lock (_lock) { _desired = _desired with { TransparentAlpha = value }; Mark(WindowDirty.TransparentAlpha); } }
    }

    public WindowSettingsSnapshot DesiredSnapshot()
    {
        lock (_lock) return _desired;
    }

    public WindowSettingsSnapshot CurrentSnapshot()
    {
        lock (_lock) return _current;
    }

    // called by render/update loops to fetch work
    internal (WindowSettingsSnapshot desired, WindowDirty dirty) ConsumeDirty()
    {
        lock (_lock)
        {
            var d = _dirtyToApply;
            var s = _desired;
            _dirtyToApply = WindowDirty.None;
            return (s, d);
        }
    }

    // called by host events (manual move/resize) to update current and maybe desired
    internal void UpdateFromOs(
        Vector2? newWindowPos = null,
        Vector2? newSize      = null)
    {
        lock (_lock)
        {
            var cur = _current;

            if (newWindowPos is { } p)
                cur = cur with { WindowPosition = p };

            if (newSize is { } sz)
                cur = cur with { Size = sz };

            _current = cur;

            // Manual change should behave like "user set it" in practice.
            // But do NOT clobber desired if there is a pending code-set for that field.
            if (newWindowPos is { } p2 && (_pending & WindowDirty.WindowPos) == 0)
                _desired = _desired with { WindowPosition = p2 };

            if (newSize is { } sz2 && (_pending & WindowDirty.CanvasSize) == 0)
                _desired = _desired with { Size = sz2 };
        }
    }

    // when host confirms apply completed, clear pending bits for those fields
    internal void MarkApplied(WindowDirty applied)
    {
        lock (_lock)
        {
            _pending &= ~applied;
        }
    }

    private void Mark(WindowDirty bit)
    {
        _dirtyToApply |= bit;
        _pending |= bit;
    }
}