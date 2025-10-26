using MarcoZechner.CodeDrawDotNet.Engine;
using MarcoZechner.ColorLib;
using MarcoZechner.Math;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet;

/// <summary>
/// A visible window (with its own render thread) that can draw directly
/// and/or composite shared layers in a chosen order.
/// </summary>
public unsafe sealed class CodeDrawWindow
{
    private WindowRendererBasic? _renderer;

    /// <summary>Title shown in the OS window chrome.</summary>
    public string Title { get; }

    /// <summary>
    /// Drawable size in pixels. Safe to set before <see cref="Open"/>; marshalled to the UI thread after.
    /// </summary>
    public Vector2<int> Size { get; set; }

    /// <summary>Whether the window can be resized by the user.</summary>
    public bool Resizable { get; set; } = true;

    /// <summary>Target frames per second. 0 = uncapped.</summary>
    public int TargetFPS { get; set; } = 60;

    /// <summary>Background color used to clear the default framebuffer each frame.</summary>
    public Color ClearColor { get; set; } = Color.BLACK;

    /// <summary>Monotonic time since this window started (after <see cref="Open"/>).</summary>
    public TimeSpan Uptime => _renderer?.Uptime ?? TimeSpan.Zero;

    /// <summary>Total number of frames presented by this window.</summary>
    public long Frames => _renderer?.Frames ?? 0;

    /// <summary>User-defined identifier for convenience (optional).</summary>
    public object? Tag { get; set; }

    /// <summary>
    /// Fired once on the window's render thread after GL context creation
    /// and right before the first frame. Use to create per-window resources.
    /// </summary>
    public event Action<CodeDrawWindow, IGraphics>? Loaded;

    /// <summary>
    /// Fired every frame on the window's render thread, just before the window renders.
    /// <paramref name="double"/> is deltaTime (seconds since this window's previous frame).
    /// </summary>
    public event Action<CodeDrawWindow, IGraphics, double>? BeforeRender;

    internal void RaiseLoaded(IGraphics gfx) => Loaded?.Invoke(this, gfx);
    internal void RaiseBeforeRender(IGraphics gfx, double dt) => BeforeRender?.Invoke(this, gfx, dt);

    /// <summary>
    /// Creates a new window object. The render thread and GL resources are not created
    /// until <see cref="Open"/> is called.
    /// </summary>
    /// <param name="title">Window title.</param>
    public CodeDrawWindow(string title)
    {
        Title = title ?? throw new ArgumentNullException(nameof(title));
    }

    /// <summary>
    /// Starts the render thread, creates the GL context, and begins the frame loop.
    /// Triggers <see cref="Loaded"/> once and then <see cref="BeforeRender"/> every frame.
    /// </summary>
    public void Open()
    {
        var host = CodeDrawHost.Instance;
        host.EnsureStarted();

        WindowHandle* handle = host.CreateWindow(Size.X, Size.Y, Title);

        _renderer = new WindowRendererBasic(handle!, Title);
        _renderer.BindPublic(this);
        _renderer.Start();
    }

    /// <summary>
    /// Clears the window’s private offscreen buffer for this frame.
    /// </summary>
    /// <param name="color">Clear color (alpha is respected only for offscreen targets).</param>
    public void Clear(in Color color) => throw new NotImplementedException();

    /// <summary>
    /// Draws a user-defined shape (via <see cref="IDrawShape"/>) into this window for the current frame.
    /// </summary>
    /// <param name="shape">Shape that knows how to render itself with Skia onto a canvas.</param>
    public void Draw(in IDrawShape shape) => throw new NotImplementedException();

    /// <summary>
    /// Composites a shared layer onto this window using an optional transform and material override.
    /// </summary>
    /// <param name="layer">Shared layer to sample.</param>
    /// <param name="transform">Optional placement transform (layer-local → window).</param>
    /// <param name="material">Optional material/shader override (engine-managed placeholder).</param>
    public void DrawLayer(CodeDrawLayer layer, Matrix3x3? transform = null, object? material = null)
        => throw new NotImplementedException();

    /// <summary>
    /// Finalizes and enqueues this frame for presentation.
    /// </summary>
    public void Show() => throw new NotImplementedException();

    /// <summary>
    /// Blocks until the last <see cref="Show"/> frame has been presented.
    /// </summary>
    public void WaitForRender() => throw new NotImplementedException();

    /// <summary>
    /// Converts a point from window pixels to the local space of a target layer.
    /// </summary>
    /// <param name="layer">Target layer.</param>
    /// <param name="p">Point in window pixels.</param>
    /// <returns>Point in the layer's local space.</returns>
    public System.Numerics.Vector2 WindowToLayer(CodeDrawLayer layer, System.Numerics.Vector2 p)
        => throw new NotImplementedException();

    /// <summary>
    /// Converts a point from a layer's local space to window pixels.
    /// </summary>
    /// <param name="layer">Source layer.</param>
    /// <param name="p">Point in the layer's local space.</param>
    /// <returns>Point in window pixels.</returns>
    public System.Numerics.Vector2 LayerToWindow(CodeDrawLayer layer, System.Numerics.Vector2 p)
        => throw new NotImplementedException();
}
