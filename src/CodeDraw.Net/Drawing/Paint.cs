using MarcoZechner.ColorDotNet;
using MarcoZechner.ColorDotNet.RGB;

namespace MarcoZechner.CodeDrawDotNet.Drawing;

public enum PaintOrder { FillThenStroke, StrokeThenFill }

public readonly record struct Paint(
    ColorF Fill,
    Stroke Stroke,
    PaintOrder Order = PaintOrder.FillThenStroke
)
{
    public static Paint FillOnly(ColorF fill) => new(fill, new Stroke(Colors.TRANSPARENT, 0));
    public static Paint StrokeOnly(Stroke stroke) => new(Colors.TRANSPARENT, stroke);
}

