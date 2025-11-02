using SkiaSharp;

namespace MarcoZechner.CodeDrawDotNet;

public interface IDrawShape {
    void Draw(SKCanvas canvas, SKPaint paint);
}