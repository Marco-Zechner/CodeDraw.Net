namespace MarcoZechner.CodeDrawDotNet;

/// <summary>Bitmask of buffers for clear operations.</summary>
[Flags]
public enum ClearMask
{
    /// <summary>Clear the color buffer.</summary>
    Color = 1 << 0,
    /// <summary>Clear the depth buffer.</summary>
    Depth = 1 << 1,
    /// <summary>Clear the stencil buffer.</summary>
    Stencil = 1 << 2,
    /// <summary>Clear color and depth buffers.</summary>
    ColorDepth = Color | Depth,
    /// <summary>Clear all: color, depth, and stencil buffers.</summary>
    All = Color | Depth | Stencil
}