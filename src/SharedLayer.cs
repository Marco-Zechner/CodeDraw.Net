using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet;

public sealed class SharedLayer
{
    public uint Fbo { get; internal set; }
    public uint Texture { get; internal set; }
    public int Width  { get; internal set; }
    public int Height { get; internal set; }

    // Readers (window threads) call BeginUse/EndUse around sampling
    private int _useCount;
    public void BeginUse() => Interlocked.Increment(ref _useCount);
    public void EndUse()   => Interlocked.Decrement(ref _useCount);

    public async Task WaitUntilFreeAsync()
    {
        while (Volatile.Read(ref _useCount) > 0)
            await Task.Delay(1).ConfigureAwait(false);
    }

    internal static SharedLayer Create(GL gl, int width, int height, bool depthStencil = false)
    {
        var layer = new SharedLayer { Width = width, Height = height };

        // Texture
        gl.CreateTextures(TextureTarget.Texture2D, 1, out uint tex);
        gl.TextureStorage2D(tex, 1, SizedInternalFormat.Rgba8, (uint)width, (uint)height);
        gl.TextureParameter(tex, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TextureParameter(tex, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TextureParameter(tex, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TextureParameter(tex, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        // FBO
        gl.CreateFramebuffers(1, out uint fbo);
        gl.NamedFramebufferTexture(fbo, FramebufferAttachment.ColorAttachment0, tex, 0);

        if (depthStencil)
        {
            gl.CreateRenderbuffers(1, out uint rbo);
            gl.NamedRenderbufferStorage(rbo, InternalFormat.Depth24Stencil8, (uint)width, (uint)height);
            gl.NamedFramebufferRenderbuffer(fbo, FramebufferAttachment.DepthStencilAttachment,
                                            RenderbufferTarget.Renderbuffer, rbo);
        }

        // (optional) completeness check
        // var status = gl.CheckNamedFramebufferStatus(fbo, FramebufferTarget.Framebuffer);

        layer.Texture = tex;
        layer.Fbo = fbo;
        return layer;
    }

    internal static void Delete(GL gl, SharedLayer layer)
    {
        if (layer.Fbo != 0) gl.DeleteFramebuffer(layer.Fbo);
        if (layer.Texture != 0) gl.DeleteTexture(layer.Texture);
        layer.Fbo = 0;
        layer.Texture = 0;
    }
}
