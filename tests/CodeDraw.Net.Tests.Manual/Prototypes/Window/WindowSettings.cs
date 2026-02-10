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
    SizeLimits       = 1u << 8,
}

public enum WindowBorder{
    Resizable,   // resizable, no limits
    Limited,  // resizable + min/max limits
    Fixed,    // resizable=false (no limits)
    Hidden,   // decorated=false
}
public enum WindowState  { Normal, Minimized, Maximized, Fullscreen }

/// <summary>
/// Snapshot (value type) users can reuse.
/// </summary>
public readonly record struct WindowSettingsSnapshot(
    Vector2 WindowPosition,
    Vector2 Size,
    Vector2 MinSize,
    Vector2 MaxSize,
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
    private readonly Lock _lock = new();

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
    public Vector2 MinSize
    {
        get { lock (_lock) return _desired.MinSize; }
        set
        {
            lock (_lock)
            {
                var s = _desired with { MinSize = SanitizeSize(value) };
                s = FixLimitsAndMaybeClamp(s, clampSizeIfLimited: true);

                _desired = s;
                Mark(WindowDirty.SizeLimits);

                // If clamping changed Size, we must also push CanvasSize
                // (FixLimitsAndMaybeClamp already adjusted Size)
                MarkIfSizeChangedComparedToCurrentDesired();
            }
        }
    }

    public Vector2 MaxSize
    {
        get { lock (_lock) return _desired.MaxSize; }
        set
        {
            lock (_lock)
            {
                var s = _desired with { MaxSize = SanitizeSize(value) };
                s = FixLimitsAndMaybeClamp(s, clampSizeIfLimited: true);

                _desired = s;
                Mark(WindowDirty.SizeLimits);
                MarkIfSizeChangedComparedToCurrentDesired();
            }
        }
    }

    public Vector2 WindowPosition
    {
        get { lock (_lock) return _desired.WindowPosition; }
        set { lock (_lock) { _desired = _desired with { WindowPosition = value }; Mark(WindowDirty.WindowPos); } }
    }

    public Vector2 Size
    {
        get { lock (_lock) return _desired.Size; }
        set
        {
            lock (_lock)
            {
                var s = _desired with { Size = value };

                // If Limited, clamp requested size immediately so the API feels deterministic.
                if (s.Border == WindowBorder.Limited)
                    s = ClampSizeToLimits(s);

                _desired = s;
                Mark(WindowDirty.CanvasSize);
            }
        }
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
        set
        {
            lock (_lock)
            {
                var before = _desired;
                var after = _desired with { Border = value };

                // If switching into Limited, enforce valid limits + clamp size
                // If switching out of Limited, keep min/max stored but don’t clamp size.
                after = FixLimitsAndMaybeClamp(after, clampSizeIfLimited: value == WindowBorder.Limited);

                _desired = after;
                Mark(WindowDirty.Border);

                // Switching into Limited may clamp Size
                if (value == WindowBorder.Limited && after.Size != before.Size)
                    Mark(WindowDirty.CanvasSize);
            }
        }
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

    private static Vector2 SanitizeSize(Vector2 v)
        => new Vector2(MathF.Max(1, v.X), MathF.Max(1, v.Y));

    private static WindowSettingsSnapshot FixLimitsAndMaybeClamp(WindowSettingsSnapshot s, bool clampSizeIfLimited)
    {
        var min = SanitizeSize(s.MinSize);
        var max = SanitizeSize(s.MaxSize);

        // Ensure min <= max component-wise
        if (min.X > max.X) max = max.WithX(min.X);
        if (min.Y > max.Y) max = max.WithY(min.Y);

        // Also if max < min (when setting max), bring min down? No: your rule says
        // “if min>max, change max and vice versa.”
        // We’ll implement that symmetrically by also fixing min if max was made smaller:
        if (max.X < min.X) min = min.WithX(max.X);
        if (max.Y < min.Y) min = min.WithY(max.Y);

        s = s with { MinSize = min, MaxSize = max };

        if (clampSizeIfLimited && s.Border == WindowBorder.Limited)
            s = ClampSizeToLimits(s);

        return s;
    }

    private static WindowSettingsSnapshot ClampSizeToLimits(WindowSettingsSnapshot s)
    {
        var clamped = new Vector2(
            MathF.Min(MathF.Max(s.Size.X, s.MinSize.X), s.MaxSize.X),
            MathF.Min(MathF.Max(s.Size.Y, s.MinSize.Y), s.MaxSize.Y)
        );
        return clamped == s.Size ? s : s with { Size = clamped };
    }

    private void MarkIfSizeChangedComparedToCurrentDesired()
    {
        // We’re inside lock(_lock).
        // If size is outside limits and got clamped, we must push size change.
        // Easiest safe behavior: if Border == Limited, always ensure CanvasSize dirty.
        if (_desired.Border == WindowBorder.Limited)
            _dirtyToApply |= WindowDirty.CanvasSize;
    }

    private void Mark(WindowDirty bit)
    {
        _dirtyToApply |= bit;
        _pending |= bit;
    }
}