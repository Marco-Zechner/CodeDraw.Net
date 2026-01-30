using System.Collections.Concurrent;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using Monitor = System.Threading.Monitor;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;

/// Minimal layer with the semantics:
/// - Clear() affects the NEXT Render() (explicit).
/// - Render() blocks until the CPU has submitted the frame AND inserted a fence.
/// - If you don't Clear(), the next Render draws on top of the previous published image (front->back copy).
/// Backend:
/// - Double buffer (front/back).
/// - Presenter only polls fences; fence deletion happens in the layer's own context (safe).
public sealed unsafe class CodeDrawLayer : IDisposable
{
    public enum BlendMode
    {
        ALPHA,      // SrcAlpha, OneMinusSrcAlpha
        ADD,        // One, One
        MULTIPLY,   // DstColor, Zero  (common quick multiply)
        NONE,        // Disable blending
        BLEND_RBG_SOURCEOVER_ALPHA
    }

    private void ApplyBlendMode()
    {
        switch (_blendMode)
        {
            case BlendMode.NONE:
                _gl.Disable(GLEnum.Blend);
                break;

            case BlendMode.ALPHA:
                _gl.Enable(GLEnum.Blend);
                _gl.BlendEquationSeparate(GLEnum.FuncAdd, GLEnum.FuncAdd);
                _gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);
                break;

            case BlendMode.ADD:
                _gl.Enable(GLEnum.Blend);
                _gl.BlendEquationSeparate(GLEnum.FuncAdd, GLEnum.FuncAdd);
                _gl.BlendFunc(GLEnum.One, GLEnum.One);
                break;

            case BlendMode.MULTIPLY:
                _gl.Enable(GLEnum.Blend);
                _gl.BlendEquationSeparate(GLEnum.FuncAdd, GLEnum.FuncAdd);
                _gl.BlendFunc(GLEnum.DstColor, GLEnum.Zero);
                break;

            case BlendMode.BLEND_RBG_SOURCEOVER_ALPHA:
                _gl.Enable(GLEnum.Blend);
                _gl.BlendEquationSeparate(GLEnum.FuncAdd, GLEnum.FuncAdd);
                _gl.BlendFuncSeparate(
                    GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha,   // RGB
                    GLEnum.One,      GLEnum.OneMinusSrcAlpha    // A
                );
                break;
        }
    }

    public sealed class CodeDrawShader : IDisposable
    {
        private readonly SharedGlfwHost _host;
        private readonly WindowHandle* _ctxWin;
        private readonly GL _gl;

        public uint Program { get; private set; }
        public bool IsDisposed { get; private set; }

        public CodeDrawShader(SharedGlfwHost host, string vs, string fs)
        {
            _host = host;
            _ctxWin = host.CreateHiddenWindow(1, 1, "shader-ctx");
            var glfw = host.Glfw;

            glfw.MakeContextCurrent(_ctxWin);
            glfw.SwapInterval(0);
            _gl = GL.GetApi(glfw.GetProcAddress);

            Program = GlShader.CreateProgram(_gl, vs, fs);

            glfw.MakeContextCurrent(null);
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;

            var glfw = _host.Glfw;
            glfw.MakeContextCurrent(_ctxWin);

            if (Program != 0)
            {
                _gl.DeleteProgram(Program);
                Program = 0;
            }

            glfw.MakeContextCurrent(null);
            _host.DestroyWindow(_ctxWin);
        }
    }

    private struct Buffer
    {
        public uint Tex;
        public uint Fbo;
        public nint Fence; // GLsync for when this buffer was published as front
        public int W, H;
    }

    private struct Publication
    {
        public int FrontIndex; // 0/1
        public nint Fence;     // fence for current front (may be 0 if already retired)
        public int W, H;
        public long Seq;       // written last
    }

    private interface ICmd { void Exec(GL gl, CodeDrawLayer self); }

    private sealed class CmdSetBlendMode : ICmd
    {
        public BlendMode Mode;
        public void Exec(GL gl, CodeDrawLayer self)
        {
            self._blendMode = Mode;
            self.ApplyBlendMode();
        }
    }

    private sealed class CmdSetBlitShader : ICmd
    {
        public CodeDrawShader? Shader;
        public void Exec(GL gl, CodeDrawLayer self)
        {
            // null => default layer shader
            self._customBlitShader = (Shader != null && !Shader.IsDisposed) ? Shader : null;
        }
    }

    private sealed class CmdClear(float r, float g, float b, float a) : ICmd
    {
        public void Exec(GL gl, CodeDrawLayer self)
        {
            self._clearColor = (r, g, b, a);
            self._clearRequested = true;
        }
    }

    private sealed class CmdRect : ICmd
    {
        public float X, Y, W, H;
        public float R, G, B, A;
        public void Exec(GL gl, CodeDrawLayer self) => self.ExecRect(gl, X, Y, W, H, R, G, B, A);
    }

    private sealed class CmdLayer : ICmd
    {
        public CodeDrawLayer? Src;
        public void Exec(GL gl, CodeDrawLayer self)
        {
            var s = Src;
            if (s is null || s._disposed) return;
            self.ExecLayer(gl, s);
        }
    }

    private sealed class CmdResize : ICmd
    {
        public readonly int W, H;
        public CmdResize(int w, int h) { W = w; H = h; }
        public void Exec(GL gl, CodeDrawLayer self) => self.ResizeInternal(W, H);
    }

    private BlendMode _blendMode = BlendMode.ALPHA;
    private CodeDrawShader? _customBlitShader;

    private readonly SharedGlfwHost _host;
    private readonly WindowHandle* _ctxWin;
    private readonly GL _gl;

    private readonly Buffer[] _buf = new Buffer[2];
    private int _front = 0;
    private int Back => 1 - _front;

    private Publication _pub;
    private long _frameSeq;

    private volatile bool _disposed;

    // Commands + cut-line
    private readonly ConcurrentQueue<(long seq, ICmd cmd)> _q = new();
    private long _nextCmdSeq;
    private long _lastEnqueuedSeq;
    private long _lastRenderedCmdSeq;

    // Render exclusion
    private readonly object _renderLock = new();
    private bool _rendering;

    // Fence retirement requests from presenters
    private readonly ConcurrentQueue<nint> _retireFences = new();

    // Layer semantics
    private bool _clearRequested = true;
    private (float r, float g, float b, float a) _clearColor = (0f, 0f, 0f, 0f);

    // GL resources
    private bool _inited;
    private uint _vao, _vbo, _ebo;
    private uint _progRect, _progBlit;
    private int _uRectPosSize, _uRectColor, _uRectRes;
    private int _uBlitTex;

    private int _w, _h;

    public bool IsDisposed => _disposed;

    public CodeDrawLayer(SharedGlfwHost host, int w = 800, int h = 600)
    {
        _host = host;
        _ctxWin = host.CreateHiddenWindow(1, 1, "layer-ctx");

        var glfw = host.Glfw;
        glfw.MakeContextCurrent(_ctxWin);
        glfw.SwapInterval(0);
        _gl = GL.GetApi(glfw.GetProcAddress);

        EnsureInit();
        ResizeInternal(w, h);

        glfw.MakeContextCurrent(null);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Render(); // finish any work

        var glfw = _host.Glfw;
        glfw.MakeContextCurrent(_ctxWin);

        // delete fences/textures/fbos
        for (int i = 0; i < 2; i++)
        {
            if (_buf[i].Fence != 0) _gl.DeleteSync(_buf[i].Fence);
            if (_buf[i].Fbo != 0) _gl.DeleteFramebuffer(_buf[i].Fbo);
            if (_buf[i].Tex != 0) _gl.DeleteTexture(_buf[i].Tex);
            _buf[i] = default;
        }

        if (_progRect != 0) _gl.DeleteProgram(_progRect);
        if (_progBlit != 0) _gl.DeleteProgram(_progBlit);
        if (_vao != 0) _gl.DeleteVertexArray(_vao);
        if (_vbo != 0) _gl.DeleteBuffer(_vbo);
        if (_ebo != 0) _gl.DeleteBuffer(_ebo);

        glfw.MakeContextCurrent(null);
        _host.DestroyWindow(_ctxWin);
    }

    // --------- Public API ---------

    public void SetBlendMode(BlendMode mode) => Enqueue(new CmdSetBlendMode { Mode = mode });

    public void SetLayerBlitShader(CodeDrawShader? shader) => Enqueue(new CmdSetBlitShader { Shader = shader });

    public void Clear(float r = 0f, float g = 0, float b = 0f, float a = 0f) => Enqueue(new CmdClear(r,g,b,a));

    public void DrawRect(float x, float y, float w, float h, float r, float g, float b, float a)
        => Enqueue(new CmdRect { X = x, Y = y, W = w, H = h, R = r, G = g, B = b, A = a });

    public void DrawLayer(CodeDrawLayer src)
        => Enqueue(new CmdLayer { Src = src });

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

        long targetSeq = Volatile.Read(ref _lastEnqueuedSeq);

        while (true)
        {
            if (Volatile.Read(ref _lastRenderedCmdSeq) >= targetSeq) return;

            bool iAmRenderer = false;
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

    /// Presenter reads latest front texture and fence.
    public bool TryGetLatest(out uint tex, out int w, out int h, out nint fence, out long seq)
    {
        var p = _pub;
        seq = Volatile.Read(ref p.Seq);
        if (seq == 0)
        {
            tex = 0; w = h = 0; fence = 0;
            return false;
        }

        int fi = p.FrontIndex;
        tex = (fi == 0 || fi == 1) ? _buf[fi].Tex : 0;
        w = p.W; h = p.H;
        fence = p.Fence;
        return tex != 0;
    }

    /// Called by presenter once it observed the fence is signaled.
    /// We do NOT delete the fence there; we delete it here in the layer context.
    public void RequestRetireFence(nint fence)
    {
        if (fence == 0) return;
        _retireFences.Enqueue(fence);
    }

    // --------- Internals ---------

    private void Enqueue(ICmd cmd)
    {
        if (_disposed) return;
        var seq = Interlocked.Increment(ref _nextCmdSeq);
        _q.Enqueue((seq, cmd));
        Volatile.Write(ref _lastEnqueuedSeq, seq);
    }

    private void DrainUntil(long targetSeq)
    {
        var glfw = _host.Glfw;
        glfw.MakeContextCurrent(_ctxWin);

        EnsureInit();
        RetireRequestedFences();

        // Collect commands up to cut-line into a local list (so ordering is bulletproof)
        var local = new List<(long seq, ICmd cmd)>(256);
        while (Volatile.Read(ref _lastRenderedCmdSeq) < targetSeq)
        {
            if (!_q.TryDequeue(out var item))
            {
                Thread.Yield();
                continue;
            }
            local.Add(item);
            Volatile.Write(ref _lastRenderedCmdSeq, item.seq);
        }

        // Back buffer must be safe to overwrite. If it still has a fence, wait + delete here.
        if (_buf[Back].Fence != 0)
        {
            while (true)
            {
                var s = _gl.ClientWaitSync(_buf[Back].Fence, SyncObjectMask.Bit, 1_000_000); // 1ms
                if (s == GLEnum.AlreadySignaled || s == GLEnum.ConditionSatisfied) break;
            }
            _gl.DeleteSync(_buf[Back].Fence);
            _buf[Back].Fence = 0;
        }

        // Bind back FBO
        _gl.BindFramebuffer(GLEnum.Framebuffer, _buf[Back].Fbo);
        _gl.Viewport(0, 0, (uint)_w, (uint)_h);
        _gl.Disable(GLEnum.DepthTest);
        ApplyBlendMode();

        // Phase 1: process semantic commands (like Clear/Resize) BEFORE preparing the frame
        foreach (var (_, cmd) in local)
        {
            if (cmd is CmdClear) cmd.Exec(_gl, this);
            if (cmd is CmdResize) cmd.Exec(_gl, this); // may rebuild buffers
        }

        // If resize happened, we must rebind back FBO after it
        _gl.BindFramebuffer(GLEnum.Framebuffer, _buf[Back].Fbo);
        _gl.Viewport(0, 0, (uint)_w, (uint)_h);

        // Prepare frame
        if (_clearRequested)
        {
            _gl.ClearColor(_clearColor.r, _clearColor.g, _clearColor.b, _clearColor.a);
            _gl.Clear((uint)ClearBufferMask.ColorBufferBit);
            _clearRequested = false;
        }
        else
        {
            CopyFrontToBack();
        }

        // Phase 2: execute draw commands in order (skip Clear/Resize now)
        foreach (var (_, cmd) in local)
        {
            if (cmd is CmdClear || cmd is CmdResize) continue;
            cmd.Exec(_gl, this);
        }

        // Publish: insert fence on back, then swap front/back
        nint fence = _gl.FenceSync(SyncCondition.SyncGpuCommandsComplete, SyncBehaviorFlags.None);
        _gl.Flush();

        _buf[Back].Fence = fence;
        _front = Back;

        _pub.FrontIndex = _front;
        _pub.Fence = fence;
        _pub.W = _w;
        _pub.H = _h;
        long next = Interlocked.Increment(ref _frameSeq);
        Volatile.Write(ref _pub.Seq, next);

        _gl.BindFramebuffer(GLEnum.Framebuffer, 0);
        glfw.MakeContextCurrent(null);
    }



    private void RetireRequestedFences()
    {
        while (_retireFences.TryDequeue(out var f))
        {
            // clear publication pointer if it matches
            if (_pub.Fence == f) _pub.Fence = 0;

            // Also clear any buffer fence pointer that matches, then delete
            for (int i = 0; i < 2; i++)
                if (_buf[i].Fence == f) _buf[i].Fence = 0;

            _gl.DeleteSync(f);
        }
    }

    private void CopyFrontToBack()
    {
        _gl.UseProgram(_progBlit);
        _gl.BindVertexArray(_vao);
        _gl.ActiveTexture(GLEnum.Texture0);
        _gl.BindTexture(GLEnum.Texture2D, _buf[_front].Tex);
        if (_uBlitTex >= 0) _gl.Uniform1(_uBlitTex, 0);

        _gl.Disable(GLEnum.Blend);
        _gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, null);

        // restore whatever blend mode is currently selected
        ApplyBlendMode();

        _gl.BindTexture(GLEnum.Texture2D, 0);
        _gl.BindVertexArray(0);
        _gl.UseProgram(0);
    }

    private void EnsureInit()
    {
        if (_inited) return;
        _inited = true;

        (_vao, _vbo, _ebo) = GlShader.CreateFullScreenQuad(_gl);

        _progRect = GlShader.CreateProgram(_gl, GlShader.RectShader.VS, GlShader.RectShader.FS);
        _uRectPosSize = _gl.GetUniformLocation(_progRect, "uPosSize");
        _uRectColor = _gl.GetUniformLocation(_progRect, "uColor");
        _uRectRes = _gl.GetUniformLocation(_progRect, "uRes");

        _progBlit = GlShader.CreateProgram(_gl, GlShader.LayerShader.VS, GlShader.LayerShader.FS);
        _uBlitTex = _gl.GetUniformLocation(_progBlit, "uTex");
    }

    private void ResizeInternal(int w, int h)
    {
        if (w <= 0 || h <= 0) return;

        _w = w; _h = h;
        _clearRequested = true;

        for (int i = 0; i < 2; i++)
        {
            if (_buf[i].Fence != 0) { _gl.DeleteSync(_buf[i].Fence); _buf[i].Fence = 0; }
            if (_buf[i].Fbo != 0) _gl.DeleteFramebuffer(_buf[i].Fbo);
            if (_buf[i].Tex != 0) _gl.DeleteTexture(_buf[i].Tex);
            _buf[i] = default;

            uint tex = _gl.GenTexture();
            _gl.BindTexture(GLEnum.Texture2D, tex);
            _gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.Rgba8, (uint)w, (uint)h, 0, GLEnum.Rgba, GLEnum.UnsignedByte, null);
            _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
            _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
            _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
            _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
            _gl.BindTexture(GLEnum.Texture2D, 0);

            uint fbo = _gl.GenFramebuffer();
            _gl.BindFramebuffer(GLEnum.Framebuffer, fbo);
            _gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0, GLEnum.Texture2D, tex, 0);
            _gl.BindFramebuffer(GLEnum.Framebuffer, 0);

            _buf[i].Tex = tex;
            _buf[i].Fbo = fbo;
            _buf[i].W = w;
            _buf[i].H = h;
        }

        _front = 0;
        _pub = new Publication { FrontIndex = _front, Fence = 0, W = w, H = h, Seq = 0 };
    }

    private void ExecRect(GL gl, float x, float y, float w, float h, float r, float g, float b, float a)
    {
        gl.UseProgram(_progRect);
        gl.BindVertexArray(_vao);

        gl.Uniform4(_uRectPosSize, x, y, w, h);
        gl.Uniform4(_uRectColor, r, g, b, a);
        gl.Uniform2(_uRectRes, (float)_w, (float)_h);

        gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, null);

        gl.BindVertexArray(0);
        gl.UseProgram(0);
    }

    private void ExecLayer(GL gl, CodeDrawLayer src)
    {
        if (!src.TryGetLatest(out uint tex, out _, out _, out _, out _)) return;

        uint prog = _customBlitShader?.Program ?? _progBlit;

        gl.UseProgram(prog);
        gl.BindVertexArray(_vao);
        gl.ActiveTexture(GLEnum.Texture0);
        gl.BindTexture(GLEnum.Texture2D, tex);

        int uTexLoc = (prog == _progBlit) ? _uBlitTex : gl.GetUniformLocation(prog, "uTex");
        if (uTexLoc >= 0) gl.Uniform1(uTexLoc, 0);

        gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, null);

        gl.BindTexture(GLEnum.Texture2D, 0);
        gl.BindVertexArray(0);
        gl.UseProgram(0);
    }
}
