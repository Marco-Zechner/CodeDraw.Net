using MarcoZechner.ColorDotNet.RGB;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfGpu;

public enum SdfRuleMode
{
    Disabled   = 0,
    SdLessThan = 1,
    SdGreaterThan = 2,
    Range = 3,
    NearValue = 4,
    Gradient = 5,
    GradientStep = 6,
}

public readonly record struct SdfColorRuleDef(
    SdfRuleMode Mode,
    ColorF ColorA,
    float A,
    float B,
    float FeatherPx,
    ColorF? ColorB = null,
    float StepPx = 0f
);

public sealed class SdfMaterialDef(in DrawStyle style)
{
    public DrawStyle Style = style;
    public readonly List<SdfColorRuleDef> Rules = [];

}