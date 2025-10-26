using MarcoZechner.Math;
using SkiaSharp;

namespace MarcoZechner.CodeDrawDotNet.Old1;

// Simple image handle you can keep around & reuse
public sealed class ImageHandle : IDisposable
{
    internal readonly SKImage Image;
    public readonly Vector2 NaturalSize;

    internal ImageHandle(SKImage img)
    {
        Image = img;
        NaturalSize = new Vector2(img.Width, img.Height);
    }

    public void Dispose() => Image.Dispose();
}