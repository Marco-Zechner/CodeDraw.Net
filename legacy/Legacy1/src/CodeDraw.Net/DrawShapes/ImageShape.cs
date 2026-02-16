using MarcoZechner.MathDotNet;
using SkiaSharp;

namespace Legacy1.MarcoZechner.CodeDrawDotNet.DrawShapes;

public record ImageShape(SKImage Image, Vector2 Position, Vector2 Size, bool Antialias) : IDrawShape
{
    public void Draw(SKCanvas canvas, SKPaint paint)
    {
        var sampling = new SKSamplingOptions(
            Antialias ? SKFilterMode.Linear : SKFilterMode.Nearest,
            SKMipmapMode.Nearest
        );
        var dest = new SKRect(Position.X, Position.Y, Position.X + Size.X, Position.Y + Size.Y);
        canvas.DrawImage(Image, dest, sampling);
    }
}