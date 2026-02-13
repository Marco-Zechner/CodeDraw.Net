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
    Border           = 1u << 3, // decorations + resizable + constraints (min/max/aspect)
    AlwaysOnTop      = 1u << 4,
    WindowState      = 1u << 5,
    ClickThrough     = 1u << 6,
    TransparentAlpha = 1u << 7, // render-side only in your current design
    FocusPolicy      = 1u << 8,
}

public enum WindowState
{
    Windowed,             // honors FrameMode + ResizeMode + constraints
    Minimized,            // not visible; ignore most things
    Maximized,            // OS maximize (real), ignore FrameMode/ResizeMode/constraints
    BorderlessMaximized,  // manual maximize to workarea, forced borderless+fixed
    BorderlessFullscreen, // manual borderless fullscreen (+1px hack), forced borderless+fixed
}
public enum WindowFrameMode { Decorated, Hidden }
public enum WindowResizeMode
{
    Resizable, // resizable, no constraints
    Limited,   // resizable, size limits active
    Aspect,    // resizable, aspect ratio active
    Fixed,     // not resizable, no constraints
}

/// <summary>
/// Single authoritative snapshot: "the last settings we applied (or intend to apply)".
/// With the new design there is no OS->snapshot sync.
/// </summary>
public readonly record struct WindowSettingsSnapshot(
    Vector2<int> WindowPosition,
    Vector2<int> Size,
    string Title,
    bool AlwaysOnTop,

    WindowFrameMode FrameMode,
    WindowResizeMode ResizeMode,

    Vector2<int> MinSize,
    Vector2<int> MaxSize,
    Vector2<int> AspectRatio,

    WindowState State,
    bool ClickThrough,
    bool TransparentAlpha,
    bool StealFocusOnOpen
)
{
    // How many pixels are "wasted" by the fake fullscreen hack.
    private int ExtraRightPixels
        => State == WindowState.BorderlessFullscreen ? 1 : 0;

    // What the user is allowed to draw into.
    public Vector2<int> ClientSize
    {
        get
        {
            var w = Size.X - ExtraRightPixels;
            var h = Size.Y;
            if (w < 1) w = 1;
            if (h < 1) h = 1;
            return new Vector2<int>(w, h);
        }
    }
    
    public WindowSettingsSnapshot Normalize()
    {
        var d = this;

        // ensure valid constraints (convert 0 or negative to DontCare, and clamp min against max if both specified)
        var min = Dc(d.MinSize);
        var max = Dc(d.MaxSize);
        var asp = Dc(d.AspectRatio);

        if (max.X != Glfw.DontCare && max.X < min.X)
            min = min.WithX(max.X);
        if (max.Y != Glfw.DontCare && max.Y < min.Y)
            min = min.WithY(max.Y);
        if (asp.X == Glfw.DontCare || asp.Y == Glfw.DontCare)
            asp = Vector2<int>.One * Glfw.DontCare;

        // ensure sane size (avoid 0/negative)
        var size = d.Size;
        if (size.X < 1) size = size.WithX(1);
        if (size.Y < 1) size = size.WithY(1);

        if (d is { State: WindowState.Windowed, ResizeMode: WindowResizeMode.Limited })
        {
            // Clamp against min/max if they are not DontCare
            var minX = (min.X == Glfw.DontCare) ? 1 : min.X;
            var minY = (min.Y == Glfw.DontCare) ? 1 : min.Y;
            var maxX = (max.X == Glfw.DontCare) ? int.MaxValue : max.X;
            var maxY = (max.Y == Glfw.DontCare) ? int.MaxValue : max.Y;

            size = new Vector2<int>(
                Math.Clamp(size.X, minX, maxX),
                Math.Clamp(size.Y, minY, maxY)
            );
        }

        if (d is { State: WindowState.Windowed, ResizeMode: WindowResizeMode.Aspect })
        {
            if (asp.X != Glfw.DontCare && asp.Y != Glfw.DontCare)
            {
                // y = x * asp.Y / asp.X
                var y = (int)Math.Round(size.X * (asp.Y / (double)asp.X));
                if (y < 1) y = 1;
                size = size.WithY(y);
            }
        }
        
        if (d.State == WindowState.BorderlessFullscreen)
        {
            // If user wants "1920", you store physical "1921"
            var physical = size;
            if (physical.X < 1) physical = physical.WithX(1);
            physical = physical.WithX(physical.X + ExtraRightPixels);
            size = physical;
        }

        return d with { MinSize = min, MaxSize = max, AspectRatio = asp, Size = size };

        // normalize "DontCare" semantics
        static Vector2<int> Dc(Vector2<int> v)
            => new(v.X <= 0 ? Glfw.DontCare : v.X, v.Y <= 0 ? Glfw.DontCare : v.Y);
    }


    internal WindowDirty ComputeDirty(WindowSettingsSnapshot newSettings)
    {
        var d = WindowDirty.None;

        if (Title != newSettings.Title) d |= WindowDirty.Title;
        if (AlwaysOnTop != newSettings.AlwaysOnTop) d |= WindowDirty.AlwaysOnTop;
        if (ClickThrough != newSettings.ClickThrough) d |= WindowDirty.ClickThrough;
        if (TransparentAlpha != newSettings.TransparentAlpha) d |= WindowDirty.TransparentAlpha;

        if (State != newSettings.State) d |= WindowDirty.WindowState;

        if (WindowPosition != newSettings.WindowPosition) d |= WindowDirty.WindowPos;
        if (Size != newSettings.Size) d |= WindowDirty.CanvasSize;

        if (StealFocusOnOpen != newSettings.StealFocusOnOpen) d |= WindowDirty.FocusPolicy;
        
        // border/constraints bundle
        if (FrameMode != newSettings.FrameMode ||
            ResizeMode != newSettings.ResizeMode ||
            MinSize != newSettings.MinSize ||
            MaxSize != newSettings.MaxSize ||
            AspectRatio != newSettings.AspectRatio)
            d |= WindowDirty.Border;

        return d;
    }

    public override string ToString()
    {
        return $"""
                Title: {Title}
                Position: {WindowPosition}
                Size: {Size}
                AlwaysOnTop: {AlwaysOnTop}
                FrameMode: {FrameMode}
                ResizeMode: {ResizeMode}
                MinSize: {MinSize}
                MaxSize: {MaxSize}
                AspectRatio: {AspectRatio}
                State: {State}
                ClickThrough: {ClickThrough}
                TransparentAlpha: {TransparentAlpha}
                """;
    }
}