using SkiaSharp;
using SysColor = System.Drawing.Color;

namespace MarcoZechner.ColorDotNet;

public static class ColorConversionExtensions
{
    public static SKColor ToSkia(this Color c) => new(c.R_Byte, c.G_Byte, c.B_Byte, c.A_Byte);
    public static Color ToColor(this SKColor c) => new(c.Red, c.Green, c.Blue, c.Alpha);

    public static SysColor ToSys(this Color c) => SysColor.FromArgb(c.A_Byte, c.R_Byte, c.G_Byte, c.B_Byte);
    public static Color ToColor(this SysColor c) => new(c.R, c.G, c.B, c.A);
}
