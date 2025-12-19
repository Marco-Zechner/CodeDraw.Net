namespace MarcoZechner.ColorDotNet;

public partial record Color {
    public float R { get; set; } = 0.0f;
    public byte RByte { get => (byte)(R * 255); set => R = value / 255.0f; }
    public float G { get; set; } = 0.0f;
    public byte GByte { get => (byte)(G * 255); set => G = value / 255.0f; }
    public float B { get; set; } = 0.0f;
    public byte BByte { get => (byte)(B * 255); set => B = value / 255.0f; }
    public float A { get; set; } = 1.0f;
    public byte AByte { get => (byte)(A * 255); set => A = value / 255.0f; }

    public Color(float red, float green, float blue, float alpha = 1.0f){
        R = red;
        G = green;
        B = blue;
        A = alpha;
    }

    public Color(byte red, byte green, byte blue, byte alpha = 255){
        R = red / 255.0f;
        G = green / 255.0f;
        B = blue / 255.0f;
        A = alpha / 255.0f;
    }

    public Color(uint rgba) {
        R = ((rgba >> 24) & 0xFF) / 255.0f;
        G = ((rgba >> 16) & 0xFF) / 255.0f;
        B = ((rgba >> 8) & 0xFF) / 255.0f;
        A = (rgba & 0xFF) / 255.0f;
    }

    public Color(float grayscale, float alpha = 1.0f) {
        R = grayscale;
        G = grayscale;
        B = grayscale;
        A = alpha;
    }

    public Color(Color original, byte? alpha = null) {
        R = original.R;
        G = original.G;
        B = original.B;
        A = alpha.HasValue ? alpha.Value / 255.0f : original.A;
    }

    public Color(int hue, byte saturation, byte brightness, byte alpha = 255) {
        float h = hue / 360.0f;
        float s = saturation / 255.0f;
        float v = brightness / 255.0f;
        A = alpha / 255.0f;

        int i = (int)(h * 6);
        float f = h * 6 - i;
        int p = (int)(v * (1 - s) * 255);
        int q = (int)(v * (1 - f * s) * 255);
        int t = (int)(v * (1 - (1 - f) * s) * 255);
        v *= 255;

        switch (i % 6) {
            case 0: R = v; G = t; B = p; break;
            case 1: R = q; G = v; B = p; break;
            case 2: R = p; G = v; B = t; break;
            case 3: R = p; G = q; B = v; break;
            case 4: R = t; G = p; B = v; break;
            case 5: R = v; G = p; B = q; break;
        }
    }

    public Color(string hexRrggbbaa) {
        if (!hexRrggbbaa.StartsWith('#')) {
            throw new ArgumentException("Hex string must start with '#'.");
        }
        hexRrggbbaa = hexRrggbbaa[1..];
        if (hexRrggbbaa.Length != 8 && hexRrggbbaa.Length != 6) {
            throw new ArgumentException("Hex string must be 9 (with alpha) or 7 characters long.");
        }
        if (hexRrggbbaa.Length == 8) {
            A = Convert.ToByte(hexRrggbbaa[0..2], 16) / 255.0f;
            hexRrggbbaa = hexRrggbbaa[2..];
        } else {
            A = 1.0f;
        }
        R = Convert.ToByte(hexRrggbbaa[0..2], 16) / 255.0f;
        G = Convert.ToByte(hexRrggbbaa[2..4], 16) / 255.0f;
        B = Convert.ToByte(hexRrggbbaa[4..6], 16) / 255.0f;
    }

    public override string ToString()
    {
        return $"Color(R: {RByte}, G: {GByte}, B: {BByte}, A: {AByte})";
    }

    public float GetBrightness()
    {
        // Perceived brightness formula
        return (0.299f * R + 0.587f * G + 0.114f * B);
    }

    #region Math

    public static Color Lerp(Color a, Color b, float t)
    {
        return new Color(Lerp(a.R, b.R, t), Lerp(a.G, b.G, t), Lerp(a.B, b.B, t), Lerp(a.A, b.A, t));
    }

    private static float Lerp(float firstFloat, float secondFloat, float by)
    {
        return firstFloat * (1 - by) + secondFloat * by;
    }

    #endregion

    #region Constants

    public static Color Transparent => new(0x0);
    public static Color AliceBlue => new(0xF0F8FFFF);
    public static Color AntiqueWhite => new(0xFAEBD7FF);
    public static Color Aqua => new(0x00FFFFFF);
    public static Color Aquamarine => new(0x7FFFD4FF);
    public static Color Azure => new(0xF0FFFFFF);
    public static Color Beige => new(0xF5F5DCFF);
    public static Color Bisque => new(0xFFE4C4FF);
    public static Color Black => new(0x000000FF);
    public static Color BlanchedAlmond => new(0xFFEBCDFF);
    public static Color Blue => new(0x0000FFFF);
    public static Color BlueViolet => new(0x8A2BE2FF);
    public static Color Brown => new(0xA52A2AFF);
    public static Color BurlyWood => new(0xDEB887FF);
    public static Color CadetBlue => new(0x5F9EA0FF);
    public static Color Chartreuse => new(0x7FFF00FF);
    public static Color Chocolate => new(0xD2691EFF);
    public static Color Coral => new(0xFF7F50FF);
    public static Color CornflowerBlue => new(0x6495EDFF);
    public static Color Cornsilk => new(0xFFF8DCFF);
    public static Color Crimson => new(0xDC143CFF);
    public static Color Cyan => new(0x00FFFFFF);
    public static Color DarkBlue => new(0x00008BFF);
    public static Color DarkCyan => new(0x008B8BFF);
    public static Color DarkGoldenRod => new(0xB8860BFF);
    /// <summary>
    /// A medium gray color (50% brightness).
    /// </summary>
    public static Color Gray => new(0x808080FF);
    /// <summary>
    /// A dark gray color (25% brightness).
    /// </summary>
    public static Color DarkGray => new(0x404040FF);
    public static Color DarkGreen => new(0x006400FF);
    public static Color DarkKhaki => new(0xBDB76BFF);
    public static Color DarkMagenta => new(0x8B008BFF);
    public static Color DarkOliveGreen => new(0x556B2FFF);
    public static Color DarkOrange => new(0xFF8C00FF);
    public static Color DarkOrchid => new(0x9932CCFF);
    public static Color DarkRed => new(0x8B0000FF);
    public static Color DarkSalmon => new(0xE9967AFF);
    public static Color DarkSeaGreen => new(0x8FBC8FFF);
    public static Color DarkSlateBlue => new(0x483D8BFF);
    public static Color DarkSlateGray => new(0x2F4F4FFF);
    public static Color DarkTurquoise => new(0x00CED1FF);
    public static Color DarkViolet => new(0x9400D3FF);
    public static Color DeepPink => new(0xFF1493FF);
    public static Color DeepSkyBlue => new(0x00BFFFFF);
    public static Color DimGray => new(0x696969FF);
    public static Color DodgerBlue => new(0x1E90FFFF);
    public static Color FireBrick => new(0xB22222FF);
    public static Color FloralWhite => new(0xFFFAF0FF);
    public static Color ForestGreen => new(0x228B22FF);
    public static Color Fuchsia => new(0xFF00FFFF);
    public static Color Gainsboro => new(0xDCDCDCFF);
    public static Color GhostWhite => new(0xF8F8FFFF);
    public static Color Gold => new(0xFFD700FF);
    public static Color GoldenRod => new(0xDAA520FF);
    public static Color Green => new(0x008000FF);
    public static Color GreenYellow => new(0xADFF2FFF);
    public static Color HoneyDew => new(0xF0FFF0FF);
    public static Color HotPink => new(0xFF69B4FF);
    public static Color IndianRed => new(0xCD5C5CFF);
    public static Color Indigo => new(0x4B0082FF);
    public static Color Ivory => new(0xFFFFF0FF);
    public static Color Khaki => new(0xF0E68CFF);
    public static Color Lavender => new(0xE6E6FAFF);
    public static Color LavenderBlush => new(0xFFF0F5FF);
    public static Color LawnGreen => new(0x7CFC00FF);
    public static Color LemonChiffon => new(0xFFFACDFF);
    public static Color LightBlue => new(0xADD8E6FF);
    public static Color LightCoral => new(0xF08080FF);
    public static Color LightCyan => new(0xE0FFFFFF);
    public static Color LightGoldenRodYellow => new(0xFAFAD2FF);
    /// <summary>
    /// A light gray color (75% brightness).
    /// </summary>
    public static Color LightGray => new(0xD3D3D3FF);
    public static Color LightGreen => new(0x90EE90FF);
    public static Color LightPink => new(0xFFB6C1FF);
    public static Color LightSalmon => new(0xFFA07AFF);
    public static Color LightSeaGreen => new(0x20B2AAFF);
    public static Color LightSkyBlue => new(0x87CEFAFF);
    public static Color LightSlateGray => new(0x778899FF);
    public static Color LightSteelBlue => new(0xB0C4DEFF);
    public static Color LightYellow => new(0xFFFFE0FF);
    public static Color Lime => new(0x00FF00FF);
    public static Color LimeGreen => new(0x32CD32FF);
    public static Color Linen => new(0xFAF0E6FF);
    public static Color Magenta => new(0xFF00FFFF);
    public static Color Maroon => new(0x800000FF);
    public static Color MediumAquaMarine => new(0x66CDAAFF);
    public static Color MediumBlue => new(0x0000CDFF);
    public static Color MediumOrchid => new(0xBA55D3FF);
    public static Color MediumPurple => new(0x9370DBFF);
    public static Color MediumSeaGreen => new(0x3CB371FF);
    public static Color MediumSlateBlue => new(0x7B68EEFF);
    public static Color MediumSpringGreen => new(0x00FA9AFF);
    public static Color MediumTurquoise => new(0x48D1CCFF);
    public static Color MediumVioletRed => new(0xC71585FF);
    public static Color MidnightBlue => new(0x191970FF);
    public static Color MintCream => new(0xF5FFFAFF);
    public static Color MistyRose => new(0xFFE4E1FF);
    public static Color Moccasin => new(0xFFE4B5FF);
    public static Color NavajoWhite => new(0xFFDEADFF);
    public static Color Navy => new(0x000080FF);
    public static Color OldLace => new(0xFDF5E6FF);
    public static Color Olive => new(0x808000FF);
    public static Color OliveDrab => new(0x6B8E23FF);
    public static Color Orange => new(0xFFA500FF);
    public static Color OrangeRed => new(0xFF4500FF);
    public static Color Orchid => new(0xDA70D6FF);
    public static Color PaleGoldenRod => new(0xEEE8AAFF);
    public static Color PaleGreen => new(0x98FB98FF);
    public static Color PaleTurquoise => new(0xAFEEEEFF);
    public static Color PaleVioletRed => new(0xDB7093FF);
    public static Color PapayaWhip => new(0xFFEFD5FF);
    public static Color PeachPuff => new(0xFFDAB9FF);
    public static Color Peru => new(0xCD853FFF);
    public static Color Pink => new(0xFFC0CBFF);
    public static Color Plum => new(0xDDA0DDFF);
    public static Color PowderBlue => new(0xB0E0E6FF);
    public static Color Purple => new(0x800080FF);
    public static Color Red => new(0xFF0000FF);
    public static Color RosyBrown => new(0xBC8F8FFF);
    public static Color RoyalBlue => new(0x4169E1FF);
    public static Color SaddleBrown => new(0x8B4513FF);
    public static Color Salmon => new(0xFA8072FF);
    public static Color SandyBrown => new(0xF4A460FF);
    public static Color SeaGreen => new(0x2E8B57FF);
    public static Color SeaShell => new(0xFFF5EEFF);
    public static Color Sienna => new(0xA0522DFF);
    public static Color Silver => new(0xC0C0C0FF);
    public static Color SkyBlue => new(0x87CEEBFF);
    public static Color SlateBlue => new(0x6A5ACDFF);
    public static Color SlateGray => new(0x708090FF);
    public static Color Snow => new(0xFFFAFAFF);
    public static Color SpringGreen => new(0x00FF7FFF);
    public static Color SteelBlue => new(0x4682B4FF);
    public static Color Tan => new(0xD2B48CFF);
    public static Color Teal => new(0x008080FF);
    public static Color Thistle => new(0xD8BFD8FF);
    public static Color Tomato => new(0xFF6347FF);
    public static Color Turquoise => new(0x40E0D0FF);
    public static Color Violet => new(0xEE82EEFF);
    public static Color Wheat => new(0xF5DEB3FF);
    public static Color White => new(0xFFFFFFFF);
    public static Color WhiteSmoke => new(0xF5F5F5FF);
    public static Color Yellow => new(0xFFFF00FF);
    public static Color YellowGreen => new(0x9ACD32FF);
    #endregion
}