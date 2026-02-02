using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using Monitor = System.Threading.Monitor;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;

public sealed unsafe partial class CodeDrawLayer : IDisposable
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Uniform2f(GL gl, int loc, float x, float y)
        => gl.Uniform2(loc, x, y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Uniform4f(GL gl, int loc, float x, float y, float z, float w)
        => gl.Uniform4(loc, x, y, z, w);

    public enum BlendMode
    {
        SOURCE_OVER_ALPHA,      // SrcAlpha, OneMinusSrcAlpha
        ADD,        // One, One
        MULTIPLY,   // DstColor, Zero
        NONE,       // Disable blending
        RGB_ALPHA_KEEP_DST_A,
    }

    private void ApplyBlendMode()
    {
        switch (_blendMode)
        {
            case BlendMode.NONE:
                _gl.Disable(GLEnum.Blend);
                break;

            case BlendMode.SOURCE_OVER_ALPHA:
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

            case BlendMode.RGB_ALPHA_KEEP_DST_A:
                _gl.Enable(GLEnum.Blend);
                _gl.BlendEquationSeparate(GLEnum.FuncAdd, GLEnum.FuncAdd);
                _gl.BlendFuncSeparate(
                    GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha, // RGB
                    GLEnum.Zero,     GLEnum.One              // A: keep dst
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

            Program = ShaderCompiler.CreateProgram(_gl, vs, fs);

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
        public nint Fence;
        public int W, H;
    }

    private struct Publication
    {
        public int FrontIndex;
        public nint Fence;
        public int W, H;
        public long Seq;
    }

    private interface ICmd { void Exec(GL gl, CodeDrawLayer self); }

    private sealed class CmdSetBlendMode : ICmd
    {
        public BlendMode Mode;
        public void Exec(GL gl, CodeDrawLayer self) { self._blendMode = Mode; self.ApplyBlendMode(); }
    }

    private sealed class CmdSetBlitShader : ICmd
    {
        public CodeDrawShader? Shader;
        public void Exec(GL gl, CodeDrawLayer self) => self._customBlitShader = Shader is { IsDisposed: false } ? Shader : null;
    }

    private sealed class CmdClear(float r, float g, float b, float a) : ICmd
    {
        public void Exec(GL gl, CodeDrawLayer self)
        {
            self._clearColor = (r, g, b, a);
            gl.ClearColor(self._clearColor.r, self._clearColor.g, self._clearColor.b, self._clearColor.a);
            gl.Clear((uint)ClearBufferMask.ColorBufferBit);
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

    private sealed class CmdResize(int w, int h) : ICmd
    {
        public readonly int W = w, H = h;
        public void Exec(GL gl, CodeDrawLayer self) => self.ResizeInternal(W, H);
    }

    private bool _clearFirst = true;

    private sealed class CmdSetClearFirst : ICmd
    {
        public bool Enabled;
        public void Exec(GL gl, CodeDrawLayer self) => self._clearFirst = Enabled;
    }

    private void SetClearFirst(bool enabled) => Enqueue(new CmdSetClearFirst { Enabled = enabled });

    /// <summary>
    /// If enabled, every Render() begins with ClearColor+Clear,
    /// and we never CopyFrontToBack(). This prevents "retained" accumulation.
    /// </summary>
    public bool AutoClearLastFrame
    {
        get => _clearFirst;
        set => SetClearFirst(value);
    }

    private BlendMode _blendMode = BlendMode.SOURCE_OVER_ALPHA;
    private CodeDrawShader? _customBlitShader;

    private readonly SharedGlfwHost _host;
    private readonly WindowHandle* _ctxWin;
    private readonly GL _gl;

    private readonly Buffer[] _buf = new Buffer[2];
    private int _front;
    private int Back => 1 - _front;

    private Publication _pub;
    private long _frameSeq;

    private volatile bool _disposed;

    private readonly ConcurrentQueue<(long seq, ICmd cmd)> _q = new();
    private long _nextCmdSeq;
    private long _lastEnqueuedSeq;
    private long _lastRenderedCmdSeq;

    private readonly object _renderLock = new();
    private bool _rendering;

    private readonly ConcurrentQueue<nint> _retireFences = new();

    private bool _clearRequested = true;
    private (float r, float g, float b, float a) _clearColor = (0f, 0f, 0f, 0f);

    private bool _inited;
    private uint _vao, _vbo, _ebo;

    private ShaderStore? _shaders;

    private AutoProgram _progRect, _progBlit, _progLayerRect;
    private AutoUniform _uRectPosSize, _uRectColor, _uRectRes;
    private AutoUniform _uBlitTex;
    private AutoUniform _uLayerRectDstRectPx, _uLayerRectDstResPx, _uLayerRectSrcUvRect, _uLayerRectTex;

    private int _w, _h;

    private readonly AutoResetEvent _published = new(false);

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

    private void EnsureInit()
    {
        if (_inited) return;
        _inited = true;

        (_vao, _vbo, _ebo) = ShaderCompiler.CreateFullScreenQuad(_gl);

        _shaders = new ShaderStore(_gl, EngineShaderPaths.ResolveEngineShaderRoot(), hotReload: false);

        _progRect     = new AutoProgram(_shaders, "rect");
        _uRectPosSize = new AutoUniform(_shaders, _progRect, "uPosSize");
        _uRectColor   = new AutoUniform(_shaders, _progRect, "uColor");
        _uRectRes     = new AutoUniform(_shaders, _progRect, "uRes");

        _progBlit = new AutoProgram(_shaders, "layerShader");
        _uBlitTex = new AutoUniform(_shaders, _progBlit, "uTex");

        _progLayerRect       = new AutoProgram(_shaders, "layerRectShader");
        _uLayerRectDstRectPx = new AutoUniform(_shaders, _progLayerRect, "uDstRectPx");
        _uLayerRectDstResPx  = new AutoUniform(_shaders, _progLayerRect, "uDstResPx");
        _uLayerRectSrcUvRect = new AutoUniform(_shaders, _progLayerRect, "uSrcUvRect");
        _uLayerRectTex       = new AutoUniform(_shaders, _progLayerRect, "uTex");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _published.Set();
        Render();
        _disposed = true;

        var glfw = _host.Glfw;
        glfw.MakeContextCurrent(_ctxWin);

        for (var i = 0; i < 2; i++)
        {
            if (_buf[i].Fence != 0) _gl.DeleteSync(_buf[i].Fence);
            if (_buf[i].Fbo != 0) _gl.DeleteFramebuffer(_buf[i].Fbo);
            if (_buf[i].Tex != 0) _gl.DeleteTexture(_buf[i].Tex);
            _buf[i] = default;
        }

        _shaders?.Dispose();
        _shaders = null;

        if (_vao != 0) _gl.DeleteVertexArray(_vao);
        if (_vbo != 0) _gl.DeleteBuffer(_vbo);
        if (_ebo != 0) _gl.DeleteBuffer(_ebo);

        glfw.MakeContextCurrent(null);
        _host.DestroyWindow(_ctxWin);
    }

    // --------- Public API ---------

    /// <summary>
    /// SOURCE_OVER_ALPHA is the default blend mode.
    /// </summary>
    /// <param name="mode"></param>
    public void SetBlendMode(BlendMode mode) => Enqueue(new CmdSetBlendMode { Mode = mode });
    public void SetLayerBlitShader(CodeDrawShader? shader) => Enqueue(new CmdSetBlitShader { Shader = shader });
    public void Clear(float r = 0f, float g = 0, float b = 0f, float a = 0f) => Enqueue(new CmdClear(r, g, b, a));

    public void DrawRect(float x, float y, float w, float h, float r, float g, float b, float a)
        => Enqueue(new CmdRect { X = x, Y = y, W = w, H = h, R = r, G = g, B = b, A = a });

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

    public void WaitForPublish(int timeoutMs)
    {
        if (_disposed) return;
        if (timeoutMs < 0) timeoutMs = Timeout.Infinite;
        _published.WaitOne(timeoutMs);
    }

    public bool TryGetLatest(out uint tex, out int w, out int h, out nint fence, out long seq)
    {
        var p = _pub;
        seq = Volatile.Read(ref p.Seq);
        if (seq == 0)
        {
            tex = 0; w = h = 0; fence = 0;
            return false;
        }

        var fi = p.FrontIndex;
        tex = (fi == 0 || fi == 1) ? _buf[fi].Tex : 0;
        w = p.W; h = p.H;
        fence = p.Fence;
        return tex != 0;
    }

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

        CmdResize? lastResize = null;
        var local = new List<(long seq, ICmd cmd)>(256);
        while (Volatile.Read(ref _lastRenderedCmdSeq) < targetSeq)
        {
            if (!_q.TryDequeue(out var item))
            {
                Thread.Yield();
                continue;
            }
            if (item.cmd is CmdResize cmd)
                lastResize = cmd;
            else
                local.Add(item);
            Volatile.Write(ref _lastRenderedCmdSeq, item.seq);
        }

        if (_buf[Back].Fence != 0)
        {
            while (true)
            {
                var s = _gl.ClientWaitSync(_buf[Back].Fence, SyncObjectMask.Bit, 1_000_000);
                if (s == GLEnum.AlreadySignaled || s == GLEnum.ConditionSatisfied) break;
            }
            _gl.DeleteSync(_buf[Back].Fence);
            _buf[Back].Fence = 0;
        }

        _gl.BindFramebuffer(GLEnum.Framebuffer, _buf[Back].Fbo);
        _gl.Viewport(0, 0, (uint)_w, (uint)_h);
        _gl.Disable(GLEnum.DepthTest);
        ApplyBlendMode();

        lastResize?.Exec(_gl, this);

        _gl.BindFramebuffer(GLEnum.Framebuffer, _buf[Back].Fbo);
        _gl.Viewport(0, 0, (uint)_w, (uint)_h);

        if (_clearFirst || _clearRequested)
        {
            local.Insert(0, (0, new CmdClear(_clearColor.r, _clearColor.g, _clearColor.b, _clearColor.a)));
            _clearRequested = false;
        }
        else if (local.Count > 1 && local[0].cmd is not CmdClear)
        {
            CopyFrontToBack();
        }

        foreach (var (_, cmd) in local)
            cmd.Exec(_gl, this);

        _gl.Finish();

        _buf[Back].Fence = 0;
        _front = Back;

        _pub.FrontIndex = _front;
        _pub.Fence = 0;
        _pub.W = _w;
        _pub.H = _h;

        var next = Interlocked.Increment(ref _frameSeq);
        Volatile.Write(ref _pub.Seq, next);

        _published.Set();

        _gl.BindFramebuffer(GLEnum.Framebuffer, 0);
        glfw.MakeContextCurrent(null);
    }

    private void RetireRequestedFences()
    {
        while (_retireFences.TryDequeue(out var f))
        {
            if (_pub.Fence == f) _pub.Fence = 0;
            for (var i = 0; i < 2; i++)
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

        ApplyBlendMode();

        _gl.BindTexture(GLEnum.Texture2D, 0);
        _gl.BindVertexArray(0);
        _gl.UseProgram(0);
    }

    private void ResizeInternal(int w, int h)
    {
        if (w <= 0 || h <= 0) return;

        _w = w; _h = h;
        _clearRequested = true;

        for (var i = 0; i < 2; i++)
        {
            if (_buf[i].Fence != 0) { _gl.DeleteSync(_buf[i].Fence); _buf[i].Fence = 0; }
            if (_buf[i].Fbo != 0) _gl.DeleteFramebuffer(_buf[i].Fbo);
            if (_buf[i].Tex != 0) _gl.DeleteTexture(_buf[i].Tex);
            _buf[i] = default;

            var tex = _gl.GenTexture();
            _gl.BindTexture(GLEnum.Texture2D, tex);
            _gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.Rgba8, (uint)w, (uint)h, 0, GLEnum.Rgba, GLEnum.UnsignedByte, null);
            _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
            _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
            _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
            _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
            _gl.BindTexture(GLEnum.Texture2D, 0);

            var fbo = _gl.GenFramebuffer();
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

        _published.Set();
    }

    private void ExecRect(GL gl, float x, float y, float w, float h, float r, float g, float b, float a)
    {
        gl.UseProgram(_progRect);
        gl.BindVertexArray(_vao);

        Uniform4f(gl, _uRectPosSize, x, y, w, h);
        Uniform4f(gl, _uRectColor, r, g, b, a);
        Uniform2f(gl, _uRectRes, _w, _h);

        gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, null);

        gl.BindVertexArray(0);
        gl.UseProgram(0);
    }

    private void ExecLayer(GL gl, CodeDrawLayer src)
    {
        if (!src.TryGetLatest(out var tex, out _, out _, out _, out _)) return;

        var prog = _customBlitShader?.Program ?? _progBlit;

        gl.UseProgram(prog);
        gl.BindVertexArray(_vao);
        gl.ActiveTexture(GLEnum.Texture0);
        gl.BindTexture(GLEnum.Texture2D, tex);

        var uTexLoc = (prog == _progBlit) ? _uBlitTex : gl.GetUniformLocation(prog, "uTex");
        if (uTexLoc >= 0) gl.Uniform1(uTexLoc, 0);

        gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, null);

        gl.BindTexture(GLEnum.Texture2D, 0);
        gl.BindVertexArray(0);
        gl.UseProgram(0);
    }
}