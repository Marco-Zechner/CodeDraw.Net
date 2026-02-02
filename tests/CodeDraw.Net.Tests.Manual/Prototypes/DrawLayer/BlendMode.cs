namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer;

public enum BlendMode
{
    SOURCE_OVER_ALPHA,      // SrcAlpha, OneMinusSrcAlpha
    ADD,        // One, One
    MULTIPLY,   // DstColor, Zero
    NONE,       // Disable blending
    RGB_ALPHA_KEEP_DST_A,
}
