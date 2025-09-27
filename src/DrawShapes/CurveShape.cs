using MarcoZechner.Math;
using SkiaSharp;

namespace MarcoZechner.CodeDraw.Net;

public record CurveShape(Vector2 Start, Vector2 Control, Vector2 End) : IDrawShape
{
    public void Draw(SKCanvas canvas, SKPaint paint)
    {
        using var path = new SKPath();
        path.MoveTo(Start.X, Start.Y);
        path.QuadTo(Control.X, Control.Y, End.X, End.Y);
        canvas.DrawPath(path, paint);
    }
}