namespace MarcoZechner.ColorDotNet;

public enum ColorLayout
{
    // “channel order” layouts (can be 3 or 4 digits/bytes)
    RGB,
    RGBA,
    ARGB,
    // “byte-group” layouts (classic hex byte pairs)
    RRGGBB,
    RRGGBBAA,
    AARRGGBB
}