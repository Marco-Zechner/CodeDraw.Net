using MarcoZechner.ColorDotNet;
using SkiaSharp;
using SysColor = System.Drawing.Color;

namespace Legacy1.MarcoZechner.CodeDrawDotNet.Extensions;

public static class ColorConversionExtensions
{
    public static SKColor ToSkia(this Color c) => new(c.RByte, c.GByte, c.BByte, c.AByte);
    public static Color ToColor(this SKColor c) => new(c.Red, c.Green, c.Blue, c.Alpha);

    public static SysColor ToSys(this Color c) => SysColor.FromArgb(c.AByte, c.RByte, c.GByte, c.BByte);
    public static Color ToColor(this SysColor c) => new(c.R, c.G, c.B, c.A);
}
