using SkiaSharp;

namespace MarcoZechner.CodeDraw.Net;

public interface IDrawShape {
    void Draw(SKCanvas canvas, SKPaint paint);
}