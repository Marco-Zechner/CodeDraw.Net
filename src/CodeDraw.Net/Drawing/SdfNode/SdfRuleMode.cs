namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode;

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