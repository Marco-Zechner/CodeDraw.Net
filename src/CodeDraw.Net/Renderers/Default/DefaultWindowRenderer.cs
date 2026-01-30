using System.Diagnostics;
using MarcoZechner.CodeDrawDotNet.Api;
using MarcoZechner.CodeDrawDotNet.Api.Graphics.Actions;
using MarcoZechner.CodeDrawDotNet.Engine;
using MarcoZechner.CodeDrawDotNet.Interfaces;
using MarcoZechner.DiagnosticsDotNet;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Renderers.Default;

public sealed unsafe class DefaultWindowRenderer : AbstractWindowRenderer
{
    static DefaultWindowRenderer()
    {
        CodeDrawRuntime.Init(CodeDrawHost.Instance);
    }

    private uint _canvasFbo, _canvasTex, _canvasDepthStencilRb;
    private int _canvasW, _canvasH;

    private uint _presentVao, _presentVbo;
    private uint _presentProgram;
    private int _presentLocTex;

    private readonly SharedLayer _canvasLayer = new();

    public ILayerHandle CanvasLayer => _canvasLayer;

    private volatile bool _presentDirty = true;

    private readonly RateMeter _fps = new(0.25);

    public DefaultWindowRenderer()
    {
        FpsGetter = () => _fps.Ewma;
    }

    protected override void RunLoop()
    {
        if (CodeDrawHost.Instance.WithGlfw(glfw => glfw.GetCurrentContext() != Window))
            throw new InvalidOperationException("Render thread lost current context!");

        var gl = Gl!;
        Console.WriteLine($"[DBG] RunLoop started for {Title} on thread {Environment.CurrentManagedThreadId}");
        // Good default: enable sRGB when drawing into sRGB targets
        gl.Enable(EnableCap.FramebufferSrgb);

        // main loop
        var warnThresholdMs = PublicWindow!.LongActionWarnMs;

        var frameTimer = Stopwatch.StartNew();
        double targetMs = (PublicWindow!.VSync || PublicWindow!.TargetFps <= 0)
            ? 0.0
            : 1000.0 / PublicWindow!.TargetFps;

        CodeDrawHost.Instance.WithGlfw(glfw => glfw.SwapInterval(PublicWindow!.VSync ? 1 : 0));
        while (Running)
        {
            if ((Frames % 120) == 0)
                Console.WriteLine($"[DBG] {Title} alive. Frames={Frames} presentDirty={_presentDirty} canvas={_canvasW}x{_canvasH}");
            DebugCheckContext("loop-top");
            DebugCheckGl(gl, "loop-top");
            frameTimer.Restart();

            // Size & canvas
            var (fbW, fbH) = GetFramebufferSizeCached();

            if (fbW <= 0 || fbH <= 0)
            {
                Thread.Sleep(8);
                continue;
            }

            // First creation: must create no matter what
            if (_canvasFbo == 0)
            {
                EnsureCanvas(gl, fbW, fbH);
            }
            else
            {
                // If we are in interactive resize: NEVER rebuild canvas.
                if (!IsResizeInProgress())
                {
                    if (fbW != _canvasW || fbH != _canvasH)
                        EnsureCanvas(gl, fbW, fbH);
                }
                else
                {
                    // keep presenting old canvas while dragging
                    _presentDirty = true;
                }
            }

            // 1) See if there is a sealed frame to execute
            var hadFrame = TryDequeueFrame(out long token, out var batch);

            if (hadFrame)
            {
                DebugCheckGl(gl, "hadFrame-start");
                // Render into the persistent canvas
                BindCanvas(gl);
                DebugCheckGl(gl, "after BindCanvas");
                foreach (var cmd in batch!)
                {
                    var sw = Stopwatch.StartNew();
                    switch (cmd)
                    {
                        case IRenderAction act:
                            act.Execute(gl, null, Window, _canvasW, _canvasH); //TODO: pass glfw again
                            break;
                        case DrawLayerCommand dla:
                            //TODO: progress :) now it works for a short moment and then freezes. that was also an issue in legacy where i had a solution for it. so look there
                            var src = (SharedLayer)dla.Layer;
                            //
                            // // If another window wrote this layer, wait until its latest write is complete
                            ConsumerWaitBeforeSampling(gl, src);

                            RenderLayerTexture(src.Tex, _canvasW, _canvasH, dla.Premultiply);
                            break;
                        default:
                            throw new NotSupportedException($"Unknown render command: {cmd.GetType().Name}");
                    }
                    sw.Stop();
                    if (warnThresholdMs > 0 && sw.ElapsedMilliseconds > warnThresholdMs)
                        Console.WriteLine($"[Render Watchdog] {cmd.GetType().Name} took {sw.ElapsedMilliseconds} ms");

                    DebugCheckGl(gl, $"after cmd {cmd.GetType().Name}");
                }
                Unbind(gl);
                DebugCheckGl(gl, "after Unbind");

                ProducerFenceAfterCanvasWrite(gl, _canvasLayer);
                DebugCheckGl(gl, "after ProducerFence");

                // Present once
                PresentCanvas(gl, fbW, fbH);
                DebugCheckGl(gl, "after PresentCanvas");

                gl.Finish();
                // var swSwap = Stopwatch.StartNew();
                // CodeDrawHost.Instance.GlfwUnsafe.SwapBuffers(Window);
                DebugCheckContext("before-swap");
                DebugCheckGl(gl, "before-swap");
                CodeDrawHost.Instance.WithGlfw(glfw =>glfw.SwapBuffers(Window));
                // swSwap.Stop();
                // if (swSwap.ElapsedMilliseconds > 5)
                    // Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.ffff} [SWAP] {Title} took {swSwap.ElapsedMilliseconds} ms ${(IsResizeInProgress() ? "(resizing)" : "")}");
                DebugCheckGl(gl, "after SwapBuffers");

                Frames++;
                _fps.Tick();
                _fps.MaybeSample();
                _presentDirty = false;

                // wake waiters
                SignalPresented(token);
            }
            else if (_presentDirty)
            {
                // No new work but we owe a present (fresh window / post-resize)
                PresentCanvas(gl, fbW, fbH);
                DebugCheckGl(gl, "after PresentCanvas");

                gl.Finish();
                // var swSwap = Stopwatch.StartNew();
                // CodeDrawHost.Instance.GlfwUnsafe.SwapBuffers(Window);
                DebugCheckContext("before-swap");
                DebugCheckGl(gl, "before-swap");
                CodeDrawHost.Instance.WithGlfw(glfw =>glfw.SwapBuffers(Window));
                // swSwap.Stop();
                // if (swSwap.ElapsedMilliseconds > 5)
                    // Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.ffff} [SWAP-dirty] {Title} took {swSwap.ElapsedMilliseconds} ms ${(IsResizeInProgress() ? "(resizing)" : "")}");
                DebugCheckGl(gl, "after SwapBuffers");

                Frames++;
                _fps.Tick();
                _fps.MaybeSample();
                _presentDirty = false;
            }

            if (PublicWindow!.VSync) continue;

            if (!hadFrame && !_presentDirty)
            {
                Thread.Yield();
                continue;
            }

            // Throttle to target FPS
            if (targetMs > 0)
            {
                var spent = frameTimer.Elapsed.TotalMilliseconds;
                var sleepMs = MathF.Max(0, (float)(targetMs - spent));
                if (sleepMs >= 1) Thread.Sleep((int)sleepMs); //TODO: causes 60fps max? should sleep 4ms (at 240fps) but it only reaches ~16ms
                else Thread.Yield();
            }
        }

        // Cleanup canvas
        DestroyCanvas(Gl!);

        if (_presentVbo != 0) gl.DeleteBuffer(_presentVbo);
        if (_presentVao != 0) gl.DeleteVertexArray(_presentVao);
        if (_presentProgram != 0) gl.DeleteProgram(_presentProgram);
        _presentVbo = _presentVao = _presentProgram = 0;
    }


    private void EnsureCanvas(GL gl, int w, int h)
    {
        Console.WriteLine("EnsureCanvas: " + w + "x" + h);

        if (w == _canvasW && h == _canvasH && _canvasFbo != 0) return;

        // Create new
        uint newTex = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, newTex);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Srgb8Alpha8, (uint)w, (uint)h, 0,
                      PixelFormat.Rgba, PixelType.UnsignedByte, null);

        uint newRb = gl.GenRenderbuffer();
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, newRb);
        gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8, (uint)w, (uint)h);

        uint newFbo = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, newFbo);
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                TextureTarget.Texture2D, newTex, 0);
        gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment,
                                   RenderbufferTarget.Renderbuffer, newRb);

        var status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
            throw new InvalidOperationException($"Canvas FBO incomplete: {status}");

        // Destroy old
        DestroyCanvas(gl);

        // Swap in new
        _canvasTex = newTex;
        _canvasDepthStencilRb = newRb;
        _canvasFbo = newFbo;
        _canvasW = w; _canvasH = h;

        _canvasLayer.Fbo = _canvasFbo;
        _canvasLayer.Tex = _canvasTex;
        _canvasLayer.DepthStencilRb = _canvasDepthStencilRb;
        _canvasLayer.Width = _canvasW;
        _canvasLayer.Height = _canvasH;

        // Initialize new canvas to window clear color
        gl.ClearColor(PublicWindow!.ClearColor.R, PublicWindow!.ClearColor.G,
                      PublicWindow!.ClearColor.B, PublicWindow!.ClearColor.A);
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

        ProducerFenceAfterCanvasWrite(gl, _canvasLayer);

        // Present once after resize
        _presentDirty = true;

        // Unbind for hygiene
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        gl.BindTexture(TextureTarget.Texture2D, 0);
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
    }

    private void DestroyCanvas(GL gl)
    {
        foreach (var f in _canvasLayer.DrainFencesForDisposal())
        {
            if (f != 0) gl.DeleteSync(f);
        }

        if (_canvasFbo != 0) { gl.DeleteFramebuffer(_canvasFbo); _canvasFbo = 0; }
        if (_canvasDepthStencilRb != 0) { gl.DeleteRenderbuffer(_canvasDepthStencilRb); _canvasDepthStencilRb = 0; }
        if (_canvasTex != 0) { gl.DeleteTexture(_canvasTex); _canvasTex = 0; }
        _canvasW = _canvasH = 0;
    }

    private void BindCanvas(GL gl)
    {
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _canvasFbo);
        gl.Viewport(0, 0, (uint)_canvasW, (uint)_canvasH);
        // sRGB already enabled in RunLoop()
    }

    private static void Unbind(GL gl)
    {
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    private void PresentCanvas(GL gl, int fbW, int fbH)
    {
        EnsurePresentResources(gl);

        // draw to default backbuffer
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        gl.Viewport(0, 0, (uint)fbW, (uint)fbH);

        // Present must be deterministic:
        gl.Disable(EnableCap.Blend);
        gl.ColorMask(true, true, true, true);

        gl.UseProgram(_presentProgram);

        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _canvasTex);
        gl.Uniform1(_presentLocTex, 0);

        gl.BindVertexArray(_presentVao);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 6);

        gl.BindVertexArray(0);
        gl.BindTexture(TextureTarget.Texture2D, 0);
        gl.UseProgram(0);
    }

    private void EnsurePresentResources(GL gl)
    {
        if (_presentProgram != 0) return;

        const string vs = """
                          #version 330 core
                          layout(location=0) in vec2 a_pos;
                          layout(location=1) in vec2 a_uv;
                          out vec2 v_uv;
                          void main() {
                              v_uv = a_uv;
                              gl_Position = vec4(a_pos, 0.0, 1.0);
                          }
                          """;

        const string fs = """
                          #version 330 core
                          in vec2 v_uv;
                          out vec4 o;
                          uniform sampler2D u_tex;
                          void main() {
                              vec4 c = texture(u_tex, v_uv);
                              // Premultiply once for the OS compositor:
                              o = vec4(c.rgb * c.a, c.a);
                          }
                          """;

        _presentProgram = CompileProgram(gl, vs, fs);
        _presentLocTex = gl.GetUniformLocation(_presentProgram, "u_tex");

        // Fullscreen quad (two triangles), NDC coords
        // pos(x,y), uv(u,v)
        float[] quad =
        {
            // tri 1
            -1f, -1f,  0f, 0f,
            1f, -1f,  1f, 0f,
            1f,  1f,  1f, 1f,
            // tri 2
            -1f, -1f,  0f, 0f,
            1f,  1f,  1f, 1f,
            -1f,  1f,  0f, 1f,
        };

        _presentVao = gl.GenVertexArray();
        _presentVbo = gl.GenBuffer();

        gl.BindVertexArray(_presentVao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _presentVbo);
        unsafe
        {
            fixed (float* p = quad)
            {
                gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(quad.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
            }
        }

        // a_pos
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);

        // a_uv
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        gl.BindVertexArray(0);
    }

    private static uint CompileProgram(GL gl, string vsSrc, string fsSrc)
    {
        uint vs = Compile(ShaderType.VertexShader, vsSrc);
        uint fs = Compile(ShaderType.FragmentShader, fsSrc);

        uint p = gl.CreateProgram();
        gl.AttachShader(p, vs);
        gl.AttachShader(p, fs);
        gl.LinkProgram(p);

        gl.GetProgram(p, ProgramPropertyARB.LinkStatus, out int linked);
        if (linked == 0)
        {
            string log = gl.GetProgramInfoLog(p);
            gl.DeleteProgram(p);
            throw new Exception($"Program link failed:\n{log}");
        }

        gl.DetachShader(p, vs);
        gl.DetachShader(p, fs);
        gl.DeleteShader(vs);
        gl.DeleteShader(fs);

        return p;

        uint Compile(ShaderType type, string src)
        {
            uint s = gl.CreateShader(type);
            gl.ShaderSource(s, src);
            gl.CompileShader(s);

            gl.GetShader(s, ShaderParameterName.CompileStatus, out int ok);
            if (ok == 0)
            {
                string log = gl.GetShaderInfoLog(s);
                gl.DeleteShader(s);
                throw new Exception($"{type} compile failed:\n{log}");
            }
            return s;
        }
    }

    private uint _blitProgram;
    private int _blitLocTex;
    private int _blitLocPremul;
    private uint _blitVao, _blitVbo;

    private void EnsureBlitResources(GL gl)
    {
        if (_blitProgram != 0) return;

        const string vs = """
                              #version 330 core
                              layout(location=0) in vec2 a_pos;
                              layout(location=1) in vec2 a_uv;
                              out vec2 v_uv;
                              void main(){ v_uv=a_uv; gl_Position=vec4(a_pos,0,1); }
                          """;

        const string fs = """
                              #version 330 core
                              in vec2 v_uv;
                              out vec4 o;
                              uniform sampler2D u_tex;
                              uniform int u_premul;
                              void main(){
                                  vec4 c = texture(u_tex, v_uv);
                                  if(u_premul != 0) c = vec4(c.rgb * c.a, c.a);
                                  o = c;
                              }
                          """;

        _blitProgram = CompileProgram(gl, vs, fs);
        _blitLocTex = gl.GetUniformLocation(_blitProgram, "u_tex");
        _blitLocPremul = gl.GetUniformLocation(_blitProgram, "u_premul");

        // same quad as present (pos+uv)
        float[] quad =
        {
            -1f,-1f, 0f,0f,   1f,-1f, 1f,0f,   1f, 1f, 1f,1f,
            -1f,-1f, 0f,0f,   1f, 1f, 1f,1f,  -1f, 1f, 0f,1f,
        };

        _blitVao = gl.GenVertexArray();
        _blitVbo = gl.GenBuffer();

        gl.BindVertexArray(_blitVao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _blitVbo);
        unsafe { fixed(float* p = quad) gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(quad.Length*sizeof(float)), p, BufferUsageARB.StaticDraw); }

        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4*sizeof(float), (void*)0);

        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4*sizeof(float), (void*)(2*sizeof(float)));

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        gl.BindVertexArray(0);
    }

    private void RenderLayerTexture(uint tex, int fbW, int fbH, bool premultiply)
    {
        var gl = Gl!;
        EnsureBlitResources(gl);

        // Draw into currently bound framebuffer (canvas or default backbuffer)
        gl.Viewport(0, 0, (uint)fbW, (uint)fbH);

        // IMPORTANT: do NOT silently mess with the user's blend mode here.
        // Just make sure we can actually draw (color mask).
        gl.ColorMask(true, true, true, true);

        gl.UseProgram(_blitProgram);

        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, tex);
        gl.Uniform1(_blitLocTex, 0);
        gl.Uniform1(_blitLocPremul, premultiply ? 1 : 0);

        gl.BindVertexArray(_blitVao);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 6);

        gl.BindVertexArray(0);
        gl.BindTexture(TextureTarget.Texture2D, 0);
        gl.UseProgram(0);
    }

    private void ProducerFenceAfterCanvasWrite(GL gl, SharedLayer layer)
    {
        nint newFence = gl.FenceSync(SyncCondition.SyncGpuCommandsComplete, SyncBehaviorFlags.None);
        gl.Flush();

        // Publish newest fence (whatever PushFence does internally)
        layer.PushFence(newFence);
    }

    private static void ConsumerWaitBeforeSampling(GL gl, SharedLayer layer)
    {
        nint fence = Volatile.Read(ref layer.LatestFence);
        if (fence == 0) return;

        // Non-blocking poll (0 timeout). If not ready, skip sampling this frame.
        var res = gl.ClientWaitSync(fence, SyncObjectMask.Bit, 0);

        if (res == GLEnum.TimeoutExpired)
        {
            // Not ready -> DON'T stall the renderer.
            // We'll just draw the last available texture content.
            return;
        }
    }

    // [Conditional("DEBUG")]
    private void DebugCheckContext(string where)
    {
        CodeDrawHost.Instance.WithGlfw(glfw =>
        {
            var cur = glfw.GetCurrentContext();
            if (cur != Window)
            {
                Console.WriteLine($"[CTX LOST] {Title} @ {where}  cur=0x{(nint)cur:X} expected=0x{(nint)Window:X}");
                // Attempt recovery so we can keep collecting evidence:
                glfw.MakeContextCurrent(Window);
                cur = glfw.GetCurrentContext();
                Console.WriteLine($"[CTX REBIND] {Title} @ {where}  now=0x{(nint)cur:X}");
            }
        });
    }

    // [Conditional("DEBUG")]
    private static void DebugCheckGl(GL gl, string where)
    {
        var e = gl.GetError();
        if (e != GLEnum.NoError)
            Console.WriteLine($"[GLERR] {where}: {e}");
    }

    // [Conditional("DEBUG")]
    private static void DebugInjectMagentaClear(GL gl, int fbW, int fbH)
    {
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        gl.Viewport(0, 0, (uint)fbW, (uint)fbH);
        gl.Disable(EnableCap.Blend);
        gl.ColorMask(true, true, true, true);
        gl.ClearColor(1f, 0f, 1f, 1f);
        gl.Clear(ClearBufferMask.ColorBufferBit);
    }
}
