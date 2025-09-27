using SkiaSharp;

namespace MarcoZechner.CodeDraw.Net;

public class DrawQueue {
    private readonly List<IDrawShape> _drawQueue = [];
    private readonly List<ShapeSettings> _settingsQueue = [];
    public int Count => _drawQueue.Count;

    private bool _waitTillNextFrame = false;

    public void Enqueue(IDrawShape shape, ShapeSettings settings, bool waitTillNextFrame = false) {
        _drawQueue.Add(shape);
        _settingsQueue.Add(settings);
        while (_waitTillNextFrame) Thread.Sleep(1);
    }

    public void DequeueInto(DrawQueue target) {
        for (int i = 0; i < _drawQueue.Count; i++) {
            var shape = _drawQueue[i];
            var settings = _settingsQueue[i];
            target.Enqueue(shape, settings);
        }
        Clear();
    }

    public void Draw(SKCanvas canvas) {
        for (int i = 0; i < _drawQueue.Count; i++) {
            var shape = _drawQueue[i];
            var settings = _settingsQueue[i];
            var isFill = settings.IsFill;
            SKStrokeCap cap = settings.CornerStyle switch {
                CornerStyle.SHARP => SKStrokeCap.Square,
                CornerStyle.ROUND => SKStrokeCap.Round,
                CornerStyle.BEVEL => SKStrokeCap.Butt,
                _ => SKStrokeCap.Butt
            };
            SKStrokeJoin join = settings.CornerStyle switch {
                CornerStyle.SHARP => SKStrokeJoin.Miter,
                CornerStyle.ROUND => SKStrokeJoin.Round,
                CornerStyle.BEVEL => SKStrokeJoin.Bevel,
                _ => SKStrokeJoin.Miter
            };
            int cornerRadius = settings.CornerRadius; //TODO: implement corner radius for shapes that support it
            shape.Draw(canvas, new SKPaint()
            {
                Color = settings.DrawColor,
                Style = isFill ? SKPaintStyle.StrokeAndFill : SKPaintStyle.Stroke,
                StrokeWidth = settings.LineWidth,
                IsAntialias = settings.IsAntiAliased,
                StrokeCap = cap,
                StrokeJoin = join,
            });
        } 
        _waitTillNextFrame = false;
    }

    public void Clear() {
        _drawQueue.Clear();
        _settingsQueue.Clear();
    }
}