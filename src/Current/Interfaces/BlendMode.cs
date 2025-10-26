namespace MarcoZechner.CodeDrawDotNet;

/// <summary>Common blending modes.</summary>
public enum BlendMode
{
    /// <summary>Standard alpha blending: SrcAlpha, OneMinusSrcAlpha.</summary>
    Alpha,
    /// <summary>Premultiplied alpha blending: One, OneMinusSrcAlpha.</summary>
    PremultipliedAlpha,
    /// <summary>Additive blending: One, One.</summary>
    Add,
    /// <summary>Multiplicative blending: DstColor, Zero.</summary>
    Multiply,
    /// <summary>Opaque (disable blending).</summary>
    Opaque,
    /// <summary>No blending (disable blend, raw writes).</summary>
    None
}