namespace MarcoZechner.MathDotNet;

public enum RectAnchorMode
{
    KeepPosition,   // Position stays fixed in world; LocalOrigin changes as needed.
    KeepLocalOrigin, // LocalOrigin stays fixed in rect-space; Position changes as needed.
}