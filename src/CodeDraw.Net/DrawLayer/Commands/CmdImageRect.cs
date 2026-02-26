using MarcoZechner.CodeDrawDotNet.Images;
using MarcoZechner.MathDotNet;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer.Commands;

internal sealed class CmdDrawImage : ICmd
{
    public CodeDrawImage Img = null!;
    public ImageDrawOptions Opt;
    public Rect<int> DstRectPx;

    public unsafe void Exec(GL gl, CodeDrawLayer layer)
    {
        ImageStore.Register(layer, Img.Key);

        var tex = ImageStore.GetTexture(gl, layer, Img.Key, out var iw, out var ih);
        if (tex == 0) return;

        // Compute dst + src uv rect
        ComputeImageDraw(iw, ih, DstRectPx, Opt, out var dst, out var u0, out var v0, out var u1, out var v1);

        gl.UseProgram(layer._progImageRect);
        gl.BindVertexArray(layer._vao);

        if (layer._uImageDstRectPx >= 0)
            GlHelper.Uniform4F(gl, layer._uImageDstRectPx, dst.Left, dst.Top, dst.Width, dst.Height);

        if (layer._uImageDstResPx >= 0)
            GlHelper.Uniform2F(gl, layer._uImageDstResPx, layer.Width, layer.Height);

        if (layer._uImageSrcUvRect >= 0)
            GlHelper.Uniform4F(gl, layer._uImageSrcUvRect, u0, v0, u1, v1);

        gl.ActiveTexture(GLEnum.Texture0);
        gl.BindTexture(GLEnum.Texture2D, tex);

        if (Opt.FitMode == ImageFitMode.Repeat)
        {
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.Repeat);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.Repeat);
        }
        else
        {
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
        }
        
        if (layer._uImageTex >= 0)
            GlHelper.Uniform1(gl, layer._uImageTex, 0);

        gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, null);

        gl.BindTexture(GLEnum.Texture2D, 0);
        gl.BindVertexArray(0);
        gl.UseProgram(0);
    }
    
    private static float Anchor01X(ImageAnchor a) => a switch
    {
        ImageAnchor.TopLeft or ImageAnchor.Left or ImageAnchor.BottomLeft => 0f,
        ImageAnchor.TopRight or ImageAnchor.Right or ImageAnchor.BottomRight => 1f,
        _ => 0.5f,
    };

    private static float Anchor01Y(ImageAnchor a) => a switch
    {
        ImageAnchor.TopLeft or ImageAnchor.Top or ImageAnchor.TopRight => 0f,
        ImageAnchor.BottomLeft or ImageAnchor.Bottom or ImageAnchor.BottomRight => 1f,
        _ => 0.5f,
    };

    private static void ComputeImageDraw(
        int imgW, int imgH,
        Rect<int> dst,
        in ImageDrawOptions opt,
        out Rect<int> outDst,
        out float u0, out float v0, out float u1, out float v1)
    {
        outDst = dst;

        const bool DECODER_NEEDS_FLIP = false; // stb/top-left origin => needs flip for GL UVs
        var effectiveFlip = opt.FlipY ^ DECODER_NEEDS_FLIP;
        
        // default full source rect
        u0 = 0f; u1 = 1f;
        v0 = effectiveFlip ? 1f : 0f;
        v1 = effectiveFlip ? 0f : 1f;

        if (imgW <= 0 || imgH <= 0 || dst.Width <= 0 || dst.Height <= 0)
            return;

        var ax = Anchor01X(opt.Anchor);
        var ay = Anchor01Y(opt.Anchor);

        float iw = imgW, ih = imgH;
        float dw = dst.Width, dh = dst.Height;

        var ia = iw / ih;
        var da = dw / dh;

        switch (opt.FitMode)
        {
            case ImageFitMode.Fit:
                // stretch to dst, src full
                return;

            case ImageFitMode.Contain:
            {
                // scale to fit inside dst, adjust dst rect, src full
                var scale = (ia > da) ? (dw / iw) : (dh / ih);
                var rw = iw * scale;
                var rh = ih * scale;

                var w = (int)MathF.Round(rw);
                var h = (int)MathF.Round(rh);

                var x = dst.Left + (int)MathF.Round((dw - w) * ax);
                var y = dst.Top  + (int)MathF.Round((dh - h) * ay);

                outDst = (Rect<int>)(Rect)new RectWh(x, y, w, h); //TODO: this is a mess. add a direct cast
                return;
            }

            case ImageFitMode.Cover:
            {
                // fill dst, crop via src UV
                if (ia > da)
                {
                    // crop X
                    var needed = da / ia; // width in UV
                    var cut = (1f - needed);
                    var left = cut * ax;
                    var right = left + needed;
                    u0 = left; u1 = right;
                }
                else
                {
                    // crop Y
                    var needed = ia / da; // height in UV
                    var cut = (1f - needed);
                    var top = cut * ay;
                    var bot = top + needed;

                    // apply flipY mapping
                    if (opt.FlipY)
                    {
                        // v goes 1..0, so swap in that space
                        v0 = 1f - top;
                        v1 = 1f - bot;
                    }
                    else
                    {
                        v0 = top;
                        v1 = bot;
                    }
                }
                return;
            }

            case ImageFitMode.PixelPerfect:
            {
                // no scaling: dst size is image size anchored inside dst
                var w = imgW;
                var h = imgH;

                var x = dst.Left + (int)MathF.Round((dw - w) * ax);
                var y = dst.Top  + (int)MathF.Round((dh - h) * ay);

                outDst = (Rect<int>)(Rect)new RectWh(x, y, w, h); //TODO: this is a mess. add a direct cast

                // if it doesn't fit, crop via UV (and clamp dst to original dst)
                // simplest: keep dst as-is and compute UV window of the image
                if (w > dw || h > dh)
                {
                    outDst = dst; // draw into dst, sample a cropped uv portion
                    var uNeeded = MathF.Min(1f, dw / iw);
                    var vNeeded = MathF.Min(1f, dh / ih);

                    var uLeft = (1f - uNeeded) * ax;
                    var vTop  = (1f - vNeeded) * ay;

                    u0 = uLeft;
                    u1 = uLeft + uNeeded;

                    if (opt.FlipY)
                    {
                        v0 = 1f - vTop;
                        v1 = 1f - (vTop + vNeeded);
                    }
                    else
                    {
                        v0 = vTop;
                        v1 = vTop + vNeeded;
                    }
                }

                return;
            }

            case ImageFitMode.Repeat:
            {
                var rs = opt.RepeatScale;
                if (rs <= 0f) rs = 1f; // avoid sx/sy==0 => single-color

                var sx = (dw / iw) * rs;
                var sy = (dh / ih) * rs;

                // Optional: also guard against denormally tiny values
                if (sx == 0f) sx = 1e-6f;
                if (sy == 0f) sy = 1e-6f;

                var ox = (1f - sx) * ax;
                var oy = (1f - sy) * ay;

                u0 = ox;
                u1 = ox + sx;

                if (effectiveFlip) { v0 = oy + sy; v1 = oy; }
                else { v0 = oy; v1 = oy + sy; }

                return;
            }
        }
    }
}