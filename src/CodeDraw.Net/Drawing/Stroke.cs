using MarcoZechner.ColorDotNet.RGB;

namespace MarcoZechner.CodeDrawDotNet.Drawing;

public enum StrokeAlign { Inside, Center, Outside } // Center = symmetric
public enum LineJoin { Miter, Bevel, Round }
public enum LineCap { Butt, Square, Round }

public readonly record struct Stroke(
    ColorF Color,
    float Thickness = 1f,
    StrokeAlign Align = StrokeAlign.Outside,
    LineJoin Join = LineJoin.Miter,
    LineCap Cap = LineCap.Butt,
    float MiterLimit = 4f
);