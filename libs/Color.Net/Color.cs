namespace MarcoZechner.ColorDotNet;

public partial record Color {
    public float R { get; set; } = 0.0f;
    public byte R_Byte { get => (byte)(R * 255); set => R = value / 255.0f; }
    public float G { get; set; } = 0.0f;
    public byte G_Byte { get => (byte)(G * 255); set => G = value / 255.0f; }
    public float B { get; set; } = 0.0f;
    public byte B_Byte { get => (byte)(B * 255); set => B = value / 255.0f; }
    public float A { get; set; } = 1.0f;
    public byte A_Byte { get => (byte)(A * 255); set => A = value / 255.0f; }

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

    public Color(string hex_RRGGBBAA) {
        if (!hex_RRGGBBAA.StartsWith('#')) {
            throw new ArgumentException("Hex string must start with '#'.");
        }
        hex_RRGGBBAA = hex_RRGGBBAA[1..];
        if (hex_RRGGBBAA.Length != 8 && hex_RRGGBBAA.Length != 6) {
            throw new ArgumentException("Hex string must be 9 (with alpha) or 7 characters long.");
        }
        if (hex_RRGGBBAA.Length == 8) {
            A = Convert.ToByte(hex_RRGGBBAA[0..2], 16) / 255.0f;
            hex_RRGGBBAA = hex_RRGGBBAA[2..];
        } else {
            A = 1.0f;
        }
        R = Convert.ToByte(hex_RRGGBBAA[0..2], 16) / 255.0f;
        G = Convert.ToByte(hex_RRGGBBAA[2..4], 16) / 255.0f;
        B = Convert.ToByte(hex_RRGGBBAA[4..6], 16) / 255.0f;
    }

    public override string ToString()
    {
        return $"Color(R: {R_Byte}, G: {G_Byte}, B: {B_Byte}, A: {A_Byte})";
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

    public static Color TRANSPARENT => new(0x0);
    public static Color ALICE_BLUE => new(0xF0F8FFFF);
    public static Color ANTIQUE_WHITE => new(0xFAEBD7FF);
    public static Color AQUA => new(0x00FFFFFF);
    public static Color AQUAMARINE => new(0x7FFFD4FF);
    public static Color AZURE => new(0xF0FFFFFF);
    public static Color BEIGE => new(0xF5F5DCFF);
    public static Color BISQUE => new(0xFFE4C4FF);
    public static Color BLACK => new(0x000000FF);
    public static Color BLANCHED_ALMOND => new(0xFFEBCDFF);
    public static Color BLUE => new(0x0000FFFF);
    public static Color BLUE_VIOLET => new(0x8A2BE2FF);
    public static Color BROWN => new(0xA52A2AFF);
    public static Color BURLY_WOOD => new(0xDEB887FF);
    public static Color CADET_BLUE => new(0x5F9EA0FF);
    public static Color CHARTREUSE => new(0x7FFF00FF);
    public static Color CHOCOLATE => new(0xD2691EFF);
    public static Color CORAL => new(0xFF7F50FF);
    public static Color CORNFLOWER_BLUE => new(0x6495EDFF);
    public static Color CORNSILK => new(0xFFF8DCFF);
    public static Color CRIMSON => new(0xDC143CFF);
    public static Color CYAN => new(0x00FFFFFF);
    public static Color DARK_BLUE => new(0x00008BFF);
    public static Color DARK_CYAN => new(0x008B8BFF);
    public static Color DARK_GOLDEN_ROD => new(0xB8860BFF);
    /// <summary>
    /// A medium gray color (50% brightness).
    /// </summary>
    public static Color GRAY => new(0x808080FF);
    /// <summary>
    /// A dark gray color (25% brightness).
    /// </summary>
    public static Color DARK_GRAY => new(0x404040FF);
    public static Color DARK_GREEN => new(0x006400FF);
    public static Color DARK_KHAKI => new(0xBDB76BFF);
    public static Color DARK_MAGENTA => new(0x8B008BFF);
    public static Color DARK_OLIVE_GREEN => new(0x556B2FFF);
    public static Color DARK_ORANGE => new(0xFF8C00FF);
    public static Color DARK_ORCHID => new(0x9932CCFF);
    public static Color DARK_RED => new(0x8B0000FF);
    public static Color DARK_SALMON => new(0xE9967AFF);
    public static Color DARK_SEA_GREEN => new(0x8FBC8FFF);
    public static Color DARK_SLATE_BLUE => new(0x483D8BFF);
    public static Color DARK_SLATE_GRAY => new(0x2F4F4FFF);
    public static Color DARK_TURQUOISE => new(0x00CED1FF);
    public static Color DARK_VIOLET => new(0x9400D3FF);
    public static Color DEEP_PINK => new(0xFF1493FF);
    public static Color DEEP_SKY_BLUE => new(0x00BFFFFF);
    public static Color DIM_GRAY => new(0x696969FF);
    public static Color DODGER_BLUE => new(0x1E90FFFF);
    public static Color FIRE_BRICK => new(0xB22222FF);
    public static Color FLORAL_WHITE => new(0xFFFAF0FF);
    public static Color FOREST_GREEN => new(0x228B22FF);
    public static Color FUCHSIA => new(0xFF00FFFF);
    public static Color GAINSBORO => new(0xDCDCDCFF);
    public static Color GHOST_WHITE => new(0xF8F8FFFF);
    public static Color GOLD => new(0xFFD700FF);
    public static Color GOLDEN_ROD => new(0xDAA520FF);
    public static Color GREEN => new(0x008000FF);
    public static Color GREEN_YELLOW => new(0xADFF2FFF);
    public static Color HONEY_DEW => new(0xF0FFF0FF);
    public static Color HOT_PINK => new(0xFF69B4FF);
    public static Color INDIAN_RED => new(0xCD5C5CFF);
    public static Color INDIGO => new(0x4B0082FF);
    public static Color IVORY => new(0xFFFFF0FF);
    public static Color KHAKI => new(0xF0E68CFF);
    public static Color LAVENDER => new(0xE6E6FAFF);
    public static Color LAVENDER_BLUSH => new(0xFFF0F5FF);
    public static Color LAWN_GREEN => new(0x7CFC00FF);
    public static Color LEMON_CHIFFON => new(0xFFFACDFF);
    public static Color LIGHT_BLUE => new(0xADD8E6FF);
    public static Color LIGHT_CORAL => new(0xF08080FF);
    public static Color LIGHT_CYAN => new(0xE0FFFFFF);
    public static Color LIGHT_GOLDEN_ROD_YELLOW => new(0xFAFAD2FF);
    /// <summary>
    /// A light gray color (75% brightness).
    /// </summary>
    public static Color LIGHT_GRAY => new(0xD3D3D3FF);
    public static Color LIGHT_GREEN => new(0x90EE90FF);
    public static Color LIGHT_PINK => new(0xFFB6C1FF);
    public static Color LIGHT_SALMON => new(0xFFA07AFF);
    public static Color LIGHT_SEA_GREEN => new(0x20B2AAFF);
    public static Color LIGHT_SKY_BLUE => new(0x87CEFAFF);
    public static Color LIGHT_SLATE_GRAY => new(0x778899FF);
    public static Color LIGHT_STEEL_BLUE => new(0xB0C4DEFF);
    public static Color LIGHT_YELLOW => new(0xFFFFE0FF);
    public static Color LIME => new(0x00FF00FF);
    public static Color LIME_GREEN => new(0x32CD32FF);
    public static Color LINEN => new(0xFAF0E6FF);
    public static Color MAGENTA => new(0xFF00FFFF);
    public static Color MAROON => new(0x800000FF);
    public static Color MEDIUM_AQUA_MARINE => new(0x66CDAAFF);
    public static Color MEDIUM_BLUE => new(0x0000CDFF);
    public static Color MEDIUM_ORCHID => new(0xBA55D3FF);
    public static Color MEDIUM_PURPLE => new(0x9370DBFF);
    public static Color MEDIUM_SEA_GREEN => new(0x3CB371FF);
    public static Color MEDIUM_SLATE_BLUE => new(0x7B68EEFF);
    public static Color MEDIUM_SPRING_GREEN => new(0x00FA9AFF);
    public static Color MEDIUM_TURQUOISE => new(0x48D1CCFF);
    public static Color MEDIUM_VIOLET_RED => new(0xC71585FF);
    public static Color MIDNIGHT_BLUE => new(0x191970FF);
    public static Color MINT_CREAM => new(0xF5FFFAFF);
    public static Color MISTY_ROSE => new(0xFFE4E1FF);
    public static Color MOCCASIN => new(0xFFE4B5FF);
    public static Color NAVAJO_WHITE => new(0xFFDEADFF);
    public static Color NAVY => new(0x000080FF);
    public static Color OLD_LACE => new(0xFDF5E6FF);
    public static Color OLIVE => new(0x808000FF);
    public static Color OLIVE_DRAB => new(0x6B8E23FF);
    public static Color ORANGE => new(0xFFA500FF);
    public static Color ORANGE_RED => new(0xFF4500FF);
    public static Color ORCHID => new(0xDA70D6FF);
    public static Color PALE_GOLDEN_ROD => new(0xEEE8AAFF);
    public static Color PALE_GREEN => new(0x98FB98FF);
    public static Color PALE_TURQUOISE => new(0xAFEEEEFF);
    public static Color PALE_VIOLET_RED => new(0xDB7093FF);
    public static Color PAPAYA_WHIP => new(0xFFEFD5FF);
    public static Color PEACH_PUFF => new(0xFFDAB9FF);
    public static Color PERU => new(0xCD853FFF);
    public static Color PINK => new(0xFFC0CBFF);
    public static Color PLUM => new(0xDDA0DDFF);
    public static Color POWDER_BLUE => new(0xB0E0E6FF);
    public static Color PURPLE => new(0x800080FF);
    public static Color RED => new(0xFF0000FF);
    public static Color ROSY_BROWN => new(0xBC8F8FFF);
    public static Color ROYAL_BLUE => new(0x4169E1FF);
    public static Color SADDLE_BROWN => new(0x8B4513FF);
    public static Color SALMON => new(0xFA8072FF);
    public static Color SANDY_BROWN => new(0xF4A460FF);
    public static Color SEA_GREEN => new(0x2E8B57FF);
    public static Color SEA_SHELL => new(0xFFF5EEFF);
    public static Color SIENNA => new(0xA0522DFF);
    public static Color SILVER => new(0xC0C0C0FF);
    public static Color SKY_BLUE => new(0x87CEEBFF);
    public static Color SLATE_BLUE => new(0x6A5ACDFF);
    public static Color SLATE_GRAY => new(0x708090FF);
    public static Color SNOW => new(0xFFFAFAFF);
    public static Color SPRING_GREEN => new(0x00FF7FFF);
    public static Color STEEL_BLUE => new(0x4682B4FF);
    public static Color TAN => new(0xD2B48CFF);
    public static Color TEAL => new(0x008080FF);
    public static Color THISTLE => new(0xD8BFD8FF);
    public static Color TOMATO => new(0xFF6347FF);
    public static Color TURQUOISE => new(0x40E0D0FF);
    public static Color VIOLET => new(0xEE82EEFF);
    public static Color WHEAT => new(0xF5DEB3FF);
    public static Color WHITE => new(0xFFFFFFFF);
    public static Color WHITE_SMOKE => new(0xF5F5F5FF);
    public static Color YELLOW => new(0xFFFF00FF);
    public static Color YELLOW_GREEN => new(0x9ACD32FF);
    #endregion
}