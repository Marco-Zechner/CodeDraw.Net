using SkiaSharp;

namespace Legacy1.MarcoZechner.CodeDrawDotNet.DrawShapes;

public interface IDrawShape {
    void Draw(SKCanvas canvas, SKPaint paint);
}