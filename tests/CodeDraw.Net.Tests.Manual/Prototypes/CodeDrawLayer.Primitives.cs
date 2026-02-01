namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;

public sealed unsafe partial class CodeDrawLayer
{
    public readonly record struct RectF(float X, float Y, float W, float H)
    {
        public float X2 => X + W;
        public float Y2 => Y + H;
        public bool IsEmpty => W <= 0 || H <= 0;
    }

    public readonly record struct Rgba(float R, float G, float B, float A);

    public int Width  => _w;
    public int Height => _h;

    public RectF FullRect => new(0, 0, _w, _h);

    // 1) Draw full layer stretched into full destination (current behavior)
    public void DrawLayer(CodeDrawLayer src) => Enqueue(new CmdLayer { Src = src });

    // 2) Draw full layer stretched into dst rect (no crop)
    public void DrawLayer(CodeDrawLayer src, RectF dstRect)
        => Blit(src).Place(dstRect).Draw();

    // 3) Crop source rect and draw into full dst
    public void DrawLayer(CodeDrawLayer src, RectF srcRect, bool fitToTarget = true)
    {
        var dst = fitToTarget ? FullRect : new RectF(0, 0, srcRect.W, srcRect.H);
        Blit(src).Crop(srcRect).Place(dst).Draw();
    }

    // 4) Crop source and place into destination
    public void DrawLayer(CodeDrawLayer src, RectF srcRect, RectF dstRect)
        => Blit(src).Crop(srcRect).Place(dstRect).Draw();

    // Convenience
    public BlitSrcStage Blit(CodeDrawLayer src) => new(this, src);
}