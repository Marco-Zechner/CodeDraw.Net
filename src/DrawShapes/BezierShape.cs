using MarcoZechner.MathDotNet;
using SkiaSharp;

namespace MarcoZechner.CodeDrawDotNet;

public record BezierShape(Vector2 Start, Vector2 Control1, Vector2 Control2, Vector2 End) : IDrawShape
{
    public void Draw(SKCanvas canvas, SKPaint paint)
    {
        using var path = new SKPath();
        path.MoveTo(Start.X, Start.Y);
        path.CubicTo(Control1.X, Control1.Y, Control2.X, Control2.Y, End.X, End.Y);
        canvas.DrawPath(path, paint);
    }
}