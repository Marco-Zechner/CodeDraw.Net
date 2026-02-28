using MarcoZechner.ColorDotNet.RGB;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode;

public readonly record struct SdfColorRule(
    SdfRuleMode Mode,
    ColorF ColorA,
    float A,
    float B,
    float FeatherPx,
    ColorF? ColorB = null,
    float StepPx = 0f
);