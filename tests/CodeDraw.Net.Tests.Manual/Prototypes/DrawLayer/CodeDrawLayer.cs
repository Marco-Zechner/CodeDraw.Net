using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using UniformType = MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders.UniformType;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer;

public sealed unsafe partial class CodeDrawLayer : IDisposable, IShaderConsumer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Uniform2F(GL gl, int loc, float x, float y)
        => gl.Uniform2(loc, x, y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Uniform4F(GL gl, int loc, float x, float y, float z, float w)
        => gl.Uniform4(loc, x, y, z, w);

    // --- time base for uTime ---
    private readonly long _timeStartTicks = Stopwatch.GetTimestamp();

    public float LayerAliveForSeconds() //TODO stop if disposed?
    {
        var now = Stopwatch.GetTimestamp();
        var dt = (now - _timeStartTicks) / (double)Stopwatch.Frequency;
        return (float)dt;
    }

    // ---- external shader cache (now supports both layer-copy + custom-rect) ----
    private readonly object _extShaderLock = new();
    private readonly HashSet<ShaderKey> _extKnown = [];
    private readonly ConcurrentQueue<ShaderKey> _extInitPending = new();

    private sealed class ExtShaderEntry
    {
        public AutoProgram Prog = null!;

        // Common (layer copy)
        public AutoUniform UTex = null!;

        // Per-program user uniform location cache:
        // programHandle -> (uniformName -> location)
        public readonly Dictionary<uint, Dictionary<string, int>> UserLocCache = new();
    }

    private readonly Dictionary<ShaderKey, ExtShaderEntry> _extCache = new();

    private void ScheduleExternalShader(CustomShader? shader)
    {
        if (shader == null) return;

        // thread-safe, GL-free
        lock (_extShaderLock)
        {
            if (_extKnown.Add(shader.Key))
                _extInitPending.Enqueue(shader.Key);
        }
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

    private bool _clearFirst = true;

    private BlendMode _blendMode = BlendMode.SOURCE_OVER_ALPHA;

    private readonly SharedGlfwHost _host;
    private readonly WindowHandle* _ctxWin;
    private readonly GL _gl;

    private readonly Buffer[] _buf = new Buffer[2];
    private int _front;
    private int Back => 1 - _front;

    private Publication _pub;
    private long _frameSeq;

    private bool _disposed;
    public bool IsDisposed => Volatile.Read(ref _disposed);

    private readonly ConcurrentQueue<(long seq, ICmd cmd)> _q = new();
    private long _nextCmdSeq;
    private long _lastEnqueuedSeq;
    private long _lastRenderedCmdSeq;

    private readonly object _renderLock = new(); // Not "Lock" because we use Monitor.Wait/Pulse which isn't compatible with the "Lock" type.
    private bool _rendering;

    private readonly ConcurrentQueue<nint> _retireFences = new();

    private bool _clearRequested = true;
    private (float r, float g, float b, float a) _clearColor = (0f, 0f, 0f, 0f);

    private bool _inited;
    private uint _vao, _vbo, _ebo;

    private AutoProgram _progRect = null!, _progBlit = null!, _progLayerRect = null!;
    private AutoUniform _uRectPosSize = null!, _uRectColor = null!, _uRectRes = null!;
    private AutoUniform _uBlitTex = null!;
    private AutoUniform _uLayerRectDstRectPx = null!, _uLayerRectDstResPx = null!, _uLayerRectSrcUvRect = null!, _uLayerRectTex = null!;

    private int _w, _h;

    private readonly AutoResetEvent _published = new(false);


    public string DebugName { get; }

    public CodeDrawLayer(SharedGlfwHost host, int w = 800, int h = 600, string label = "Unknown Layer")
    {
        DebugName = $"[Layer:{label}]";
        _host = host;
        _ctxWin = host.CreateHiddenLayerWindow(1, 1, "layer-ctx");

        LockedGlfw.MakeContextCurrent(_ctxWin);
        LockedGlfw.SwapInterval(0);
        _gl = GL.GetApi(LockedGlfw.GetProcAddress);

        EnsureInit();
        ResizeInternal(w, h);

        LockedGlfw.MakeContextCurrent(null);
    }

    private void EnsureInit()
    {
        if (_inited) return;
        _inited = true;

        (_vao, _vbo, _ebo) = ShaderCompiler.CreateFullScreenQuad(_gl);

        _progRect     = new AutoProgram(this, ShaderPath.Engine("rect"));
        _uRectPosSize = new AutoUniform(_gl, this, _progRect, "uPosSize");
        _uRectColor   = new AutoUniform(_gl, this, _progRect, "uColor");
        _uRectRes     = new AutoUniform(_gl, this, _progRect, "uRes");

        _progBlit = new AutoProgram(this, ShaderPath.Engine("layerShader"));
        _uBlitTex = new AutoUniform(_gl, this, _progBlit, "uTex");

        _progLayerRect       = new AutoProgram(this, ShaderPath.Engine("layerRectShader"));
        _uLayerRectDstRectPx = new AutoUniform(_gl, this, _progLayerRect, "uDstRectPx");
        _uLayerRectDstResPx  = new AutoUniform(_gl, this, _progLayerRect, "uDstResPx");
        _uLayerRectSrcUvRect = new AutoUniform(_gl, this, _progLayerRect, "uSrcUvRect");
        _uLayerRectTex       = new AutoUniform(_gl, this, _progLayerRect, "uTex");
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, true))
            return;

        _published.Set();

        // Flush any pending commands once
        Render();

        LockedGlfw.MakeContextCurrent(_ctxWin);

        for (var i = 0; i < 2; i++)
        {
            if (_buf[i].Fence != 0) _gl.DeleteSync(_buf[i].Fence);
            if (_buf[i].Fbo != 0) _gl.DeleteFramebuffer(_buf[i].Fbo);
            if (_buf[i].Tex != 0) _gl.DeleteTexture(_buf[i].Tex);
            _buf[i] = default;
        }

        ShaderStore.DisposeConsumer(_gl, this);

        if (_vao != 0) _gl.DeleteVertexArray(_vao);
        if (_vbo != 0) _gl.DeleteBuffer(_vbo);
        if (_ebo != 0) _gl.DeleteBuffer(_ebo);

        LockedGlfw.MakeContextCurrent(null);
        _host.DestroyHiddenLayerWindow(_ctxWin);
    }

    private void Enqueue(ICmd cmd)
    {
        if (_disposed) return;
        var seq = Interlocked.Increment(ref _nextCmdSeq);
        _q.Enqueue((seq, cmd));
        Volatile.Write(ref _lastEnqueuedSeq, seq);
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

    private void DrainUntil(long targetSeq)
    {
        LockedGlfw.MakeContextCurrent(_ctxWin);

        EnsureInit();

        // 0) Initialize any newly-seen external shaders BEFORE checkHotReload.
        while (_extInitPending.TryDequeue(out var key))
        {
            lock (_extShaderLock)
            {
                if (_extCache.ContainsKey(key)) continue;

                var ap = new AutoProgram(this, key);

                _extCache[key] = new ExtShaderEntry
                {
                    Prog    = ap,
                    UTex    = new AutoUniform(_gl, this, ap, "uTex"),
                };
            }
        }

        // 1) compile/link any changes (internal + external) for this GL consumer
        ShaderStore.CheckHotReload(_gl, this);

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
        LockedGlfw.MakeContextCurrent(null);
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

        Uniform4F(gl, _uRectPosSize, x, y, w, h);
        Uniform4F(gl, _uRectColor, r, g, b, a);
        Uniform2F(gl, _uRectRes, _w, _h);

        gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, null);

        gl.BindVertexArray(0);
        gl.UseProgram(0);
    }

    private void ExecLayer(GL gl, CodeDrawLayer src, CustomShader? shader)
    {
        if (!src.TryGetLatest(out var tex, out _, out _, out _, out _)) return;

        uint prog;
        int uTexLoc;

        if (shader == null)
        {
            prog = _progBlit;
            uTexLoc = _uBlitTex;
        }
        else
        {
            lock (_extShaderLock)
            {
                if (!_extCache.TryGetValue(shader.Key, out var entry))
                {
                    prog = _progBlit;
                    uTexLoc = _uBlitTex;
                }
                else
                {
                    prog = entry.Prog;
                    uTexLoc = entry.UTex;
                }
            }
        }

        if (prog == 0) return;

        gl.UseProgram(prog);
        gl.BindVertexArray(_vao);
        gl.ActiveTexture(GLEnum.Texture0);
        gl.BindTexture(GLEnum.Texture2D, tex);

        if (uTexLoc >= 0) gl.Uniform1(uTexLoc, 0);

        gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, null);

        gl.BindTexture(GLEnum.Texture2D, 0);
        gl.BindVertexArray(0);
        gl.UseProgram(0);
    }

        private void ExecCustomRect(
        GL gl,
        CustomShader? shader,
        Uniforms uniforms)
    {
        uint prog;

        if (shader == null)
        {
            // Default to engine rect
            prog = _progRect;
        }
        else
        {
            ExtShaderEntry? entry;
            lock (_extShaderLock)
            {
                _extCache.TryGetValue(shader.Key, out entry);
            }

            if (entry == null)
            {
                prog = _progRect;
            }
            else
            {
                prog = entry.Prog;
            }
        }

        if (prog == 0) return;

        gl.UseProgram(prog);
        gl.BindVertexArray(_vao);

        // User uniforms
        int usedTexUnits = 0;
        if (shader != null && uniforms.Values.Length > 0)
        {
            ApplyUserUniforms(gl, prog, shader.Key, uniforms, out usedTexUnits);
        }

        gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, null);

        for (int i = 0; i < usedTexUnits; i++)
        {
            gl.ActiveTexture(GLEnum.Texture0 + i);
            gl.BindTexture(GLEnum.Texture2D, 0);
        }
        gl.ActiveTexture(GLEnum.Texture0);
        gl.BindVertexArray(0);
        gl.UseProgram(0);
    }

    private void ApplyUserUniforms(GL gl, uint prog, ShaderKey key, Uniforms uniforms, out int usedTexUnits)
    {
        usedTexUnits = 0;

        Dictionary<string, int> locMap;

        // Only protect access to _extCache / entry.UserLocCache structure.
        lock (_extShaderLock)
        {
            if (!_extCache.TryGetValue(key, out var entry))
                return;

            if (!entry.UserLocCache.TryGetValue(prog, out locMap!))
            {
                locMap = new Dictionary<string, int>(StringComparer.Ordinal);
                entry.UserLocCache[prog] = locMap;
            }
        }

        // No locks while doing GL calls.
        var nextTexUnit = 0;

        foreach (var u in uniforms.Values)
        {
            if (!locMap.TryGetValue(u.Name, out var loc))
            {
                loc = gl.GetUniformLocation(prog, u.Name);
                locMap[u.Name] = loc;
            }

            if (loc < 0) continue;

            switch (u.Type)
            {
                case UniformType.FLOAT1: gl.Uniform1(loc, u.A); break;
                case UniformType.FLOAT2: gl.Uniform2(loc, u.A, u.B); break;
                case UniformType.FLOAT3: gl.Uniform3(loc, u.A, u.B, u.C); break;
                case UniformType.FLOAT4: gl.Uniform4(loc, u.A, u.B, u.C, u.D); break;
                case UniformType.TEX_2D:
                    if (u.TexRef is null) break;
                    gl.ActiveTexture(GLEnum.Texture0 + nextTexUnit);
                    gl.BindTexture(GLEnum.Texture2D, u.TexRef.Value.Tex);
                    gl.Uniform1(loc, nextTexUnit);
                    nextTexUnit++;
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        usedTexUnits = nextTexUnit;
    }
}