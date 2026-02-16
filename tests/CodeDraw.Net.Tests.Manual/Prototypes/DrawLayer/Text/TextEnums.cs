namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer.Text;

public enum TextAlign { Left, Center, Right }
public enum TextVAlign { Top, Middle, Bottom }

public readonly record struct TextMetrics(float Width, float Height);

[Flags]
public enum TextDebugMode
{
    None = 0,

    // Monospace cells
    Cells = 1,

    // Glyph bitmap box (bearing + bitmap size)
    GlyphBoxes = 2,

    // Baseline marker per line
    Baseline = 4,

    All = Cells | GlyphBoxes | Baseline
}

public enum DebugRectMode
{
    Fill,
    Outline,
    FillAndOutline
}