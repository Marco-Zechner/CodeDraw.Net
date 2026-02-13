using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Window;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer;

public sealed partial class CodeDrawLayer
{
    /// <summary>
    /// If called multiple times during one frame, only the last size is applied.
    /// Warning: multiple windows can share the same CodeDrawLayer instance, so it might be easy to accidentally
    /// resize a layer that is in use by another window.
    /// </summary>
    /// <param name="w"></param>
    /// <param name="h"></param>
    public void RequestLayerSize(int w, int h)
    {
        if (_disposed) return;
        if (w <= 0 || h <= 0) return;
        if (w == _w && h == _h) return;

        Enqueue(new CmdResize(w, h));
        Render();
    }

    public void Render()
    {
        if (_disposed) return;

        var targetSeq = Volatile.Read(ref _lastEnqueuedSeq);
        RequestRenderTo(targetSeq, wait: true, timeoutMs: Timeout.Infinite);
    }

    /// <summary>
    /// SOURCE_OVER_ALPHA is the default blend mode.
    /// </summary>
    /// <param name="mode"></param>
    public void SetBlendMode(BlendMode mode) => Enqueue(new CmdSetBlendMode { Mode = mode });

    public void Clear(float r = 0f, float g = 0, float b = 0f, float a = 0f) => Enqueue(new CmdClear(r, g, b, a));

    public void DrawRect(float x, float y, float w, float h, float r, float g, float b, float a)
        => Enqueue(new CmdRect { X = x, Y = y, W = w, H = h, R = r, G = g, B = b, A = a });


    public void DrawLayer(CodeDrawLayer src, CustomShader? shader = null)
    {
        if (shader is not null)
            ScheduleExternalShader(shader);

        Enqueue(new CmdLayer { Src = src, Shader = shader });
    }

    public void DrawLayer(CodeDrawLayer src, RectF dstRect)
        => Blit(src).Place(dstRect).Draw();

    public void DrawLayer(CodeDrawLayer src, RectF dstRect, BlendMode blend)
        => Blit(src).Place(dstRect).Blend(blend).Draw();

    public void DrawLayer(CodeDrawLayer src, RectF srcRect, bool fitToTarget)
        => Blit(src).Crop(srcRect).Place(fitToTarget ? FullRect : new RectF(0, 0, srcRect.W, srcRect.H)).Draw();

    public void DrawLayer(CodeDrawLayer src, RectF srcRect, RectF dstRect)
        => Blit(src).Crop(srcRect).Place(dstRect).Draw();



    public BlitSrcStage Blit(CodeDrawLayer src) => new(this, src);


    public void CustomDrawRect(
        int x, int y, int w, int h,
        CustomShader shader,
        Uniforms uniforms)
    {
        if (_disposed) return;

        // Ensure shader is registered before CheckHotReload in the next DrainUntil()
        ScheduleExternalShader(shader);

        // Defensive copy: keep cmd immutable even if caller reuses the array.
        var src = uniforms.Values;
        var copy = (src.Length == 0) ? [] : (UniformValue[])src.Clone();

        Enqueue(new CmdCustomRect
        {
            X = x, Y = y, W = w, H = h,
            Shader = shader,
            Uniforms = new Uniforms(copy)
        });
    }

    public void CustomDrawRect(
        int x, int y, int w, int h,
        CustomShader shader,
        params UniformValue[] uniforms)
        => CustomDrawRect(x,y,w,h,shader, new Uniforms(uniforms));

    #region Transform Point Helpers

    public (float x, float y) TransformPointFrom(CodeDrawWindow win, float winX, float winY)
    {
        var ww = win.Width;
        var wh = win.Height;

        if (ww <= 0 || wh <= 0 || _w <= 0 || _h <= 0) return (0, 0);

        float sx = _w / (float)ww;
        float sy = _h / (float)wh;
        return (winX * sx, winY * sy);
    }

    public (float x, float y) TransformPointTo(CodeDrawWindow win, float layerX, float layerY)
    {
        var ww = win.Width;
        var wh = win.Height;

        if (ww <= 0 || wh <= 0 || _w <= 0 || _h <= 0) return (0, 0);

        float sx = ww / (float)_w;
        float sy = wh / (float)_h;
        return (layerX * sx, layerY * sy);
    }

    public bool TransformLayerPointFrom(
        CodeDrawLayer src,
        RectF dstRectPx,
        float srcX, float srcY,
        out float dstX, out float dstY)
    {
        // src full rect
        var srcRect = new RectF(0, 0, src._w, src._h);
        return TransformLayerPointFrom(src, srcRect, dstRectPx, srcX, srcY, out dstX, out dstY);
    }

    public bool TransformLayerPointTo(
        CodeDrawLayer src,
        RectF dstRectPx,
        float dstX, float dstY,
        out float srcX, out float srcY)
    {
        var srcRect = new RectF(0, 0, src._w, src._h);
        return TransformLayerPointTo(src, srcRect, dstRectPx, dstX, dstY, out srcX, out srcY);
    }

    public bool TransformLayerPointFrom(
        CodeDrawLayer src,
        RectF srcRectPx,
        bool fitToTarget,
        float srcX, float srcY,
        out float dstX, out float dstY)
    {
        var dstRectPx = fitToTarget ? FullRect : new RectF(0, 0, srcRectPx.W, srcRectPx.H);
        return TransformLayerPointFrom(src, srcRectPx, dstRectPx, srcX, srcY, out dstX, out dstY);
    }

    public bool TransformLayerPointTo(
        CodeDrawLayer src,
        RectF srcRectPx,
        bool fitToTarget,
        float dstX, float dstY,
        out float srcX, out float srcY)
    {
        var dstRectPx = fitToTarget ? FullRect : new RectF(0, 0, srcRectPx.W, srcRectPx.H);
        return TransformLayerPointTo(src, srcRectPx, dstRectPx, dstX, dstY, out srcX, out srcY);
    }

    public static bool TransformLayerPointFrom(
        CodeDrawLayer src,
        RectF srcRectPx,
        RectF dstRectPx,
        float srcX, float srcY,
        out float dstX, out float dstY)
    {
        dstX = dstY = 0;

        if (srcRectPx.IsEmpty || dstRectPx.IsEmpty) return false;

        // outside srcRect => no mapping
        if (srcX < srcRectPx.X || srcX > srcRectPx.X2 ||
            srcY < srcRectPx.Y || srcY > srcRectPx.Y2)
            return false;

        float su = srcRectPx.W;
        float sv = srcRectPx.H;
        if (su == 0 || sv == 0) return false;

        float lx = (srcX - srcRectPx.X) / su; // 0..1
        float ly = (srcY - srcRectPx.Y) / sv; // 0..1

        dstX = dstRectPx.X + lx * dstRectPx.W;
        dstY = dstRectPx.Y + ly * dstRectPx.H;
        return true;
    }

    public static bool TransformLayerPointTo(
        CodeDrawLayer src,
        RectF srcRectPx,
        RectF dstRectPx,
        float dstX, float dstY,
        out float srcX, out float srcY)
    {
        srcX = srcY = 0;

        if (srcRectPx.IsEmpty || dstRectPx.IsEmpty) return false;

        // outside dstRect => no mapping
        if (dstX < dstRectPx.X || dstX > dstRectPx.X2 ||
            dstY < dstRectPx.Y || dstY > dstRectPx.Y2)
            return false;

        float du = dstRectPx.W;
        float dv = dstRectPx.H;
        if (du == 0 || dv == 0) return false;

        float lx = (dstX - dstRectPx.X) / du; // 0..1
        float ly = (dstY - dstRectPx.Y) / dv; // 0..1

        srcX = srcRectPx.X + lx * srcRectPx.W;
        srcY = srcRectPx.Y + ly * srcRectPx.H;
        return true;
    }


    #endregion
}