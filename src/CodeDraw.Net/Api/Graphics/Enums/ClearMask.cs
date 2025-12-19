namespace MarcoZechner.CodeDrawDotNet.Api.Graphics;

/// <summary>Bitmask of buffers for clear operations.</summary>
[Flags]
public enum ClearMask
{
    /// <summary>Clear the color buffer.</summary>
    COLOR = 1 << 0,
    /// <summary>Clear the depth buffer.</summary>
    DEPTH = 1 << 1,
    /// <summary>Clear the stencil buffer.</summary>
    STENCIL = 1 << 2,
    /// <summary>Clear color and depth buffers.</summary>
    COLOR_DEPTH = COLOR | DEPTH,
    /// <summary>Clear all: color, depth, and stencil buffers.</summary>
    ALL = COLOR | DEPTH | STENCIL
}