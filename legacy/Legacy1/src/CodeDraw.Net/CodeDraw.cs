using MarcoZechner.ColorDotNet;
using Silk.NET.OpenGL;
using SkiaSharp;

namespace MarcoZechner.CodeDrawDotNet;

public class CodeDraw(string title = "title", bool useManagementEvents = false) : GlfwWindow(title, useManagementEvents)
{
    public readonly Shapes Shapes = new();
    private readonly DrawQueue _drawQueue = new();

    protected override void Render(double dt, SKCanvas canvas, GL gl)
    {
        _drawQueue.Draw(canvas);
    }

    /// <summary>
    /// Pushes all shapes to the draw queue and then waits for the next frame to be rendered.
    /// </summary>
    public override void Show()
    {
        Shapes.DrawBuffer.DequeueInto(_drawQueue);
        if (!UseManagementEvents)
            base.Show();
    }

    public override void Clear(Color? clearColor = null)
    {
        base.Clear(clearColor);
        _drawQueue.Clear();
        Shapes.DrawBuffer.Clear();
    }
}