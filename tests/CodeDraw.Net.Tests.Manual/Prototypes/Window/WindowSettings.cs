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
}

public enum WindowState { Windowed, Minimized, Maximized, Fullscreen }
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
    bool TransparentAlpha
)
{
    public WindowSettingsSnapshot Normalize()
    {
        var d = this;

        // sanitize title
        d = d with { Title = d.Title ?? "" };

        // normalize "DontCare" semantics
        static Vector2<int> DC(Vector2<int> v)
            => new(v.X <= 0 ? Glfw.DontCare : v.X, v.Y <= 0 ? Glfw.DontCare : v.Y);

        var min = DC(d.MinSize);
        var max = DC(d.MaxSize);
        var asp = DC(d.AspectRatio);

        // Fullscreen hard forces
        if (d.State == WindowState.Fullscreen)
        {
            return d with
            {
                FrameMode = WindowFrameMode.Hidden,
                ResizeMode = WindowResizeMode.Fixed,
                MinSize = new(Glfw.DontCare, Glfw.DontCare),
                MaxSize = new(Glfw.DontCare, Glfw.DontCare),
                AspectRatio = new(Glfw.DontCare, Glfw.DontCare),
            };
        }

        // Maximized: you currently treat it as "fixed" (no constraints)
        if (d.State == WindowState.Maximized)
        {
            return d with
            {
                ResizeMode = WindowResizeMode.Fixed,
                MinSize = new(Glfw.DontCare, Glfw.DontCare),
                MaxSize = new(Glfw.DontCare, Glfw.DontCare),
                AspectRatio = new(Glfw.DontCare, Glfw.DontCare),
            };
        }

        // Windowed: constraints depend on ResizeMode
        switch (d.ResizeMode)
        {
            case WindowResizeMode.Fixed:
            case WindowResizeMode.Resizable:
                min = new(Glfw.DontCare, Glfw.DontCare);
                max = new(Glfw.DontCare, Glfw.DontCare);
                asp = new(Glfw.DontCare, Glfw.DontCare);
                break;

            case WindowResizeMode.Limited:
                asp = new(Glfw.DontCare, Glfw.DontCare);
                break;

            case WindowResizeMode.Aspect:
                min = new(Glfw.DontCare, Glfw.DontCare);
                max = new(Glfw.DontCare, Glfw.DontCare);
                break;
        }

        // ensure sane size (avoid 0/negative)
        var size = d.Size;
        if (size.X < 1) size = size.WithX(1);
        if (size.Y < 1) size = size.WithY(1);

        return d with { MinSize = min, MaxSize = max, AspectRatio = asp, Size = size };
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