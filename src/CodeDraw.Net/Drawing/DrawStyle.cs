using MarcoZechner.CodeDrawDotNet.DrawLayer;

namespace MarcoZechner.CodeDrawDotNet.Drawing;

public readonly record struct DrawStyle(
    Paint Paint,
    BlendMode Blend = BlendMode.SOURCE_OVER_ALPHA,
    float Opacity = 1f,  // multiplies both fill+stroke alpha
    float FeatherPx = 1f // AA width in pixels (0 = hard edge)
);