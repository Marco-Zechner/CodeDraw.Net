using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer;

public sealed unsafe partial class CodeDrawLayer
{
    // ---------- internal command ----------
    private sealed class CmdBlit : ICmd
    {
        public CodeDrawLayer? Src;

        public RectF SrcRectPx;
        public RectF DstRectPx;

        // Optional per-draw blend override
        public bool HasBlendOverride;
        public BlendMode BlendOverride;

        public void Exec(GL gl, CodeDrawLayer self)
        {
            var src = Src;
            if (src is null || src._disposed) return;
            if (!src.TryGetLatest(out var tex, out var sw, out var sh, out _, out _)) return;
            if (tex == 0 || sw <= 0 || sh <= 0) return;

            if (DstRectPx.IsEmpty || SrcRectPx.IsEmpty) return;

            // Clamp src rect to src bounds (hard clamp: this avoids sampling outside)
            var sx = MathF.Max(0, SrcRectPx.X);
            var sy = MathF.Max(0, SrcRectPx.Y);
            var sx2 = MathF.Min(sw, SrcRectPx.X2);
            var sy2 = MathF.Min(sh, SrcRectPx.Y2);
            var cw = sx2 - sx;
            var ch = sy2 - sy;
            if (cw <= 0 || ch <= 0) return;

            var u0 = sx / sw;
            var v0 = 1f - ((sy + ch) / sh);
            var du = cw / sw;
            var dv = ch / sh;

            // Blend override (scoped)
            var oldBlend = self._blendMode;
            if (HasBlendOverride)
            {
                self._blendMode = BlendOverride;
                self.ApplyBlendMode();
            }

            gl.UseProgram(self._progLayerRect);
            gl.BindVertexArray(self._vao);

            gl.ActiveTexture(GLEnum.Texture0);
            gl.BindTexture(GLEnum.Texture2D, tex);
            if (self._uLayerRectTex >= 0) gl.Uniform1(self._uLayerRectTex, 0);

            Uniform4F(gl, self._uLayerRectDstRectPx, DstRectPx.X, DstRectPx.Y, DstRectPx.W, DstRectPx.H);
            Uniform2F(gl, self._uLayerRectDstResPx, self._w, self._h);
            Uniform4F(gl, self._uLayerRectSrcUvRect, u0, v0, du, dv);

            gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, null);

            gl.BindTexture(GLEnum.Texture2D, 0);
            gl.BindVertexArray(0);
            gl.UseProgram(0);

            if (!HasBlendOverride) return;
            self._blendMode = oldBlend;
            self.ApplyBlendMode();
        }
    }

    // ---------- stages ----------
    public readonly struct BlitSrcStage
    {
        private readonly CodeDrawLayer _dst;
        private readonly CodeDrawLayer _src;

        private readonly RectF _srcRectPx; // crop in src pixels

        internal BlitSrcStage(CodeDrawLayer dst, CodeDrawLayer src)
        {
            _dst = dst;
            _src = src;
            _srcRectPx = new RectF(0, 0, src._w, src._h);
        }

        private BlitSrcStage(CodeDrawLayer dst, CodeDrawLayer src, RectF srcRect)
        {
            _dst = dst;
            _src = src;
            _srcRectPx = srcRect;
        }

        public BlitSrcStage Crop(RectF srcRectPx) => new(_dst, _src, srcRectPx);

        // “FitToTarget” shortcut (dstRect = full dst)
        public BlitDstStage FitToTarget() => new(_dst, _src, _srcRectPx, _dst.FullRect);

        public BlitDstStage Place(RectF dstRectPx) => new(_dst, _src, _srcRectPx, dstRectPx);
    }

    public readonly struct BlitDstStage
    {
        private readonly CodeDrawLayer _dst;
        private readonly CodeDrawLayer _src;

        private readonly RectF _srcRectPx;
        private readonly RectF _dstRectPx;

        private readonly bool _hasBlendOverride;
        private readonly BlendMode _blendOverride;

        internal BlitDstStage(CodeDrawLayer dst, CodeDrawLayer src, RectF srcRect, RectF dstRect)
        {
            _dst = dst;
            _src = src;
            _srcRectPx = srcRect;
            _dstRectPx = dstRect;
            _hasBlendOverride = false;
            _blendOverride = default;
        }

        private BlitDstStage(CodeDrawLayer dst, CodeDrawLayer src, RectF srcRect, RectF dstRect, bool hasBlend, BlendMode mode)
        {
            _dst = dst;
            _src = src;
            _srcRectPx = srcRect;
            _dstRectPx = dstRect;
            _hasBlendOverride = hasBlend;
            _blendOverride = mode;
        }

        public BlitDstStage Blend(BlendMode mode) => new(_dst, _src, _srcRectPx, _dstRectPx, true, mode);

        public void Draw()
        {
            if (_dst._disposed) return;
            if (_src._disposed) return;

            _dst.Enqueue(new CmdBlit
            {
                Src = _src,
                SrcRectPx = _srcRectPx,
                DstRectPx = _dstRectPx,
                HasBlendOverride = _hasBlendOverride,
                BlendOverride = _blendOverride
            });
        }

        public bool TryTransformPointFromDstToSrc(float dstX, float dstY, out float srcX, out float srcY)
        {
            srcX = srcY = 0;

            // outside dst rect -> no mapping
            if (dstX < _dstRectPx.X || dstX > _dstRectPx.X2 ||
                dstY < _dstRectPx.Y || dstY > _dstRectPx.Y2)
                return false;

            float du = _dstRectPx.W;
            float dv = _dstRectPx.H;
            if (du == 0 || dv == 0) return false;

            float lx = (dstX - _dstRectPx.X) / du; // 0..1
            float ly = (dstY - _dstRectPx.Y) / dv; // 0..1

            srcX = _srcRectPx.X + lx * _srcRectPx.W;
            srcY = _srcRectPx.Y + ly * _srcRectPx.H;
            return true;
        }

        public bool TryTransformPointFromSrcToDst(float srcX, float srcY, out float dstX, out float dstY)
        {
            dstX = dstY = 0;

            // outside src rect -> no mapping
            if (srcX < _srcRectPx.X || srcX > _srcRectPx.X2 ||
                srcY < _srcRectPx.Y || srcY > _srcRectPx.Y2)
                return false;

            float su = _srcRectPx.W;
            float sv = _srcRectPx.H;
            if (su == 0 || sv == 0) return false;

            float lx = (srcX - _srcRectPx.X) / su; // 0..1
            float ly = (srcY - _srcRectPx.Y) / sv; // 0..1

            dstX = _dstRectPx.X + lx * _dstRectPx.W;
            dstY = _dstRectPx.Y + ly * _dstRectPx.H;
            return true;
        }
    }
}
