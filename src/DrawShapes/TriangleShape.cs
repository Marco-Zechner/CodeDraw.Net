using MarcoZechner.Math;
using SkiaSharp;

namespace MarcoZechner.CodeDrawDotNet;

public record TriangleShape(Vector2 Point1, Vector2 Point2, Vector2 Point3) : IDrawShape
{
    public void Draw(SKCanvas canvas, SKPaint paint)
    {
        var path = new SKPath();
        path.MoveTo(Point1.X, Point1.Y);
        path.LineTo(Point2.X, Point2.Y);
        path.LineTo(Point3.X, Point3.Y);
        path.Close();
        canvas.DrawPath(path, paint);
    }
}