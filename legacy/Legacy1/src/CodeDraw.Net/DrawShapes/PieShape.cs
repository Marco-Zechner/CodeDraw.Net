using MarcoZechner.MathDotNet;
using SkiaSharp;

namespace Legacy1.MarcoZechner.CodeDrawDotNet.DrawShapes;

public record PieShape(Vector2 Center, float Radius, float StartAngle, float SweepAngle, AngleUnit AngleUnit = AngleUnit.DEGREES) : IDrawShape
{
    public void Draw(SKCanvas canvas, SKPaint paint)
    {
        float startAngle = AngleUnit == AngleUnit.DEGREES ? StartAngle : StartAngle * (180f / MathF.PI);
        float sweepAngle = AngleUnit == AngleUnit.DEGREES ? SweepAngle : SweepAngle * (180f / MathF.PI);
        var rect = new SKRect(Center.X - Radius, Center.Y - Radius, Center.X + Radius, Center.Y + Radius);
        canvas.DrawArc(rect, startAngle, sweepAngle, false, paint);
    }
}