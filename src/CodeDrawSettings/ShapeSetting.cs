using MarcoZechner.ColorLib;

namespace MarcoZechner.CodeDrawDotNet;

public record ShapeSettings(
    Color DrawColor,
    int LineWidth = 1,
    CornerStyle CornerStyle = CornerStyle.SHARP,
    int CornerRadius = 0,
    bool IsAntiAliased = false,
    bool IsFill = false
);