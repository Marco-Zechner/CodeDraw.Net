using System.Diagnostics;
using MarcoZechner.CodeDrawDotNet.Engine.Abstractions;
using MarcoZechner.CodeDrawDotNet.Engine.Impl;
using MarcoZechner.DiagnosticsDotNet;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Api.Renderers;

public unsafe sealed class DefaultWindowRenderer : AbstractWindowRenderer, IHostBootstrap<DefaultWindowRenderer>
{
    private uint _canvasFbo, _canvasTex, _canvasDepthStencilRb;
    private int _canvasW, _canvasH;

    private volatile bool _presentDirty = true;

    public static void EnsureHost()
        => CodeDrawRuntime.Init(CodeDrawHost.Instance);

    private readonly RateMeter _fps = new(0.25);

    public DefaultWindowRenderer() 
    {
        _fpsGetter = () => _fps.Ewma;
    }

    public DefaultWindowRenderer(IWindowHost host, WindowHandle* window, string title) : base(host, window, title)
    {
        _fpsGetter = () => _fps.Ewma;
    }
    public DefaultWindowRenderer(IWindowHost host, nint window, string title) : base(host, (WindowHandle*)window, title)
    {
        _fpsGetter = () => _fps.Ewma;
    }

    protected override void RunLoop()
    {
        var gl = GL!;
        // Good default: enable sRGB when drawing into sRGB targets
        gl.Enable(EnableCap.FramebufferSrgb);

        // main loop
        var warnThresholdMs = PublicWindow!.LongActionWarnMs;

        var frameTimer = Stopwatch.StartNew();
        double targetMs = (PublicWindow!.VSync || PublicWindow!.TargetFPS <= 0)
            ? 0.0
            : 1000.0 / PublicWindow!.TargetFPS;

        Glfw.SwapInterval(PublicWindow!.VSync ? 1 : 0);
        while (Running)
        {
            frameTimer.Restart();

            // Size & canvas
            Glfw.GetFramebufferSize(Window, out var fbW, out var fbH);
            if (fbW <= 0 || fbH <= 0)
            {
                Thread.Sleep(8);
                continue;
            }
            EnsureCanvas(gl, fbW, fbH);

            // 1) See if there is a sealed frame to execute
            var hadFrame = TryDequeueFrame(out long token, out var batch);

            if (hadFrame)
            {
                // Render into the persistent canvas
                BindCanvas(gl);
                foreach (var act in batch!)
                {
                    var sw = Stopwatch.StartNew();
                    act.Execute(gl, Glfw, Window, fbW, fbH);
                    sw.Stop();
                    if (warnThresholdMs > 0 && sw.ElapsedMilliseconds > warnThresholdMs)
                        Console.WriteLine($"[Render Watchdog] {act.GetType().Name} took {sw.ElapsedMilliseconds} ms");
                }
                Unbind(gl);

                // Present once
                PresentCanvas(gl, fbW, fbH);
                Glfw.SwapBuffers(Window);
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
                Glfw.SwapBuffers(Window);
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
        DestroyCanvas(GL!);
    }


    private void EnsureCanvas(GL gl, int w, int h)
    {
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

        // Initialize new canvas to window clear color
        gl.ClearColor(PublicWindow!.ClearColor.R, PublicWindow!.ClearColor.G,
                      PublicWindow!.ClearColor.B, PublicWindow!.ClearColor.A);
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

        // Present once after resize
        _presentDirty = true;

        // Unbind for hygiene
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        gl.BindTexture(TextureTarget.Texture2D, 0);
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
    }

    private void DestroyCanvas(GL gl)
    {
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
        // Blit canvas → default backbuffer
        gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _canvasFbo);
        gl.ReadBuffer(ReadBufferMode.ColorAttachment0);

        gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
        gl.DrawBuffer(DrawBufferMode.Back);

        gl.BlitFramebuffer(
            0, 0, _canvasW, _canvasH,
            0, 0, fbW, fbH,
            ClearBufferMask.ColorBufferBit,
            BlitFramebufferFilter.Nearest);

        // Unbind read/draw FBOs
        gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
        gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
    }

}
