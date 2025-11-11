using SkiaSharp;

namespace MarcoZechner.CodeDrawDotNet.DrawShapes;

public interface IDrawShape {
    void Draw(SKCanvas canvas, SKPaint paint);
}