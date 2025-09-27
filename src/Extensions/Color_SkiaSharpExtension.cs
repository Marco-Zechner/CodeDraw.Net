using SysColor = System.Drawing.Color;
using SkiaSharp;

namespace MarcoZechner.ColorLib;

public partial record Color {
    public static implicit operator SKColor(Color c) => new(c.R_Byte, c.G_Byte, c.B_Byte, c.A_Byte);

    public static implicit operator Color(SKColor c) => new(c.Red, c.Green, c.Blue, c.Alpha);

    public static implicit operator SysColor(Color c) => SysColor.FromArgb(c.A_Byte, c.R_Byte, c.G_Byte, c.B_Byte);

    public static implicit operator Color(SysColor c) => new(c.R, c.G, c.B, c.A);
}