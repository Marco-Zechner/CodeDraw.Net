namespace MarcoZechner.CodeDrawDotNet.Drawing;

public enum TransformCombine
{
    // New current = (Current * m)  (typical for "apply another local transform")
    MultiplyCurrent,

    // New current = m  (absolute set, but still stackable)
    Replace
}