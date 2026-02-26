using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using MarcoZechner.CodeDrawDotNet.DrawLayer.Commands;
using MarcoZechner.CodeDrawDotNet.Images;
using MarcoZechner.CodeDrawDotNet.Shaders;
using MarcoZechner.CodeDrawDotNet.Window;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using Monitor = System.Threading.Monitor;
using UniformType = MarcoZechner.CodeDrawDotNet.Shaders.UniformType;
using SilkUniformType = Silk.NET.OpenGL.UniformType;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer;

public sealed unsafe partial class CodeDrawLayer : IDisposable, IShaderConsumer
{

    
    private static int _nextLayerId;
    public int LayerId { get; } = Interlocked.Increment(ref _nextLayerId);

    internal sealed class LayerIdBox(CodeDrawLayer layer)
    {
        public CodeDrawLayer Layer { get; } = layer;
    }
    
    // --- time base for uTime ---
    private readonly long _timeStartTicks = Stopwatch.GetTimestamp();

    public float TimeAliveSeconds //TODO stop if disposed?
    {
        get {
            var now = Stopwatch.GetTimestamp();
            var dt = (now - _timeStartTicks) / (double)Stopwatch.Frequency;
            return (float)dt;
        }
    }

    // ---- external shader cache (now supports both layer-copy + custom-rect) ----
    private readonly Lock _extShaderLock = new();
    private readonly HashSet<ShaderKey> _extKnown = [];
    private readonly ConcurrentQueue<ShaderKey> _extInitPending = new();

    private sealed class ExtShaderEntry
    {
        public AutoProgram Prog = null!;

        public AutoUniform UPosSize = null!;
        public AutoUniform URes = null!;

        // Common (layer copy)
        public AutoUniform UTex = null!;

        // Per-program user uniform location cache:
        // programHandle -> (uniformName -> location)
        public readonly Dictionary<uint, UniformReflection> ReflectionCache = new();
    }
    
    private sealed class UniformInfo
    {
        public int Loc;
        public SilkUniformType Type;
        public int Size;
    }

    private sealed class UniformReflection
    {
        public readonly Dictionary<string, UniformInfo> ByName = new(StringComparer.Ordinal);
        public readonly HashSet<string> WarnedMissingFromShader = new(StringComparer.Ordinal);
        public readonly HashSet<string> WarnedTypeMismatch = new(StringComparer.Ordinal);
        public readonly HashSet<string> WarnedMissingFromCode = new(StringComparer.Ordinal);
        public readonly HashSet<string> WarnedReservedSet = new(StringComparer.Ordinal);

        // Per-draw tracking:
        public readonly HashSet<string> TouchedThisDraw = new(StringComparer.Ordinal);

        public int ProgramVersionTag; // to detect program relink/recreate
    }

    private readonly Dictionary<ShaderKey, ExtShaderEntry> _extCache = new();

    private void ScheduleExternalShader(CodeDrawShader? shader)
    {
        if (shader == null) return;

        // thread-safe, GL-free
        lock (_extShaderLock)
        {
            if (_extKnown.Add(shader.Key))
                _extInitPending.Enqueue(shader.Key);
        }
    }

    public BlendMode GetBlendMode() => _blendMode;

    private void ApplyBlendMode(BlendMode? force = null)
    {
        if (force != null) _blendMode = force.Value;

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

            case BlendMode.PREMULTIPLIED_ALPHA:
                _gl.Enable(GLEnum.Blend);
                _gl.BlendEquationSeparate(GLEnum.FuncAdd, GLEnum.FuncAdd);
                _gl.BlendFunc(GLEnum.One, GLEnum.OneMinusSrcAlpha);
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

            case BlendMode.SUBTRACT:
                _gl.Enable(GLEnum.Blend);
                _gl.BlendEquationSeparate(GLEnum.FuncReverseSubtract, GLEnum.FuncReverseSubtract); // dst - src
                _gl.BlendFunc(GLEnum.One, GLEnum.One);
                break;

            case BlendMode.INVERSE_SUBTRACT:
                _gl.Enable(GLEnum.Blend);
                _gl.BlendEquationSeparate(GLEnum.FuncSubtract, GLEnum.FuncSubtract); // src - dst
                _gl.BlendFunc(GLEnum.One, GLEnum.One);
                break;

            case BlendMode.MIN:
                _gl.Enable(GLEnum.Blend);
                _gl.BlendEquationSeparate(GLEnum.Min, GLEnum.Min);
                // BlendFunc is ignored for MIN/MAX, but leaving a sane value avoids weird driver edge cases.
                _gl.BlendFunc(GLEnum.One, GLEnum.One);
                break;

            case BlendMode.MAX:
                _gl.Enable(GLEnum.Blend);
                _gl.BlendEquationSeparate(GLEnum.Max, GLEnum.Max);
                _gl.BlendFunc(GLEnum.One, GLEnum.One);
                break;

            case BlendMode.RGB_ALPHA_KEEP_DST_A:
                _gl.Enable(GLEnum.Blend);
                _gl.BlendEquationSeparate(GLEnum.FuncAdd, GLEnum.FuncAdd);
                _gl.BlendFuncSeparate(
                    GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha, // RGB
                    GLEnum.Zero,     GLEnum.One              // A: keep dst
                );
                break;

            default:
                _gl.Disable(GLEnum.Blend);
                break;
        }
    }

    private bool _clearFirst;
    public bool Debug;

    private BlendMode _blendMode = BlendMode.SOURCE_OVER_ALPHA;

    private readonly SharedGlfwHost _host;
    private readonly WindowHandle* _ctxWin;
    private GL _gl = null!; // only valid on render thread, but we set it there so it doesn't need to be nullable

    private Buffer _pub;  // published (read-only during render)
    private Buffer _work; // current command stream target
    private Buffer _tmp;  // temp for postprocess

    private Publication _pubInfo;
    private long _frameSeq;

    private bool _disposed;
    public bool IsDisposed => Volatile.Read(ref _disposed);

    private readonly ConcurrentQueue<(long seq, ICmd cmd)> _q = new();
    private long _nextCmdSeq;
    private long _lastEnqueuedSeq;
    private long _lastRenderedCmdSeq;

    private readonly Thread _renderThread;
    private readonly AutoResetEvent _renderKick = new(false);
    private volatile bool _renderThreadStop;
    private long _requestedRenderSeq; // target seq requested by any thread
    private long _completedRenderSeq; // last seq rendered (same as _lastRenderedCmdSeq, but atomic)
    
    private readonly object _waitLock = new();   // Not "Lock" because we use Monitor.Wait/Pulse which isn't compatible with the "Lock" type.

    private readonly ConcurrentQueue<nint> _retireFences = new();

    private bool _clearRequested = true;
    private (float r, float g, float b, float a) _clearColor = (0f, 0f, 0f, 0f);

    private bool _initComplete;
    internal uint _vao, _vbo, _ebo;

    private AutoProgram _progRect = null!, _progBlit = null!, _progLayerRect = null!;
    private AutoUniform _uRectPosSize = null!, _uRectColor = null!, _uRectRes = null!, _uRectXf = null!;
    private AutoUniform _uBlitTex = null!;
    private AutoUniform _uLayerRectDstRectPx = null!, _uLayerRectDstResPx = null!, _uLayerRectSrcUvRect = null!, _uLayerRectTex = null!;
    
    private AutoProgram _progSdf = null!;
    private AutoUniform _uSdfPosSize = null!, _uSdfRes = null!, _uSdfXf = null!;
    private AutoUniform _uMaxBlendSdfs = null!;
    private uint _sdfSsbo; //TODO: make "AutoSSBO" similar to "AutoUniform"
    private uint _sdfMatSsbo;
    private uint _sdfRuleSsbo;

    internal AutoProgram _progImageRect = null!;
    internal AutoUniform _uImageDstRectPx = null!, _uImageDstResPx = null!, _uImageSrcUvRect = null!, _uImageTex = null!;
    
    private int _w, _h;

    private readonly AutoResetEvent _published = new(false);


    public string DebugName { get; }

    public CodeDrawLayer(int w = 800, int h = 600, string label = "Unknown Layer")
    {
        DebugName = $"[Layer:{label}]";
        _host = CodeDrawHost.RequireRunningHost();

        CodeDrawHost.RequireRunningApp().OwnLayer(this);
        
        _ctxWin = _host.CreateHiddenLayerWindow(1, 1, "layer-ctx");

        _renderThread = new Thread(RenderThreadMain)
            { IsBackground = true, Name = $"LayerRenderer:{label}" };
        _renderThread.Start();

        RequestLayerSize(w, h);
    }

    private CodeDrawWindow? _debugWindow;
    public void OpenDebugWindow()
    {
        if (_debugWindow != null) return;
        _debugWindow = new CodeDrawWindow(_w, _h, 100, 100, DebugName + " Debug");
        _debugWindow.SetPresentedLayer(this);
    }

    public void CloseDebugWindow()
    {
        _debugWindow?.Dispose();
        _debugWindow = null;
    }

    private void EnsureInit()
    {
        if (_initComplete) return;
        _initComplete = true;

        (_vao, _vbo, _ebo) = ShaderCompiler.CreateFullScreenQuad(_gl);

        _progRect     = new AutoProgram(this, ShaderPath.Engine("rect"));
        _uRectPosSize = new AutoUniform(_gl, this, _progRect, "uPosSize");
        _uRectColor   = new AutoUniform(_gl, this, _progRect, "uColor");
        _uRectRes     = new AutoUniform(_gl, this, _progRect, "uRes");
        _uRectXf   = new AutoUniform(_gl, this, _progRect, "uXf");

        _progBlit = new AutoProgram(this, ShaderPath.Engine("layerShader"));
        _uBlitTex = new AutoUniform(_gl, this, _progBlit, "uTex");

        _progLayerRect       = new AutoProgram(this, ShaderPath.Engine("layerRectShader"));
        _uLayerRectDstRectPx = new AutoUniform(_gl, this, _progLayerRect, "uDstRectPx");
        _uLayerRectDstResPx  = new AutoUniform(_gl, this, _progLayerRect, "uDstResPx");
        _uLayerRectSrcUvRect = new AutoUniform(_gl, this, _progLayerRect, "uSrcUvRect");
        _uLayerRectTex       = new AutoUniform(_gl, this, _progLayerRect, "uTex");
        
        _progSdf = new AutoProgram(this, ShaderPath.Engine("sdf"));
        _uSdfPosSize         = new AutoUniform(_gl, this, _progSdf, "uPosSize");
        _uSdfRes             = new AutoUniform(_gl, this, _progSdf, "uRes");
        _uSdfXf              = new AutoUniform(_gl, this, _progSdf, "uXf");
        _uMaxBlendSdfs       = new AutoUniform(_gl, this, _progSdf, "uMaxBlendSdfs");
        _sdfSsbo = _gl.GenBuffer();
        _sdfMatSsbo = _gl.GenBuffer();
        _sdfRuleSsbo = _gl.GenBuffer();
        
        _progImageRect = new AutoProgram(this, ShaderPath.Engine("imageRectShader"));
        _uImageDstRectPx = new AutoUniform(_gl, this, _progImageRect, "uDstRectPx");
        _uImageDstResPx  = new AutoUniform(_gl, this, _progImageRect, "uDstResPx");
        _uImageSrcUvRect = new AutoUniform(_gl, this, _progImageRect, "uSrcUvRect");
        _uImageTex       = new AutoUniform(_gl, this, _progImageRect, "uTex");
    }

    public void Dispose()
    {
        CloseDebugWindow();
        
        if (Interlocked.Exchange(ref _disposed, true))
            return;

        Console.WriteLine("Layer " + DebugName + " Disposing...");
        
        try { CodeDrawHost.RequireRunningApp().DisownLayer(LayerId); } catch { /* ignored */ }
        
        _published.Set();

        _renderThreadStop = true;
        _renderKick.Set();
        if (_renderThread.IsAlive) _renderThread.Join();
        
        _host.DestroyHiddenLayerWindow(_ctxWin);
    }

    private int _renderThreadId; // 0 = unknown/not started yet

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsRenderThread()
    {
        var id = Volatile.Read(ref _renderThreadId);
        return id != 0 && Environment.CurrentManagedThreadId == id;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MarkCurrentThreadAsRenderThread()
    {
        Volatile.Write(ref _renderThreadId, Environment.CurrentManagedThreadId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ClearRenderThreadMark()
    {
        Volatile.Write(ref _renderThreadId, 0);
    }

    internal void Enqueue(ICmd cmd)
    {
        if (_disposed) return;

        if (IsRenderThread())
            throw new InvalidOperationException(
                "BUG: Enqueue() was called from the render/present thread. " +
                "This can deadlock or break ordering. " +
                "Queue commands from update/user threads only.");

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
        var p = _pubInfo;
        seq = Volatile.Read(ref p.Seq);
        if (seq == 0) { tex = 0; w = h = 0; fence = 0; return false; }

        tex = _pub.Tex;
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

    private void RequestRenderTo(long targetSeq, bool wait, int timeoutMs)
    {
        if (_disposed) return;

        // Publish desired target (monotonic max)
        while (true)
        {
            var cur = Volatile.Read(ref _requestedRenderSeq);
            if (cur >= targetSeq) break;
            if (Interlocked.CompareExchange(ref _requestedRenderSeq, targetSeq, cur) == cur) break;
        }

        _renderKick.Set();

        if (!wait) return;

        var sw = timeoutMs == Timeout.Infinite ? null : Stopwatch.StartNew();
        lock (_waitLock)
        {
            while (!_disposed && Volatile.Read(ref _completedRenderSeq) < targetSeq)
            {
                if (timeoutMs == Timeout.Infinite)
                {
                    Monitor.Wait(_waitLock);
                }
                else
                {
                    var remaining = timeoutMs - (int)sw!.ElapsedMilliseconds;
                    if (remaining <= 0) break;
                    Monitor.Wait(_waitLock, remaining);
                }
            }
        }
    }
    
    private void RenderThreadMain()
    {
        try
        {
            MarkCurrentThreadAsRenderThread();
            
            LockedGlfw.MakeContextCurrent(_ctxWin);
            LockedGlfw.SwapInterval(0);
            _gl = GL.GetApi(LockedGlfw.GetProcAddress); 

            EnsureInit();

            while (!_renderThreadStop && !_disposed)
            {
                _renderKick.WaitOne(1);

                if (_disposed) break;

                var target = Volatile.Read(ref _requestedRenderSeq);
                if (Volatile.Read(ref _lastRenderedCmdSeq) >= target)
                    continue;

                DrainUntil(target);

                Volatile.Write(ref _completedRenderSeq, Volatile.Read(ref _lastRenderedCmdSeq));
                lock (_waitLock) Monitor.PulseAll(_waitLock);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{DebugName} RenderThread error: {ex}");
        }
        finally
        {
            DeleteBuffer(ref _pub);
            DeleteBuffer(ref _work);
            DeleteBuffer(ref _tmp);
            DeleteBuffer(ref _cpu);
            
            if (_sdfSsbo != 0) _gl.DeleteBuffer(_sdfSsbo);
            if (_sdfMatSsbo != 0) _gl.DeleteBuffer(_sdfMatSsbo);
            if (_sdfRuleSsbo != 0) _gl.DeleteBuffer(_sdfRuleSsbo);
            _sdfSsbo = 0;
            _sdfMatSsbo = 0;
            _sdfRuleSsbo = 0;

            ShaderStore.DisposeConsumer(_gl, this);
            ImageStore.DisposeConsumer(_gl, this);

            if (_vao != 0) _gl.DeleteVertexArray(_vao);
            if (_vbo != 0) _gl.DeleteBuffer(_vbo);
            if (_ebo != 0) _gl.DeleteBuffer(_ebo);
            
            try { LockedGlfw.MakeContextCurrent(null); } catch { /* ignored */ }

            ClearRenderThreadMark();
        }
    }
    
    private void DeleteBuffer(ref Buffer b)
    {
        if (b.Fence != 0) { _gl.DeleteSync(b.Fence); b.Fence = 0; }
        if (b.Fbo != 0) { _gl.DeleteFramebuffer(b.Fbo); b.Fbo = 0; }
        if (b.Tex != 0) { _gl.DeleteTexture(b.Tex); b.Tex = 0; }
        b.W = b.H = 0;
    }
    
    private void CreateBuffer(ref Buffer b, int w, int h)
    {
        var tex = _gl.GenTexture();
        _gl.BindTexture(GLEnum.Texture2D, tex);
        _gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.Rgba8, (uint)w, (uint)h, 0,
            GLEnum.Rgba, GLEnum.UnsignedByte, null);
        _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.BindTexture(GLEnum.Texture2D, 0);

        var fbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(GLEnum.Framebuffer, fbo);
        _gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0, GLEnum.Texture2D, tex, 0);
        _gl.BindFramebuffer(GLEnum.Framebuffer, 0);

        b.Tex = tex;
        b.Fbo = fbo;
        b.W = w;
        b.H = h;
    }
    
    private void DrainUntil(long targetSeq)
    {
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
                    Prog = ap,
                    UPosSize = new AutoUniform(_gl, this, ap, "uPosSize"),
                    URes     = new AutoUniform(_gl, this, ap, "uRes"), 
                    UTex = new AutoUniform(_gl, this, ap, "uTex"),
                };
            }
        }

        // 1) compile/link any changes (internal + external) for this GL consumer
        ShaderStore.CheckHotReload(_gl, this);

        RetireRequestedFences();

        _cpuDirty = false;
        if (_cpuRgba8 != null)
            Array.Clear(_cpuRgba8);
        
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

        _gl.BindFramebuffer(GLEnum.Framebuffer, _work.Fbo);
        _gl.Viewport(0, 0, (uint)_w, (uint)_h);
        _gl.Disable(GLEnum.DepthTest);
        ApplyBlendMode();

        lastResize?.Exec(_gl, this);

        _gl.BindFramebuffer(GLEnum.Framebuffer, _work.Fbo);
        _gl.Viewport(0, 0, (uint)_w, (uint)_h);

        if (_clearFirst || _clearRequested)
        {
            local.Insert(0, (0, new CmdClear(_clearColor.r, _clearColor.g, _clearColor.b, _clearColor.a)));
            _clearRequested = false;
        }
        else if (local.Count > 0 && local[0].cmd is not CmdClear)
        {
            // ensure _work starts as last published frame
            _gl.BindFramebuffer(GLEnum.Framebuffer, _work.Fbo);
            _gl.Viewport(0, 0, (uint)_w, (uint)_h);
            CopyPubToWork();
        }

        foreach (var (_, cmd) in local)
            cmd.Exec(_gl, this);

        if (_cpuDirty)
        {
            ExecCpuPush(_gl);
            
            ExecCpuComposite(_gl);
        }
        
        _gl.Finish();

        (_pub, _work) = (_work, _pub);

        _pubInfo.W = _w;
        _pubInfo.H = _h;
        _pubInfo.Fence = 0;
        Volatile.Write(ref _pubInfo.Seq, Interlocked.Increment(ref _frameSeq));

        _published.Set();

        _gl.BindFramebuffer(GLEnum.Framebuffer, 0);
    }

    private void RetireRequestedFences()
    {
        while (_retireFences.TryDequeue(out var f))
        {
            if (_pubInfo.Fence == f) _pubInfo.Fence = 0;

            if (_pub.Fence  == f) _pub.Fence  = 0;
            if (_work.Fence == f) _work.Fence = 0;
            if (_tmp.Fence  == f) _tmp.Fence  = 0;

            _gl.DeleteSync(f);
        }
    }

    private void CopyPubToWork()
    {
        _gl.UseProgram(_progBlit);
        _gl.BindVertexArray(_vao);
        _gl.ActiveTexture(GLEnum.Texture0);
        _gl.BindTexture(GLEnum.Texture2D, _pub.Tex);
        if (_uBlitTex >= 0) GlHelper.Uniform1(_gl, _uBlitTex, 0);

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

        DeleteBuffer(ref _pub);
        DeleteBuffer(ref _work);
        DeleteBuffer(ref _tmp);
        CreateBuffer(ref _cpu, w, h);

        CreateBuffer(ref _pub,  w, h);
        CreateBuffer(ref _work, w, h);
        CreateBuffer(ref _tmp,  w, h);
        CreateBuffer(ref _cpu, w, h);

        _pubInfo = new Publication { Fence = 0, W = w, H = h, Seq = 0 };

        _published.Set();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Contains(ReadOnlySpan<string> a, string s)
    {
        foreach (var t in a)
            if (string.Equals(t, s, StringComparison.Ordinal))
                return true;
        return false;
    }
    
    private bool ApplyUserUniforms(
        GL gl,
        uint prog,
        ShaderKey key,
        Uniforms uniforms,
        bool providesTexture,
        ReadOnlySpan<string> reservedUniforms,
        out int usedTexUnits)
    {
        usedTexUnits = 0;

        var reflection = GetOrCreateReflection(gl, key, prog);

        reflection.TouchedThisDraw.Clear();

        foreach (var n in reservedUniforms)
            reflection.TouchedThisDraw.Add(n);

        var nextTexUnit = providesTexture ? 1 : 0;

        foreach (var u in uniforms.Values)
        {
            if (Contains(reservedUniforms, u.Name))
            {
                if (reflection.WarnedReservedSet.Add(u.Name))
                {
                    Console.WriteLine(
                        $"[Warn] {DebugName} uniform '{u.Name}' is a reserved built-in but is set in code for shader '{key}'. " +
                        $"(program=0x{prog:X})");
                }
                continue;
            }

            if (!reflection.ByName.TryGetValue(u.Name, out var info) || info.Loc < 0)
            {
                if (reflection.WarnedMissingFromShader.Add(u.Name))
                {
                    Console.WriteLine(
                        $"[Warn] {DebugName} uniform '{u.Name}' was set in code but not found/active in shader '{key}'. " +
                        $"(program=0x{prog:X})");
                }
                continue;
            }

            if (!IsCompatible(u.Type, info.Type))
            {
                if (reflection.WarnedTypeMismatch.Add(u.Name))
                {
                    Console.WriteLine(
                        $"[Error] {DebugName} uniform type mismatch for '{u.Name}' in shader '{key}'. " +
                        $"Code={u.Type}, Shader={info.Type}. (program=0x{prog:X})");
                }

                throw new InvalidOperationException(
                    $"Uniform type mismatch for '{u.Name}' in shader '{key}': Code={u.Type}, Shader={info.Type}.");
            }

            reflection.TouchedThisDraw.Add(u.Name);

            switch (u.Type)
            {
                case UniformType.FLOAT1: GlHelper.Uniform1(gl, info.Loc, u.A); break;
                case UniformType.FLOAT2: GlHelper.Uniform2(gl, info.Loc, u.A, u.B); break;
                case UniformType.FLOAT3: GlHelper.Uniform3(gl, info.Loc, u.A, u.B, u.C); break;
                case UniformType.FLOAT4: GlHelper.Uniform4(gl, info.Loc, u.A, u.B, u.C, u.D); break;

                case UniformType.TEX_2D:
                {
                    uint tex;

                    if (u.LayerRef is { IsDisposed: false } layer)
                    {
                        if (!layer.TryGetLatest(out var t, out _, out _, out _, out _))
                            return false;
                        tex = t;
                    }
                    else return false;

                    if (tex == 0) return false;

                    gl.ActiveTexture(GLEnum.Texture0 + nextTexUnit);
                    gl.BindTexture(GLEnum.Texture2D, tex);
                    GlHelper.Uniform1(gl, info.Loc, nextTexUnit);

                    nextTexUnit++;
                    break;
                }

                case UniformType.MAT3X3: {
                    // IMPORTANT: Your Matrix3x3 is row-major in C#.
                    // GLSL expects column-major data layout unless transpose=true.
                    // Easiest: upload with transpose=true and send row-major as-is.
                    GlHelper.UniformMat3(gl, info.Loc, u.Mat, true);
                    break;
                }
                
                case UniformType.COLOR: GlHelper.Uniform4(gl, info.Loc, u.ColorF.R, u.ColorF.G, u.ColorF.B, u.ColorF.A); break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        foreach (var (name, info) in reflection.ByName)
        {
            if (info.Loc < 0) continue;
            if (name.StartsWith("gl_", StringComparison.Ordinal)) continue;
            if (reservedUniforms.Contains(name)) continue;

            if (reflection.TouchedThisDraw.Contains(name)) continue;
            if (!reflection.WarnedMissingFromCode.Add(name)) continue;

            Console.WriteLine(
                $"[Warn] {DebugName} shader '{key}' has uniform '{name}' active but it was not set in code. " +
                $"(program=0x{prog:X})");
        }

        usedTexUnits = nextTexUnit;
        return true;
    }
    
    private UniformReflection GetOrCreateReflection(GL gl, ShaderKey key, uint prog)
    {
        ExtShaderEntry? entry;
        UniformReflection? refl;

        lock (_extShaderLock)
        {
            if (!_extCache.TryGetValue(key, out entry))
                return Reflect(gl, prog); // no cache entry -> still reflect

            entry.ReflectionCache.TryGetValue(prog, out refl);
        }

        if (refl != null) return refl;

        var built = Reflect(gl, prog);

        lock (_extShaderLock)
        {
            if (_extCache.TryGetValue(key, out entry))
            {
                entry.ReflectionCache[prog] = built;
            }
        }

        return built;
    }

    private static UniformReflection Reflect(GL gl, uint prog)
    {
        var r = new UniformReflection();

        gl.GetProgram(prog, GLEnum.ActiveUniforms, out int count);

        for (uint i = 0; i < (uint)count; i++)
        {
            // Silk overload: returns name as string
            var name = gl.GetActiveUniform(prog, i, out var size, out var type);

            // Some drivers return array uniforms as "arr[0]" -> normalize
            var loc = gl.GetUniformLocation(prog, name);
            var info = new UniformInfo { Loc = loc, Type = type, Size = size };

            r.ByName[name] = info;

            if (!name.EndsWith("[0]", StringComparison.Ordinal)) continue;

            var baseName = name[..^3];
            r.ByName[baseName] = info;
        }

        return r;
    }

    private static bool IsCompatible(UniformType expected, SilkUniformType  actual)
    {
        return expected switch
        {
            UniformType.FLOAT1 => actual == SilkUniformType.Float,
            UniformType.FLOAT2 => actual == SilkUniformType.FloatVec2,
            UniformType.FLOAT3 => actual == SilkUniformType.FloatVec3,
            UniformType.FLOAT4 => actual == SilkUniformType.FloatVec4,
            UniformType.TEX_2D => actual == SilkUniformType.Sampler2D,
            UniformType.MAT3X3 => actual == SilkUniformType.FloatMat3,
            UniformType.COLOR => actual == SilkUniformType.FloatVec4,
            _ => false
        };
    }
}