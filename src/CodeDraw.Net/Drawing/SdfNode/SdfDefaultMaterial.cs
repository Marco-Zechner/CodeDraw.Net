using MarcoZechner.ColorDotNet.RGB;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode;

internal static class SdfDefaultMaterial
{
    public static readonly SdfMaterial Instance =
        new(
            new DrawStyle(
                new Paint(new ColorF(1f, 1f, 1f, 1f), default(Stroke)),
                FeatherPx: 1.0f
            ),
            SdfColorOverwrite.OnlyDefault
        );
}