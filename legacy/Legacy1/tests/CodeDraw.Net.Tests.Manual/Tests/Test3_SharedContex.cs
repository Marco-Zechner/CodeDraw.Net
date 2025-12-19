using System.Diagnostics;
using System.Runtime.InteropServices;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Helpers;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual;

public class Test3SharedContext
{
    private readonly CancellationTokenSource _cts = new();

    // private static async Task SetupSharedLayer()
    // {
    //     var mgr = SharedGlManager.Instance;
    // }

    public void Run()
    {
        // SetupSharedLayer().GetAwaiter().GetResult();

        var tA = new Thread(() => RunWindow("A (shared texture)", 800, 600, 0)) { IsBackground = true };
        var tB = new Thread(() => RunWindow("B (shared texture)", 800, 600, 1)) { IsBackground = true };

        tA.Start();
        tB.Start();

        // wait until both windows close
        tA.Join();
        tB.Join();

        // stop updater and exit
        _cts.Cancel();
    }

    private static volatile uint _gSharedTex = 0;
    private static volatile uint _gSharedTex2 = 0;
    private static readonly ManualResetEventSlim _gSharedTexReady = new(false);
    private static readonly ManualResetEventSlim _gSharedTexReady2 = new(false);

    public static unsafe void RunWindow(string title, int w, int h, int windowIndex)
    {
        var mgr = SharedGlManager.Instance;
        var share = mgr.Acquire();
        var glfw = mgr.Glfw;

        WindowHandle* win = null;

        lock (mgr.ShareGroupLock)
        {
            mgr.ApplyWindowHints();
            win = glfw.CreateWindow(w, h, title, null, share);
        }

        if (win == null) throw new Exception("CreateWindow failed");

        glfw.MakeContextCurrent(win);
        var gl = GL.GetApi(glfw.GetProcAddress);

        try
        {
            var ver = gl.GetStringS(GLEnum.Version);
            var ven = gl.GetStringS(GLEnum.Vendor);
            var ren = gl.GetStringS(GLEnum.Renderer);
            Logger.LogLine($"[{title}] context: {ver} | {ven} | {ren}");
        }
        catch { /* ignore */ }

        gl.Enable(GLEnum.DebugOutput);
        gl.Enable(GLEnum.DebugOutputSynchronous);
        unsafe {
            gl.DebugMessageCallback((source, type, id, severity, length, message, userparam) => {
                string msg = Marshal.PtrToStringAnsi(message, length);
                Logger.LogLine($"[DebugMessageCallback] source: {source}, type: {type}, id: {id}, severity {severity}, length {length}, userParam {userparam}\n{msg}");
            }, (void*) 0);
        }

        uint prog = GlShader.CreateProgram(gl, GlShader.CircleShader.VS, GlShader.CircleShader.FS);
        var (vao, vbo, ebo) = GlShader.CreateFullScreenQuad(gl);

        uint progLayer = GlShader.CreateProgram(gl, GlShader.LayerShader.VS, GlShader.LayerShader.FS);
        int locUTex = gl.GetUniformLocation(progLayer, "uTex");

        int locTime   = gl.GetUniformLocation(prog, "uTime");
        int locPeriod = gl.GetUniformLocation(prog, "uPeriod");
        int locRadius = gl.GetUniformLocation(prog, "uRadius");
        int locColor  = gl.GetUniformLocation(prog, "uColor");
        int locRes    = gl.GetUniformLocation(prog, "uResolution");
        int locPathRadius = gl.GetUniformLocation(prog, "uPathRadius");

        uint offFbo = (uint)0;
        uint offTex = (uint)0;
        uint offFbo2 = (uint)0;
        uint offTex2 = (uint)0;
        //layer tests
        if (windowIndex == 0)
        {
            glfw.GetFramebufferSize(win, out int fbW, out int fbH);

            gl.CreateTextures(TextureTarget.Texture2D, 1, out offTex);
            gl.TextureStorage2D(offTex, 1, SizedInternalFormat.Rgba8, (uint)fbW, (uint)fbH);
            gl.TextureParameter(offTex, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            gl.TextureParameter(offTex, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            gl.TextureParameter(offTex, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TextureParameter(offTex, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

            _gSharedTex = offTex;
            _gSharedTexReady.Set();

            gl.CreateFramebuffers(1, out offFbo);
            gl.NamedFramebufferTexture(offFbo, FramebufferAttachment.ColorAttachment0, offTex, 0);
        } else
        {
            glfw.GetFramebufferSize(win, out int fbW, out int fbH);

            gl.CreateTextures(TextureTarget.Texture2D, 1, out offTex2);
            gl.TextureStorage2D(offTex2, 1, SizedInternalFormat.Rgba8, (uint)fbW, (uint)fbH);
            gl.TextureParameter(offTex2, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            gl.TextureParameter(offTex2, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            gl.TextureParameter(offTex2, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TextureParameter(offTex2, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

            _gSharedTex2 = offTex2;
            _gSharedTexReady2.Set();

            gl.CreateFramebuffers(1, out offFbo2);
            gl.NamedFramebufferTexture(offFbo2, FramebufferAttachment.ColorAttachment0, offTex2, 0);   
        }

        gl.Enable(GLEnum.Blend);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);

        Stopwatch stopwatch = new();

        while (!glfw.WindowShouldClose(win))
        {
            var time = stopwatch.ElapsedMilliseconds;

            if (time < 20)
            {
                Thread.Sleep((int)(20 - time));
            }

            stopwatch.Restart();
            glfw.PollEvents();
            Logger.LogLine($"window: {windowIndex}: poll events");

            if (windowIndex == 1)
            {
                _gSharedTexReady.Wait();
                offTex = _gSharedTex;
            } else
            {
                _gSharedTexReady2.Wait();
                offTex2 = _gSharedTex2;
            }

            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);        
            glfw.GetFramebufferSize(win, out var fbW, out var fbH);
            if (fbW == 0 || fbH == 0)
            {
                // keep event loop alive, but skip GL
                glfw.SwapBuffers(win);
                stopwatch.Stop();
                Console.WriteLine("size 0! at 1");
                continue;
            }
            gl.Viewport(0, 0, (uint)fbW, (uint)fbH);
            gl.ClearColor(0f, 0f, 0.2f, 1f);
            gl.Clear((uint)ClearBufferMask.ColorBufferBit);

            gl.UseProgram(prog);
            gl.BindVertexArray(vao);

            float timeSec = (float)DateTime.Now.TimeOfDay.TotalSeconds;
            float periodSec;
            float radiusPx;
            if (windowIndex == 0)
            {
                periodSec = 10.0f;            // circle crosses in 2 seconds
                radiusPx = 40.0f;           // circle radius in pixels
                gl.Uniform4(locColor, 1.0f, 0.6f, 0.2f, 1.0f);
            }
            else
            {
                periodSec = 12.0f;            // circle crosses in 2 seconds
                radiusPx = 25.0f;           // circle radius in pixels
                gl.Uniform4(locColor, 0.6f, 0.2f, 1.0f, 0.8f);
            }
            gl.Uniform1(locTime, timeSec);
            gl.Uniform1(locPeriod, periodSec);
            gl.Uniform1(locRadius, radiusPx);
            gl.Uniform2(locRes, (float)fbW, (float)fbH);
            gl.Uniform1(locPathRadius, (float)(fbH / 2 - radiusPx - 10)); // path radius

            gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, null);
            Logger.LogLine($"window: {windowIndex}: draw basic circle");


            if (windowIndex == 0)
            {
                // shared buffer
                gl.BindFramebuffer(FramebufferTarget.Framebuffer, offFbo);
                glfw.GetFramebufferSize(win, out fbW, out fbH);
                if (fbW == 0 || fbH == 0)
                {
                    // keep event loop alive, but skip GL
                    glfw.SwapBuffers(win);
                    stopwatch.Stop();
                    Console.WriteLine("size 0! at 2");
                    continue;
                }
                gl.Viewport(0, 0, (uint)fbW, (uint)fbH);
                gl.ClearColor(0f, 0f, 0f, 0f);
                gl.Clear((uint)ClearBufferMask.ColorBufferBit);

                gl.UseProgram(prog);
                gl.BindVertexArray(vao);


                // uniforms
                periodSec = 18.0f;            // circle crosses in 2 seconds
                radiusPx = 25.0f;           // circle radius in pixels
                gl.Uniform1(locTime, timeSec);
                gl.Uniform1(locPeriod, periodSec);
                gl.Uniform1(locRadius, radiusPx);
                gl.Uniform4(locColor, 0.2f, 1.0f, 0.6f, 0.5f);
                gl.Uniform2(locRes, (float)fbW, (float)fbH);
                gl.Uniform1(locPathRadius, (float)(fbH / 2 - radiusPx - 10)); // path radius

                gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, null);
                Logger.LogLine($"window: {windowIndex}: draw buffer circle");
            }

            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            if (windowIndex == 0)
                gl.Flush();
            glfw.GetFramebufferSize(win, out fbW, out fbH);
            if (fbW == 0 || fbH == 0)
            {
                // keep event loop alive, but skip GL
                glfw.SwapBuffers(win);
                stopwatch.Stop();
                Console.WriteLine("size 0! at 3");
                continue;
            }
            gl.Viewport(0, 0, (uint)fbW, (uint)fbH);

            gl.UseProgram(progLayer);
            gl.BindVertexArray(vao);

            gl.ActiveTexture(TextureUnit.Texture0);
            gl.BindTexture(TextureTarget.Texture2D, offTex);
            if (locUTex >= 0) gl.Uniform1(locUTex, 0);

            gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, null);
            Logger.LogLine($"window: {windowIndex}: draw texture");

            glfw.SwapBuffers(win);
            Logger.LogLine($"window: {windowIndex}: swap buffers");
            stopwatch.Stop();
        }

        // cleanup
        gl.DeleteVertexArray(vao);
        gl.DeleteBuffer(vbo);
        gl.DeleteBuffer(ebo);
        gl.DeleteProgram(prog);
        gl.DeleteProgram(progLayer);

        glfw.DestroyWindow(win);
        mgr.Release();
    }
}