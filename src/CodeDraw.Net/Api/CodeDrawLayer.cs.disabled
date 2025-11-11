using MarcoZechner.CodeDrawDotNet.Interfaces;
using MarcoZechner.ColorDotNet;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Api;

/// <summary>
/// A reusable, shared render target (triple-buffered). Record draw commands and <see cref="Show"/>,
/// then any number of windows can sample it (possibly with different transforms/materials).
/// </summary>
public sealed class CodeDrawLayer
{
    /// <summary>Render target size in pixels.</summary>
    public Vector2<int> Size { get; set; }

    /// <summary>Layer-local → world transform (used when windows place this layer).</summary>
    public Matrix3x3 World { get; set; } = Matrix3x3.Identity;

    /// <summary>
    /// Creates a new shared layer with the specified size.
    /// </summary>
    public CodeDrawLayer(Vector2<int> size) => Size = size;

    /// <summary>
    /// Clears the layer for the current frame. Default (if you pass <see cref="Color.Transparent"/>) is transparent black.
    /// </summary>
    public void Clear(in Color color) => throw new NotImplementedException();

    /// <summary>
    /// Draws a user-defined shape (via <see cref="IDrawShape"/>) into this layer for the current frame.
    /// </summary>
    /// <param name="shape">Shape that knows how to render itself with Skia onto a canvas.</param>
    public void Draw(in IDrawShape shape) => throw new NotImplementedException();

    /// <summary>
    /// Composites another layer into this one with an optional transform and material override.
    /// </summary>
    /// <param name="layer">Source layer to sample.</param>
    /// <param name="transform">Optional placement transform (source layer local → this layer local/world as you define).</param>
    /// <param name="material">Optional material/shader override (engine-managed placeholder).</param>
    public void DrawLayer(CodeDrawLayer layer, Matrix3x3? transform = null, object? material = null)
        => throw new NotImplementedException();

    /// <summary>
    /// Finalizes and publishes this layer’s current frame to its triple buffer.
    /// </summary>
    public void Show() => throw new NotImplementedException();

    /// <summary>
    /// Blocks until a frame for this layer has been produced.
    /// </summary>
    public void WaitForRender() => throw new NotImplementedException();
}
