namespace MarcoZechner.CodeDrawDotNet;

public class TextFormat {
    public string FontFamily { get; set; } = "Arial";
    public int FontSize { get; set; } = 12;
    public FontStyle FontStyle { get; set; } = FontStyle.Regular;
    public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;
    public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Top;

    public TextFormat() {
        
    }

    public TextFormat(TextFormat original) {
        FontFamily = original.FontFamily;
        FontSize = original.FontSize;
        FontStyle = original.FontStyle;
        HorizontalAlignment = original.HorizontalAlignment;
        VerticalAlignment = original.VerticalAlignment;
    }
}