using MarcoZechner.Math;
using SkiaSharp;

namespace MarcoZechner.CodeDrawDotNet;

public record PieShape(Vector2 Center, float Radius, float StartAngle, float SweepAngle, AngleUnit AngleUnit = AngleUnit.Degrees) : IDrawShape
{
    public void Draw(SKCanvas canvas, SKPaint paint)
    {
        float startAngle = AngleUnit == AngleUnit.Degrees ? StartAngle : StartAngle * (180f / MathF.PI);
        float sweepAngle = AngleUnit == AngleUnit.Degrees ? SweepAngle : SweepAngle * (180f / MathF.PI);
        var rect = new SKRect(Center.X - Radius, Center.Y - Radius, Center.X + Radius, Center.Y + Radius);
        canvas.DrawArc(rect, startAngle, sweepAngle, false, paint);
    }
}