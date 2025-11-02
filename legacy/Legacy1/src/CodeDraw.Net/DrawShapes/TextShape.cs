using MarcoZechner.MathDotNet;
using SkiaSharp;
using Topten.RichTextKit;

namespace MarcoZechner.CodeDrawDotNet;

public record TextShape(Vector2 Position, string Text, TextFormat Format) : IDrawShape
{
    public void Draw(SKCanvas canvas, SKPaint paint)
    {
        var rs = new RichString() {
            DefaultStyle = new Style() {
                FontFamily = Format.FontFamily,
                FontSize = Format.FontSize,
                FontItalic = (Format.FontStyle & FontStyle.Italic) == FontStyle.Italic,
                StrikeThrough = (Format.FontStyle & FontStyle.Strikeout) == FontStyle.Strikeout ? StrikeThroughStyle.Solid : StrikeThroughStyle.None,
                Underline = (Format.FontStyle & FontStyle.Underline) == FontStyle.Underline ? UnderlineStyle.Solid : UnderlineStyle.None,
                TextColor = paint.Color,
            },
        }
        .Add(Text)
        .Bold((Format.FontStyle & FontStyle.Bold) == FontStyle.Bold);

        float height = rs.MeasuredHeight;
        float width = rs.MeasuredWidth;

        float xOffset = Format.HorizontalAlignment switch
        {
            HorizontalAlignment.Left => 0,
            HorizontalAlignment.Center => width/2,
            HorizontalAlignment.Right => width,
            _ => 0,
        };

        float yOffset = Format.VerticalAlignment switch
        {
            VerticalAlignment.Top => 0,
            VerticalAlignment.Middle => height/2,
            VerticalAlignment.Bottom => height,
            _ => 0,
        };

        rs.Paint(canvas, new SKPoint(Position.X - xOffset, Position.Y - yOffset));
    }
}