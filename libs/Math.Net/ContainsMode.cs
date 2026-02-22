namespace MarcoZechner.MathDotNet;

[Flags]
public enum ContainsMode
{
    Exclusive = 0,
    InclusiveTop = 1 << 0,
    InclusiveRight = 1 << 1,
    InclusiveBottom = 1 << 2,
    InclusiveLeft = 1 << 3,
    Inclusive = InclusiveTop | InclusiveRight | InclusiveBottom | InclusiveLeft,
    InclusiveMin = InclusiveTop | InclusiveLeft,
    InclusiveMax = InclusiveBottom | InclusiveRight,
}