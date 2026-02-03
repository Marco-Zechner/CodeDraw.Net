using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer;

public sealed unsafe partial class CodeDrawLayer
{
    public void EnsureCanvas(int w, int h)
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

        while (true)
        {
            if (Volatile.Read(ref _lastRenderedCmdSeq) >= targetSeq) return;

            var iAmRenderer = false;
            lock (_renderLock)
            {
                if (!_rendering)
                {
                    _rendering = true;
                    iAmRenderer = true;
                }
            }

            if (iAmRenderer)
            {
                try { DrainUntil(targetSeq); }
                finally
                {
                    lock (_renderLock)
                    {
                        _rendering = false;
                        Monitor.PulseAll(_renderLock);
                    }
                }
                return;
            }

            lock (_renderLock)
            {
                while (_rendering && Volatile.Read(ref _lastRenderedCmdSeq) < targetSeq)
                    Monitor.Wait(_renderLock);
            }
        }
    }

    /// <summary>
    /// SOURCE_OVER_ALPHA is the default blend mode.
    /// </summary>
    /// <param name="mode"></param>
    public void SetBlendMode(BlendMode mode) => Enqueue(new CmdSetBlendMode { Mode = mode });
    public void Clear(float r = 0f, float g = 0, float b = 0f, float a = 0f) => Enqueue(new CmdClear(r, g, b, a));

    public void DrawRect(float x, float y, float w, float h, float r, float g, float b, float a)
        => Enqueue(new CmdRect { X = x, Y = y, W = w, H = h, R = r, G = g, B = b, A = a });


    public void DrawLayer(CodeDrawLayer src, LayerCopyShader? shader = null)
    {
        if (shader is not null)
            ScheduleExternalShader(shader);

        Enqueue(new CmdLayer { Src = src, Shader = shader });
    }

    public void DrawLayer(CodeDrawLayer src, RectF dstRect)
    {
        Blit(src).Place(dstRect).Draw();
    }

    public void DrawLayer(CodeDrawLayer src, RectF dstRect, BlendMode blend)
    {
        Blit(src).Place(dstRect).Blend(blend).Draw();
    }

    public void DrawLayer(CodeDrawLayer src, RectF srcRect, bool fitToTarget)
    {
        var dst = fitToTarget ? FullRect : new RectF(0, 0, srcRect.W, srcRect.H);
        Blit(src).Crop(srcRect).Place(dst).Draw();
    }

    public void DrawLayer(CodeDrawLayer src, RectF srcRect, RectF dstRect)
    {
        Blit(src).Crop(srcRect).Place(dstRect).Draw();
    }

    public BlitSrcStage Blit(CodeDrawLayer src) => new(this, src);
}