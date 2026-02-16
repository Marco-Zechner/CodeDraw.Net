namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer;

/// <summary>
/// Fixed-function GPU blending modes.
/// 
/// <para>
/// These are implemented using OpenGL's blending stage. <br/>
/// Only linear operations between source and destination are possible.
/// here.
/// </para>
/// 
/// <para>
/// Source = fragment being drawn. <br/>
/// Destination = existing framebuffer content.
/// </para>
/// </summary>
public enum BlendMode
{
    /// <summary>
    /// <para>No blending. The source overwrites the destination.</para>
    /// 
    /// <list type="bullet">
    /// <item>glDisable(GL_BLEND)</item>
    /// </list>
    /// 
    /// <para>
    /// Use when drawing opaque geometry or when alpha is not needed.
    /// </para>
    /// </summary>
    NONE,

    /// <summary>
    /// <para>Standard alpha blending for non-premultiplied colors.</para>
    /// 
    /// <para>
    /// out = src * src.a + dst * (1 - src.a)
    /// </para>
    /// 
    /// <list type="bullet">
    /// <item>glEnable(GL_BLEND)</item>
    /// <item>glBlendEquation(FUNC_ADD)</item>
    /// <item>glBlendFunc(SRC_ALPHA, ONE_MINUS_SRC_ALPHA)</item>
    /// </list>
    /// 
    /// <para>
    /// Default for UI, text, sprites, and coverage masks.
    /// </para>
    /// </summary>
    SOURCE_OVER_ALPHA,

    /// <summary>
    /// <para>Premultiplied alpha blending.</para>
    /// 
    /// <para>
    /// out = src + dst * (1 - src.a)
    /// </para>
    /// 
    /// <list type="bullet">
    /// <item>glEnable(GL_BLEND)</item>
    /// <item>glBlendEquation(FUNC_ADD)</item>
    /// <item>glBlendFunc(ONE, ONE_MINUS_SRC_ALPHA)</item>
    /// </list>
    /// 
    /// <para>
    /// Use when the source color is already multiplied by alpha. <br/>
    /// Prevents dark halos and is preferred in modern pipelines.
    /// </para>
    /// </summary>
    PREMULTIPLIED_ALPHA,

    /// <summary>
    /// <para>Additive blending.</para>
    /// 
    /// <para>
    /// out = src + dst
    /// </para>
    /// 
    /// <list type="bullet">
    /// <item>glEnable(GL_BLEND)</item>
    /// <item>glBlendFunc(ONE, ONE)</item>
    /// </list>
    /// 
    /// <para>
    /// Used for glow, particles, energy, lighting.
    /// </para>
    /// </summary>
    ADD,

    /// <summary>
    /// <para>Multiplicative blending.</para>
    /// 
    /// <para>
    /// out = src * dst
    /// </para>
    /// 
    /// <list type="bullet">
    /// <item>glEnable(GL_BLEND)</item>
    /// <item>glBlendFunc(DST_COLOR, ZERO)</item>
    /// </list>
    /// 
    /// <para>
    /// Used for shadows, tinting, decals.
    /// </para>
    /// </summary>
    MULTIPLY,

    /// <summary>
    /// <para>Subtractive blending.</para>
    /// 
    /// <para>
    /// out = dst - src
    /// </para>
    /// 
    /// <list type="bullet">
    /// <item>glEnable(GL_BLEND)</item>
    /// <item>glBlendEquation(FUNC_REVERSE_SUBTRACT)</item>
    /// <item>glBlendFunc(ONE, ONE)</item>
    /// </list>
    /// 
    /// <para>
    /// Useful for masking, darkening, certain post effects.
    /// </para>
    /// </summary>
    SUBTRACT,

    /// <summary>
    /// <para>Inverse subtract blending.</para>
    /// 
    /// <para>
    /// out = src - dst
    /// </para>
    /// 
    /// <list type="bullet">
    /// <item>glEnable(GL_BLEND)</item>
    /// <item>glBlendEquation(FUNC_SUBTRACT)</item>
    /// <item>glBlendFunc(ONE, ONE)</item>
    /// </list>
    /// 
    /// <para>
    /// Rare but useful for special effects.
    /// </para>
    /// </summary>
    INVERSE_SUBTRACT,

    /// <summary>
    /// <para>Minimum blending.</para>
    /// 
    /// <para>
    /// out = min(src, dst)
    /// </para>
    /// 
    /// <list type="bullet">
    /// <item>glEnable(GL_BLEND)</item>
    /// <item>glBlendEquation(MIN)</item>
    /// </list>
    /// 
    /// <para>
    /// Good for masks, distance fields, certain compositing.
    /// </para>
    /// </summary>
    MIN,

    /// <summary>
    /// <para>Maximum blending.</para>
    /// 
    /// <para>
    /// out = max(src, dst)
    /// </para>
    /// 
    /// <list type="bullet">
    /// <item>glEnable(GL_BLEND)</item>
    /// <item>glBlendEquation(MAX)</item>
    /// </list>
    /// 
    /// <para>
    /// Used for light accumulation and HDR tricks.
    /// </para>
    /// </summary>
    MAX,

    /// <summary>
    /// <para>Standard alpha blending but preserves destination alpha.</para>
    /// 
    /// <para>
    /// RGB: src over dst  
    /// Alpha: destination unchanged.
    /// </para>
    /// 
    /// <list type="bullet">
    /// <item>glEnable(GL_BLEND)</item>
    /// <item>glBlendFuncSeparate(SRC_ALPHA, ONE_MINUS_SRC_ALPHA, ZERO, ONE)</item>
    /// </list>
    /// 
    /// <para>
    /// Useful when alpha encodes masks or coverage.
    /// </para>
    /// </summary>
    RGB_ALPHA_KEEP_DST_A,
}