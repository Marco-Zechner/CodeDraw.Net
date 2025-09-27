using MarcoZechner.ColorLib;

namespace MarcoZechner.CodeDraw.Net;

public record ShapeSettings(
    Color DrawColor,
    int LineWidth = 1,
    CornerStyle CornerStyle = CornerStyle.SHARP,
    int CornerRadius = 0,
    bool IsAntiAliased = false,
    bool IsFill = false
);