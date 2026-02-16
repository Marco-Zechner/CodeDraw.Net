using Legacy1.MarcoZechner.CodeDrawDotNet.CodeDrawSettings.FontSettings;
using MarcoZechner.MathDotNet;
using SkiaSharp;
using Topten.RichTextKit;

namespace Legacy1.MarcoZechner.CodeDrawDotNet.DrawShapes;

public record TextShape(Vector2 Position, string Text, TextFormat Format) : IDrawShape
{
    public void Draw(SKCanvas canvas, SKPaint paint)
    {
        var rs = new RichString() {
            DefaultStyle = new Style() {
                FontFamily = Format.FontFamily,
                FontSize = Format.FontSize,
                FontItalic = (Format.FontStyle & FontStyle.ITALIC) == FontStyle.ITALIC,
                StrikeThrough = (Format.FontStyle & FontStyle.STRIKEOUT) == FontStyle.STRIKEOUT ? StrikeThroughStyle.Solid : StrikeThroughStyle.None,
                Underline = (Format.FontStyle & FontStyle.UNDERLINE) == FontStyle.UNDERLINE ? UnderlineStyle.Solid : UnderlineStyle.None,
                TextColor = paint.Color,
            },
        }
        .Add(Text)
        .Bold((Format.FontStyle & FontStyle.BOLD) == FontStyle.BOLD);

        float height = rs.MeasuredHeight;
        float width = rs.MeasuredWidth;

        float xOffset = Format.HorizontalAlignment switch
        {
            HorizontalAlignment.LEFT => 0,
            HorizontalAlignment.CENTER => width/2,
            HorizontalAlignment.RIGHT => width,
            _ => 0,
        };

        float yOffset = Format.VerticalAlignment switch
        {
            VerticalAlignment.TOP => 0,
            VerticalAlignment.MIDDLE => height/2,
            VerticalAlignment.BOTTOM => height,
            _ => 0,
        };

        rs.Paint(canvas, new SKPoint(Position.X - xOffset, Position.Y - yOffset));
    }
}