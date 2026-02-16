using MarcoZechner.MathDotNet;
using SkiaSharp;

namespace Legacy1.MarcoZechner.CodeDrawDotNet.DrawShapes;

public record LineShape(Vector2 Start, Vector2 End) : IDrawShape
{
    public void Draw(SKCanvas canvas, SKPaint paint)
    {
        canvas.DrawLine(Start.X, Start.Y, End.X, End.Y, paint);
    }
}