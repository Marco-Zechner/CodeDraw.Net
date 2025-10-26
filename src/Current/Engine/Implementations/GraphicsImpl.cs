using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Engine
{
    internal sealed class GraphicsImpl : IGraphics
    {
        private readonly GL _gl;

        public GraphicsImpl(GL gl) => _gl = gl;

        public object Raw => _gl;

        public StateGuard PushState()
        {
            // MVP: no-op guard; we’ll add real state save/restore later.
            return new StateGuard(() => { });
        }

        public void SetBlend(BlendMode mode)
        {
            // MVP: do nothing yet (we’ll enable and set when we need compositing)
        }

        public void SetBlendCustom(BlendDesc desc) { /* no-op MVP */ }

        public void ClearColor(float r, float g, float b, float a)
        {
            _gl.ClearColor(r, g, b, a);
        }

        public void Clear(ClearMask mask = ClearMask.Color)
        {
            uint m = 0;
            if (mask.HasFlag(ClearMask.Color)) m |= (uint)ClearBufferMask.ColorBufferBit;
            if (mask.HasFlag(ClearMask.Depth)) m |= (uint)ClearBufferMask.DepthBufferBit;
            if (mask.HasFlag(ClearMask.Stencil)) m |= (uint)ClearBufferMask.StencilBufferBit;
            _gl.Clear(m);
        }

        public void Use(object material)
        {
            throw new NotImplementedException();
        }
    }
}
