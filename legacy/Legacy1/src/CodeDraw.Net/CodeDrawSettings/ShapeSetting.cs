using Legacy1.MarcoZechner.CodeDrawDotNet.CodeDrawSettings.DrawSettings;
using MarcoZechner.ColorDotNet;

namespace Legacy1.MarcoZechner.CodeDrawDotNet.CodeDrawSettings;

public record ShapeSettings(
    Color DrawColor,
    int LineWidth = 1,
    CornerStyle CornerStyle = CornerStyle.SHARP,
    int CornerRadius = 0,
    bool IsAntiAliased = false,
    bool IsFill = false
);