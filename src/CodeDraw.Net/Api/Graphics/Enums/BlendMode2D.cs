namespace MarcoZechner.CodeDrawDotNet.Api.Graphics.Enums;

public enum BlendMode2D
{
    OPAQUE_REPLACE,          // overwrite RGBA
    RGB_BLEND_KEEP_DST_ALPHA,  // your desired default
    WRITE_ALPHA_REPLACE       // keep RGB, set alpha = src alpha
}