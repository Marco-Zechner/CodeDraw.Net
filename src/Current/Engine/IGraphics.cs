namespace MarcoZechner.CodeDrawDotNet;

/// <summary>
/// Minimal graphics façade handed to user callbacks. Provides safe state scoping,
/// common blending/clear helpers, and an escape hatch to the underlying graphics API.
/// </summary>
public interface IGraphics
{
    /// <summary>
    /// Expert-only: raw graphics API handle (e.g., Silk.NET OpenGL GL).
    /// Use with care; prefer the higher-level helpers when possible.
    /// </summary>
    object Raw { get; }

    /// <summary>
    /// Captures the current graphics state and restores it when disposed.
    /// Wrap any custom state changes in this guard to avoid state leaks into the engine.
    /// </summary>
    /// <returns>A disposable guard that restores state on Dispose.</returns>
    StateGuard PushState();

    /// <summary>
    /// Applies a common blending preset (Alpha, Premultiplied, Add, Multiply, Opaque, None).
    /// </summary>
    /// <param name="mode">Preset blend mode to apply.</param>
    void SetBlend(BlendMode mode);

    /// <summary>
    /// Applies a fully custom blending configuration.
    /// </summary>
    /// <param name="desc">Blend factors and equations for color and alpha.</param>
    void SetBlendCustom(BlendDesc desc);

    /// <summary>
    /// Sets the color used by subsequent clear operations. Does not clear by itself.
    /// </summary>
    /// <param name="r">Red (0..1).</param>
    /// <param name="g">Green (0..1).</param>
    /// <param name="b">Blue (0..1).</param>
    /// <param name="a">Alpha (0..1).</param>
    void ClearColor(float r, float g, float b, float a);

    /// <summary>
    /// Clears the current render target using the previously set clear color and optional buffers.
    /// </summary>
    /// <param name="mask">Which buffers to clear (defaults to color).</param>
    void Clear(ClearMask mask = ClearMask.Color);

    /// <summary>
    /// Binds an engine-managed material (shader + fixed state) for subsequent draws.
    /// No-op if the implementation does not support materials yet.
    /// </summary>
    /// <param name="material">Material object returned by the engine’s material factory.</param>
    void Use(object material);
}