using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;

/// Window presenter:
/// - GL 3.3 (no DSA)
/// - Polls the layer fence and only swaps to a new frame when ready
/// - NEVER deletes fences; it requests the layer to retire them in the layer context
public sealed unsafe class CodeDrawWindow
{
    private readonly SharedGlfwHost _host;
    private readonly WindowHandle* _win;
    private readonly Thread _presentThread;
    private volatile bool _closing;

    private CodeDrawLayer? _layer;
    public CodeDrawLayer? Layer { get => _layer; }

    private volatile bool _keepLastFrameUntilReady = true;

    public void SetPresentedLayer(CodeDrawLayer? layer, bool keepLastFrameUntilReady = true)
    {
        _layer = layer;
        _keepLastFrameUntilReady = keepLastFrameUntilReady;
    }

    public bool ShouldClose => _closing || _host.Glfw.WindowShouldClose(_win);

    public CodeDrawWindow(SharedGlfwHost host, int w, int h, string title)
    {
        _host = host;
        _win = host.CreateWindow(w, h, title);
        _layer = new CodeDrawLayer(host, w, h); // default layer

        _presentThread = new Thread(PresentLoop) { IsBackground = true, Name = $"Presenter:{title}" };
        _presentThread.Start();
    }

    public void Close()
    {
        if (_closing) return;
        _closing = true;
        _host.EnqueueUi(() =>
        {
            if (!_host.Glfw.WindowShouldClose(_win))
                _host.Glfw.SetWindowShouldClose(_win, true);
        });
    }

    public void WaitForClose() => _presentThread.Join();

    private void PresentLoop()
    {
        var glfw = _host.Glfw;
        glfw.MakeContextCurrent(_win);
        glfw.SwapInterval(0);
        var gl = GL.GetApi(glfw.GetProcAddress);

        var (vao, vbo, ebo) = GlShader.CreateFullScreenQuad(gl);
        var progBlit = GlShader.CreateProgram(gl, GlShader.LayerShader.VS, GlShader.LayerShader.FS);
        var uTex = gl.GetUniformLocation(progBlit, "uTex");

        gl.Disable(GLEnum.Blend);

        uint lastTex = 0;
        long lastSeq = 0;

        CodeDrawLayer? lastLayerRef = null;

        while (!ShouldClose)
        {
            glfw.GetFramebufferSize(_win, out var fbW, out var fbH);
            if (fbW == 0 || fbH == 0) { glfw.SwapBuffers(_win); Thread.Sleep(12); continue; }

            gl.BindFramebuffer(GLEnum.Framebuffer, 0);
            gl.Viewport(0, 0, (uint)fbW, (uint)fbH);

            gl.ClearColor(0f, 0f, 0f, 0f);
            gl.Clear((uint)ClearBufferMask.ColorBufferBit);

            var layer = _layer;
            var keepLast = _keepLastFrameUntilReady;

            if (!ReferenceEquals(layer, lastLayerRef))
            {
                lastLayerRef = layer;

                lastSeq = 0;

                if (!keepLast)
                    lastTex = 0;
            }

            if (layer is { IsDisposed: false })
            {
                if (layer.TryGetLatest(out var tex, out _, out _, out var fence, out var seq))
                {
                    bool ready = fence == 0;
                    if (!ready)
                    {
                        var s = gl.ClientWaitSync(fence, SyncObjectMask.Bit, 0);
                        ready = s is GLEnum.AlreadySignaled or GLEnum.ConditionSatisfied;
                        if (ready) layer.RequestRetireFence(fence);
                    }

                    if (ready && tex != 0 && seq >= lastSeq)
                    {
                        lastTex = tex;
                        lastSeq = seq;
                    }
                }
            }

            if (lastTex != 0)
            {
                gl.UseProgram(progBlit);
                gl.BindVertexArray(vao);
                gl.ActiveTexture(GLEnum.Texture0);
                gl.BindTexture(GLEnum.Texture2D, lastTex);
                if (uTex >= 0) gl.Uniform1(uTex, 0);
                gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, null);
                gl.BindTexture(GLEnum.Texture2D, 0);
                gl.BindVertexArray(0);
                gl.UseProgram(0);
            }

            glfw.SwapBuffers(_win);
            Thread.Sleep(12);
        }

        gl.DeleteProgram(progBlit);
        gl.DeleteVertexArray(vao);
        gl.DeleteBuffer(vbo);
        gl.DeleteBuffer(ebo);

        _host.DestroyWindow(_win);
        glfw.MakeContextCurrent(null);
    }
}
