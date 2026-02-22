namespace MarcoZechner.CodeDrawDotNet.Text;

public readonly record struct FontMetrics(
    int SizePx,

    // From face metrics (scaled) if possible; otherwise approximated.
    float AscenderPx,
    float DescenderPx,
    float RecommendedLinePx,

    // Approximations via glyphs:
    float CapHeightPx,
    float XHeightPx,

    // True extents from sampled glyph set:
    float MaxAbovePx,
    float MaxBelowPx,

    // Monospace “cell advance”
    float MonoAdvancePx
);