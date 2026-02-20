namespace MarcoZechner.CodeDrawDotNet.DrawLayer.Text;

public sealed record TextStyle
{
    public FontRef Font { get; set; }
    public float SizePx { get; set; } = 16;

    public TextAlign Align { get; set; } = TextAlign.Left;
    public TextVAlign VAlign { get; set; } = TextVAlign.Top;

    public Rgba Color { get; set; } = new(1, 1, 1, 1);
    public Rgba Background { get; set; } = new(0, 0, 0, 0.5f);
    
    public BlendMode FontBlendMode { get; set; } = BlendMode.SOURCE_OVER_ALPHA;
    public BlendMode BackgroundBlendMode { get; set; } = BlendMode.SOURCE_OVER_ALPHA;

    public TextBackgroundMode BackgroundMode { get; set; } = TextBackgroundMode.None;
    public bool BackgroundIncludeSpaces { get; set; } = false;
    public float BackgroundPaddingPx { get; set; } = 0f;

    // --- Monospace layout control ---

    public float ExtraAbovePx { get; set; } = 0;
    public float ExtraBelowPx { get; set; } = 0;
    public float ExtraLineGapPx { get; set; } = 0;
    public float ExtraCellGapPx { get; set; } = 0;

    public float? OverrideCellWidthPx { get; set; } = null;
    public float? OverrideLineHeightPx { get; set; } = null;

    /// <summary>
    /// If true and Align is Center/Right, each line is aligned individually,
    /// but snapped to integer cell steps (monospace “grid perfect” centering).
    /// If false, line alignment may be fractional (useful for proportional fonts or “smooth” centering).
    /// </summary>
    public bool MonospaceSnapLineAlignToCells { get; set; } = true;

    public TextDebugMode DebugMode { get; set; } = TextDebugMode.None;

    /// <summary>
    /// How debug rectangles are drawn.
    /// Fill looks “solid”; Outline shows the grid clearly.
    /// </summary>
    public DebugRectMode DebugRects { get; set; } = DebugRectMode.Outline;

    /// <summary>
    /// Outline thickness in px for debug rect outlines.
    /// </summary>
    public float DebugOutlinePx { get; set; } = 1;

    // --- internal caches ---
    internal string? CachedKey;
    internal FontMetrics CachedMetrics;
}