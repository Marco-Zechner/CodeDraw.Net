namespace MarcoZechner.CodeDrawDotNet.Api.Graphics.Enums;

public enum BlendMode2D
{
    OPAQUE_REPLACE,          // overwrite RGBA
    RGB_BLEND_KEEP_DST_ALPHA,  // your desired default
    WRITE_ALPHA_REPLACE,       // keep RGB, set alpha = src alpha
    RGB_BLEND_SOURCEOVER_ALPHA // blend RGB, a_out = src.a + dst.a * (1-src.a)
}