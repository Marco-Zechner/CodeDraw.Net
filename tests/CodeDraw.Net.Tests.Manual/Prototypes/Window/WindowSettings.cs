using MarcoZechner.MathDotNet;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Window;

[Flags]
internal enum WindowDirty : uint
{
    None             = 0,
    Title            = 1u << 0,
    WindowPos        = 1u << 1,
    CanvasSize       = 1u << 2,
    Border           = 1u << 3, // includes resizable/limited/aspect/fixed/hidden
    AlwaysOnTop      = 1u << 4,
    WindowState      = 1u << 5,
    ClickThrough     = 1u << 6,
    TransparentAlpha = 1u << 7,
}

public enum WindowState  { Windowed, Minimized, Maximized, Fullscreen }
public enum WindowFrameMode  { Decorated, Hidden }
public enum WindowResizeMode{
    Resizable,// resizable, no constraints
    Limited,  // resizable, size limits active
    Aspect,   // resizable, aspect ratio active
    Fixed,    // not resizable, no constraints (for code)
}


/// <summary>
/// Snapshot (value type) users can reuse.
/// </summary>
public readonly record struct WindowSettingsSnapshot(
    Vector2<int> WindowPosition,
    Vector2<int> Size,
    string Title,
    bool AlwaysOnTop,

    WindowFrameMode FrameMode,
    WindowResizeMode ResizeMode,

    Vector2<int> MinSize, // -1 or 0 means DontCare, otherwise must be >= 1
    Vector2<int> MaxSize, // -1 or 0 means DontCare, otherwise must be >= 1; if both min and max are set, must be min <= max
    Vector2<int> AspectRatio, // (numer, denom), only meaningful in Aspect; either one at -1 or 0 means DontCare

    WindowState State,
    bool ClickThrough,
    bool TransparentAlpha
)
{
    public override string ToString()
    {
        return $"WindowSettingsSnapshot(\n  WindowPosition={WindowPosition},\n  Size={Size},\n  Title=\"{Title}\",\n  AlwaysOnTop={AlwaysOnTop},\n  BorderMode={ResizeMode},\n  MinSize={MinSize},\n  MaxSize={MaxSize},\n  AspectRatio={AspectRatio},\n  State={State}, \n  Frame={FrameMode},\n  ClickThrough={ClickThrough},\n  TransparentAlpha={TransparentAlpha}\n)";
    }
}

internal static class WindowSettingsSnapshotExtensions
{
    internal static WindowSettingsSnapshot Normalize(this WindowSettingsSnapshot d)
    {
        // Fullscreen hard-forces: Hidden + Fixed
        if (d.State == WindowState.Fullscreen)
        {
            d = d with
            {
                FrameMode = WindowFrameMode.Hidden,
                ResizeMode = WindowResizeMode.Fixed,
                MinSize = new Vector2<int>(Glfw.DontCare, Glfw.DontCare),
                MaxSize = new Vector2<int>(Glfw.DontCare, Glfw.DontCare),
                AspectRatio = new Vector2<int>(Glfw.DontCare, Glfw.DontCare),
            };
            return d;
        }

        // Maximized hard-forces: Fixed (Frame can be either)
        if (d.State == WindowState.Maximized)
        {
            d = d with
            {
                ResizeMode = WindowResizeMode.Fixed,
                MinSize = new Vector2<int>(Glfw.DontCare, Glfw.DontCare),
                MaxSize = new Vector2<int>(Glfw.DontCare, Glfw.DontCare),
                AspectRatio = new Vector2<int>(Glfw.DontCare, Glfw.DontCare),
            };
            return d;
        }

        // Windowed: everything allowed, but keep constraints consistent:
        // - Fixed should clear constraints
        // - Resizable should clear constraints
        // - Limited uses Min/Max, clears aspect
        // - Aspect uses AspectRatio, clears size limits
        switch (d.ResizeMode)
        {
            case WindowResizeMode.Fixed:
            case WindowResizeMode.Resizable:
                d = d with
                {
                    MinSize = new Vector2<int>(Glfw.DontCare, Glfw.DontCare),
                    MaxSize = new Vector2<int>(Glfw.DontCare, Glfw.DontCare),
                    AspectRatio = new Vector2<int>(Glfw.DontCare, Glfw.DontCare),
                };
                break;

            case WindowResizeMode.Limited:
                d = d with { AspectRatio = new Vector2<int>(Glfw.DontCare, Glfw.DontCare) };
                break;

            case WindowResizeMode.Aspect:
                d = d with
                {
                    MinSize = new Vector2<int>(Glfw.DontCare, Glfw.DontCare),
                    MaxSize = new Vector2<int>(Glfw.DontCare, Glfw.DontCare)
                };
                break;
        }

        return d;
    }
}



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
    public Vector2<int> WindowPosition
    {
        get { lock (_lock) return _desired.WindowPosition; }
        set { lock (_lock) { _desired = _desired with { WindowPosition = value }; Mark(WindowDirty.WindowPos); } }
    }

    public Vector2<int> Size
    {
        get { lock (_lock) return _desired.Size; }
        set
        {
            lock (_lock)
            {
                var newSize = value;
                var oldSize = _desired.Size;

                var (numer, denom) = _desired.AspectRatio;

                if (_desired.ResizeMode != WindowResizeMode.Aspect || numer <= 0 || denom <= 0)
                {
                    if (_desired is { ResizeMode: WindowResizeMode.Limited, State: WindowState.Windowed })
                    {
                        newSize = ClampToLimits(newSize, _desired.MinSize, _desired.MaxSize);
                    }

                    _desired = _desired with { Size = newSize };
                    Mark(WindowDirty.CanvasSize);
                    return;
                }

                var xChanged = newSize.X != oldSize.X;
                var yChanged = newSize.Y != oldSize.Y;

                if (xChanged && !yChanged)
                {
                    newSize = new Vector2<int>(
                        newSize.X,
                        Math.Max(1, (int)Math.Round(newSize.X * (double)denom / numer))
                    );
                }
                else if (!xChanged && yChanged)
                {
                    newSize = new Vector2<int>(
                        Math.Max(1, (int)Math.Round(newSize.Y * (double)numer / denom)),
                        newSize.Y
                    );
                }
                else if (xChanged && yChanged)
                {
                    var cand1 = new Vector2<int>(
                        newSize.X,
                        Math.Max(1, (int)Math.Round(newSize.X * (double)denom / numer))
                    );
                    var cand2 = new Vector2<int>(
                        Math.Max(1, (int)Math.Round(newSize.Y * (double)numer / denom)),
                        newSize.Y
                    );

                    var err1 = Math.Abs(cand1.Y - newSize.Y);
                    var err2 = Math.Abs(cand2.X - newSize.X);

                    newSize = err1 <= err2 ? cand1 : cand2;
                }

                _desired = _desired with { Size = newSize };
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

    public WindowFrameMode FrameMode
    {
        get { lock (_lock) return _desired.FrameMode; }
        set
        {
            lock (_lock)
            {
                var old = _desired;
                _desired = _desired with { FrameMode = value };
                Mark(WindowDirty.Border);

                if (value != WindowFrameMode.Hidden || old.State != WindowState.Windowed) return;
                var clamped = ClampToLimits(_desired.Size, _desired.MinSize, _desired.MaxSize);
                if (clamped == _desired.Size) return;
                _desired = _desired with { Size = clamped };
                Mark(WindowDirty.CanvasSize);
            }
        }
    }

    public WindowResizeMode ResizeMode
    {
        get { lock (_lock) return _desired.ResizeMode; }
        set
        {
            lock (_lock)
            {
                var old = _desired;
                _desired = _desired with { ResizeMode = value };
                Mark(WindowDirty.Border);

                if (value != WindowResizeMode.Limited || old.State != WindowState.Windowed) return;
                var clamped = ClampToLimits(_desired.Size, _desired.MinSize, _desired.MaxSize);
                if (clamped == _desired.Size) return;
                _desired = _desired with { Size = clamped };
                Mark(WindowDirty.CanvasSize);
            }
        }
    }

    public Vector2<int> MinSize
    {
        get { lock (_lock) return _desired.MinSize; }
        set
        {
            lock (_lock)
            {
                var min = EnsureDontCare(value);
                var max = _desired.MaxSize;
                // if min > max => push max up
                if (min.X > max.X) max = new Vector2<int>(min.X, max.Y);
                if (min.Y > max.Y) max = new Vector2<int>(max.X, min.Y);

                max = EnsureDontCare(max);

                _desired = _desired with { MinSize = min, MaxSize = max };
                Mark(WindowDirty.Border);
            }
        }
    }

    public Vector2<int> MaxSize
    {
        get { lock (_lock) return _desired.MaxSize; }
        set
        {
            lock (_lock)
            {
                var max = EnsureDontCare(value);
                var min = _desired.MinSize;
                // if max < min => pull min down
                if (max.X < min.X) min = new Vector2<int>(max.X, min.Y);
                if (max.Y < min.Y) min = new Vector2<int>(min.X, max.Y);

                min = EnsureDontCare(min);

                _desired = _desired with { MinSize = min, MaxSize = max };
                Mark(WindowDirty.Border);

                if (_desired.ResizeMode != WindowResizeMode.Limited || _desired.State != WindowState.Windowed) return;

                var clamped = ClampToLimits(_desired.Size, _desired.MinSize, _desired.MaxSize);
                if (clamped == _desired.Size) return;

                _desired = _desired with { Size = clamped };
                Mark(WindowDirty.CanvasSize);
            }
        }
    }

    public Vector2<int> AspectRatio
    {
        get { lock (_lock) return _desired.AspectRatio; }
        set
        {
            lock (_lock)
            {
                var aspect = EnsureDontCare(value);

                _desired = _desired with { AspectRatio = aspect };
                Mark(WindowDirty.Border);

                if (_desired.ResizeMode != WindowResizeMode.Limited || _desired.State != WindowState.Windowed) return;

                var clamped = ClampToLimits(_desired.Size, _desired.MinSize, _desired.MaxSize);
                if (clamped == _desired.Size) return;

                _desired = _desired with { Size = clamped };
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

    private static int ClampDontCareMax(int v, int max) => max <= 0 ? v : Math.Min(v, max);
    private static int ClampDontCareMin(int v, int min) => min <= 0 ? v : Math.Max(v, min);

    private static Vector2<int> ClampToLimits(Vector2<int> size, Vector2<int> min, Vector2<int> max)
    {
        var x = size.X;
        var y = size.Y;

        x = ClampDontCareMin(x, min.X);
        y = ClampDontCareMin(y, min.Y);
        x = ClampDontCareMax(x, max.X);
        y = ClampDontCareMax(y, max.Y);

        // ensure sane
        x = Math.Max(1, x);
        y = Math.Max(1, y);

        return new Vector2<int>(x, y);
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
    internal void UpdateFromOs(Vector2<int>? newWindowPos = null, Vector2<int>? newSize = null)
    {
        lock (_lock)
        {
            var cur = _current;

            if (newWindowPos is { } p)
            {
                cur = cur with { WindowPosition = p };

                if ((_pending & WindowDirty.WindowPos) != 0)
                {
                    // confirm -> OS is truth
                    _desired = _desired with { WindowPosition = p };
                    _pending &= ~WindowDirty.WindowPos;
                }
                else
                {
                    _desired = _desired with { WindowPosition = p };
                }
            }

            if (newSize is { } s)
            {
                if (s.X <= 0 || s.Y <= 0)
                    return; // ignore minimize / transient 0 events

                cur = cur with { Size = s };

                if ((_pending & WindowDirty.CanvasSize) != 0)
                {
                    // confirm -> OS is truth (also fixes clamp/back-jump cases)
                    _desired = _desired with { Size = s };
                    _pending &= ~WindowDirty.CanvasSize;
                }
                else
                {
                    _desired = _desired with { Size = s };
                }
            }

            _current = cur;
        }
    }

    internal void SyncSizeFromHost(Vector2<int> actualSize)
    {
        lock (_lock)
        {
            // update both
            _current = _current with { Size = actualSize };
            _desired = _desired with { Size = actualSize };

            // clear pending for size so it doesn't block future OS updates
            _pending &= ~WindowDirty.CanvasSize;
        }
    }

    internal void SyncCurrentFromHost(WindowSettingsSnapshot applied, WindowDirty appliedBits)
    {
        lock (_lock)
        {
            var cur = _current;

            if ((appliedBits & WindowDirty.Title) != 0)
                cur = cur with { Title = applied.Title ?? "" };

            if ((appliedBits & WindowDirty.AlwaysOnTop) != 0)
                cur = cur with { AlwaysOnTop = applied.AlwaysOnTop };

            if ((appliedBits & WindowDirty.Border) != 0)
                cur = cur with
                {
                    ResizeMode = applied.ResizeMode,
                    MinSize = applied.MinSize,
                    MaxSize = applied.MaxSize,
                    AspectRatio = applied.AspectRatio
                };

            if ((appliedBits & WindowDirty.WindowState) != 0)
                cur = cur with { State = applied.State };

            if ((appliedBits & WindowDirty.ClickThrough) != 0)
                cur = cur with { ClickThrough = applied.ClickThrough };

            if ((appliedBits & WindowDirty.TransparentAlpha) != 0)
                cur = cur with { TransparentAlpha = applied.TransparentAlpha };

            _current = cur;

            // Clear pending bits ONLY for fields that do not get OS confirmation
            _pending &= ~(appliedBits & ~(WindowDirty.WindowPos | WindowDirty.CanvasSize));
        }
    }

    // when host confirms apply completed, clear pending bits for those fields
    internal void MarkApplied(WindowDirty applied)
    {
        lock (_lock) { _pending &= ~applied; }
    }

    private static Vector2<int> EnsureDontCare(Vector2<int> value)
    {
        var x = value.X <= 0 ? Glfw.DontCare : value.X;
        var y = value.Y <= 0 ? Glfw.DontCare : value.Y;
        return new Vector2(x, y);
    }

    private void Mark(WindowDirty bit)
    {
        _dirtyToApply |= bit;
        _pending |= bit;
    }

    public override string ToString()
    {
        lock (_lock)
        {
            return $"WindowSettingsHandle(\n\tDesired=\n{PadAllLines(_desired.ToString(), "\t")},\n\n\tCurrent=\n{PadAllLines(_current.ToString(), "\t")},\n\n\tPending=\n\t{_pending},\n\n\tDirtyToApply=\n\t{_dirtyToApply})";
        }
    }

    private string PadAllLines(string input, string leftPad)
    {
        var lines = input.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = leftPad + lines[i];
        }
        return string.Join('\n', lines);
    }
}