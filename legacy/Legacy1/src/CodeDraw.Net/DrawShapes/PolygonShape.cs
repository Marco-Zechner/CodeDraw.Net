using MarcoZechner.MathDotNet;
using SkiaSharp;

namespace Legacy1.MarcoZechner.CodeDrawDotNet.DrawShapes;

public record PolygonShape(Vector2[] Points) : IDrawShape
{
    public void Draw(SKCanvas canvas, SKPaint paint)
    {
        if (Points.Length < 3) return; // Not a polygon

        var path = new SKPath();
        path.MoveTo(Points[0].X, Points[0].Y);
        for (int i = 1; i < Points.Length; i++)
        {
            path.LineTo(Points[i].X, Points[i].Y);
        }
        path.Close();
        canvas.DrawPath(path, paint);
    }
}