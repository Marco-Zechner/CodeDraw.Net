using MarcoZechner.CodeDrawDotNet.Api.Graphics.Actions;
using MarcoZechner.CodeDrawDotNet.Interfaces;
using MarcoZechner.ColorDotNet;

namespace MarcoZechner.CodeDrawDotNet.Api;

public partial class CodeDrawWindowBase
{
    // Fill a rectangle in pixel coords (top-left origin), solid color.
    public void FillRect(float x, float y, float w, float h, in Color c)
    {
        float r = c.R, g = c.G, b = c.B, a = c.A;

        float x0 = x,     y0 = y;
        float x1 = x + w, y1 = y + h;

        var data = new float[]
        {
            x0, y0, r,g,b,a,
            x1, y0, r,g,b,a,
            x1, y1, r,g,b,a,

            x0, y0, r,g,b,a,
            x1, y1, r,g,b,a,
            x0, y1, r,g,b,a,
        };

        _renderer?.Enqueue(new DrawTriangles2DAction(data, vertexCount: 6));
    }

    public void FillCircle(float xCenter, float yCenter, float radius, in Color c, int segments = 64)
    {
        if (segments < 3)
            segments = 3;

        float r = c.R, g = c.G, b = c.B, a = c.A;

        // Triangle fan: center + segments around
        // vertex count = segments * 3 (each segment = 1 triangle)
        var data = new float[segments * 3 * 6]; // 3 verts * (x,y,r,g,b,a)

        var i = 0;
        var angleStep = MathF.Tau / segments;

        for (var s = 0; s < segments; s++)
        {
            var a0 = s * angleStep;
            var a1 = (s + 1) * angleStep;

            var x0 = xCenter + MathF.Cos(a0) * radius;
            var y0 = yCenter + MathF.Sin(a0) * radius;

            var x1 = xCenter + MathF.Cos(a1) * radius;
            var y1 = yCenter + MathF.Sin(a1) * radius;

            // Triangle: center → p0 → p1

            // center
            data[i++] = xCenter;
            data[i++] = yCenter;
            data[i++] = r;
            data[i++] = g;
            data[i++] = b;
            data[i++] = a;

            // p0
            data[i++] = x0;
            data[i++] = y0;
            data[i++] = r;
            data[i++] = g;
            data[i++] = b;
            data[i++] = a;

            // p1
            data[i++] = x1;
            data[i++] = y1;
            data[i++] = r;
            data[i++] = g;
            data[i++] = b;
            data[i++] = a;
        }

        _renderer?.Enqueue(new DrawTriangles2DAction(data, vertexCount: segments * 3));
    }

    public void DrawLayer(ILayerHandle layer, bool premultiply = false)
    {
        _renderer?.Enqueue(new DrawLayerCommand(layer, premultiply));
    }
}