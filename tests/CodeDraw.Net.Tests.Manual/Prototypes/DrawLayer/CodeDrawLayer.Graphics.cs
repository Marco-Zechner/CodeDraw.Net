namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer;

public sealed unsafe partial class CodeDrawLayer
{
    public void DrawLayer(DrawLayer.CodeDrawLayer src) => Enqueue(new CmdLayer { Src = src });

    public void DrawLayer(DrawLayer.CodeDrawLayer src, RectF dstRect)
        => Blit(src).Place(dstRect).Draw();

    public void DrawLayer(DrawLayer.CodeDrawLayer src, RectF dstRect, BlendMode blend)
        => Blit(src).Place(dstRect).Blend(blend).Draw();

    public void DrawLayer(DrawLayer.CodeDrawLayer src, RectF srcRect, bool fitToTarget)
    {
        var dst = fitToTarget ? FullRect : new RectF(0, 0, srcRect.W, srcRect.H);
        Blit(src).Crop(srcRect).Place(dst).Draw();
    }

    public void DrawLayer(DrawLayer.CodeDrawLayer src, RectF srcRect, RectF dstRect)
        => Blit(src).Crop(srcRect).Place(dstRect).Draw();

    public BlitSrcStage Blit(DrawLayer.CodeDrawLayer src) => new(this, src);
}