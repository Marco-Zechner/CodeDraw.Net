using MarcoZechner.MathDotNet;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer;

public sealed unsafe partial class CodeDrawLayer
{
    // ---------- internal command ----------
    private sealed class CmdBlit : ICmd
    {
        public CodeDrawLayer? Src;

        public Rect SrcRectPx;
        public Rect DstRectPx;

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
            var sx = MathF.Max(0, SrcRectPx.Left);
            var sy = MathF.Max(0, SrcRectPx.Top);
            var sx2 = MathF.Min(sw, SrcRectPx.Right);
            var sy2 = MathF.Min(sh, SrcRectPx.Bottom);
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

            Uniform4F(gl, self._uLayerRectDstRectPx, DstRectPx.Left, DstRectPx.Top, DstRectPx.Width, DstRectPx.Height);
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

        private readonly Rect _srcRectPx; // crop in src pixels

        internal BlitSrcStage(CodeDrawLayer dst, CodeDrawLayer src)
        {
            _dst = dst;
            _src = src;
            _srcRectPx = new Rect(0, 0, src._w, src._h);
        }

        private BlitSrcStage(CodeDrawLayer dst, CodeDrawLayer src, Rect srcRect)
        {
            _dst = dst;
            _src = src;
            _srcRectPx = srcRect;
        }

        public BlitSrcStage Crop(Rect srcRectPx) => new(_dst, _src, srcRectPx);

        // “FitToTarget” shortcut (dstRect = full dst)
        public BlitDstStage FitToTarget() => new(_dst, _src, _srcRectPx, _dst.FullRect);

        public BlitDstStage Place(Rect dstRectPx) => new(_dst, _src, _srcRectPx, dstRectPx);
    }

    public readonly struct BlitDstStage
    {
        private readonly CodeDrawLayer _dst;
        private readonly CodeDrawLayer _src;

        private readonly Rect _srcRectPx;
        private readonly Rect _dstRectPx;

        private readonly bool _hasBlendOverride;
        private readonly BlendMode _blendOverride;

        internal BlitDstStage(CodeDrawLayer dst, CodeDrawLayer src, Rect srcRect, Rect dstRect)
        {
            _dst = dst;
            _src = src;
            _srcRectPx = srcRect;
            _dstRectPx = dstRect;
            _hasBlendOverride = false;
            _blendOverride = default;
        }

        private BlitDstStage(CodeDrawLayer dst, CodeDrawLayer src, Rect srcRect, Rect dstRect, bool hasBlend, BlendMode mode)
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
            if (dstX < _dstRectPx.Left || dstX > _dstRectPx.Right ||
                dstY < _dstRectPx.Top || dstY > _dstRectPx.Bottom)
                return false;

            float du = _dstRectPx.Width;
            float dv = _dstRectPx.Height;
            if (du == 0 || dv == 0) return false;

            float lx = (dstX - _dstRectPx.Left) / du; // 0..1
            float ly = (dstY - _dstRectPx.Top) / dv; // 0..1

            srcX = _srcRectPx.Left + lx * _srcRectPx.Width;
            srcY = _srcRectPx.Top + ly * _srcRectPx.Height;
            return true;
        }

        public bool TryTransformPointFromSrcToDst(float srcX, float srcY, out float dstX, out float dstY)
        {
            dstX = dstY = 0;

            // outside src rect -> no mapping
            if (srcX < _srcRectPx.Left || srcX > _srcRectPx.Right ||
                srcY < _srcRectPx.Top || srcY > _srcRectPx.Bottom)
                return false;

            float su = _srcRectPx.Width;
            float sv = _srcRectPx.Height;
            if (su == 0 || sv == 0) return false;

            float lx = (srcX - _srcRectPx.Left) / su; // 0..1
            float ly = (srcY - _srcRectPx.Top) / sv; // 0..1

            dstX = _dstRectPx.Left + lx * _dstRectPx.Width;
            dstY = _dstRectPx.Top + ly * _dstRectPx.Height;
            return true;
        }
    }
}
