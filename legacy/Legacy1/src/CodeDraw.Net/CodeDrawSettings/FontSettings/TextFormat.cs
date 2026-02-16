namespace Legacy1.MarcoZechner.CodeDrawDotNet.CodeDrawSettings.FontSettings;

public class TextFormat {
    public string FontFamily { get; set; } = "Arial";
    public int FontSize { get; set; } = 12;
    public FontStyle FontStyle { get; set; } = FontStyle.REGULAR;
    public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.LEFT;
    public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.TOP;

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