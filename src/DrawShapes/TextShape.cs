using MarcoZechner.Math;
using SkiaSharp;
using Topten.RichTextKit;

namespace MarcoZechner.CodeDraw.Net;

public record TextShape(Vector2 Position, string Text, TextFormat Format) : IDrawShape
{
    public void Draw(SKCanvas canvas, SKPaint paint)
    {
        var rs = new RichString() {
            DefaultAlignment = Format.HorizontalAlignment switch
            {
                HorizontalAlignment.Left => TextAlignment.Left,
                HorizontalAlignment.Center => TextAlignment.Center,
                HorizontalAlignment.Right => TextAlignment.Right,
                _ => TextAlignment.Left
            },
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
        rs.Paint(canvas, new SKPoint(Position.X, Position.Y));
    }
}