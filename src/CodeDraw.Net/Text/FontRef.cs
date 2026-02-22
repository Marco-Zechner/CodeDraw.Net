namespace MarcoZechner.CodeDrawDotNet.Text;

public enum FontSlant { Normal, Italic }

public readonly record struct FontVariant(
    int Weight,
    FontSlant Slant)
{
    public static FontVariant Regular => new(400, FontSlant.Normal);
    public static FontVariant Bold => new(700, FontSlant.Normal);
    public static FontVariant Italic => new(400, FontSlant.Italic);
    public static FontVariant BoldItalic => new(700, FontSlant.Italic);
}

public readonly record struct FontRef(string Path, FontVariant Variant)
{
    public static FontRef FromFile(string path) => new(path, FontVariant.Regular);
    public FontRef WithVariant(FontVariant v) => new(Path, v);
}