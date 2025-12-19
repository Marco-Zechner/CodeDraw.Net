using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Helpers;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Experiments;

public unsafe static class Experiment2
{
    // Cross-context state (producer -> consumer)
    private static volatile int  _publishedIndex = -1; // 0 or 1
    private static volatile nint _publishedFence = 0;  // GLsync as nint

    // Shared texture names (published once by producer)
    private static class SharedNames
    {
        public static volatile uint Tex0;
        public static volatile uint Tex1;
        public static void Publish(uint t0, uint t1) { Tex0 = t0; Tex1 = t1; }
    }

    public static void Run()
    {
        // 1) Start host (UI) thread + hidden share root
        var host = SharedGlfwHost.Instance;
        host.Start();

        // 2) Create windows on the UI thread (sharing with root)
        WindowHandle* winA = host.CreateWindow(800, 600, "A (producer)");
        WindowHandle* winB = host.CreateWindow(800, 600, "B (consumer)");

        // 3) Start render threads (one per window)
        var tA = new Thread(() => RenderA_Producer(winA)) { IsBackground = true, Name = "Render-A" };
        var tB = new Thread(() => RenderB_Consumer(winB)) { IsBackground = true, Name = "Render-B" };
        tA.Start(); tB.Start();

        Console.WriteLine("Experiment_2 running. Close windows or press ENTER to stop.");
        Console.ReadLine();

        // 4) On ENTER: ask both windows to close (if still alive) and stop host
        host.EnqueueUi(() =>
        {
            if (winA != null && !host.Glfw.WindowShouldClose(winA)) host.Glfw.SetWindowShouldClose(winA, true);
            if (winB != null && !host.Glfw.WindowShouldClose(winB)) host.Glfw.SetWindowShouldClose(winB, true);
        });

        tA.Join();
        tB.Join();

        host.Stop();
    }

    // -------------------- Producer (Window A) --------------------
    private static void RenderA_Producer(WindowHandle* win)
    {
        var host = SharedGlfwHost.Instance;
        var glfw = host.Glfw;

        // Bind context on this thread
        glfw.MakeContextCurrent(win);
        glfw.SwapInterval(0);

        var gl = GL.GetApi(glfw.GetProcAddress);

        var (vao, vbo, ebo) = GlShader.CreateFullScreenQuad(gl);
        uint progCircle = GlShader.CreateProgram(gl, GlShader.CircleShader.VS, GlShader.CircleShader.FS);
        uint progBlit   = GlShader.CreateProgram(gl, GlShader.LayerShader.VS, GlShader.LayerShader.FS);
        int  uTex       = gl.GetUniformLocation(progBlit, "uTex");

        // Uniform locations
        int locTime   = gl.GetUniformLocation(progCircle, "uTime");
        int locPeriod = gl.GetUniformLocation(progCircle, "uPeriod");
        int locRadius = gl.GetUniformLocation(progCircle, "uRadius");
        int locColor  = gl.GetUniformLocation(progCircle, "uColor");
        int locRes    = gl.GetUniformLocation(progCircle, "uResolution");
        int locPathR  = gl.GetUniformLocation(progCircle, "uPathRadius");

        glfw.GetFramebufferSize(win, out int fbW, out int fbH);

        // Create ping-pong textures & FBOs (shared with all)
        uint[] tex = new uint[2];
        uint[] fbo = new uint[2];
        for (int i = 0; i < 2; i++)
        {
            gl.CreateTextures(TextureTarget.Texture2D, 1, out tex[i]);
            gl.TextureStorage2D(tex[i], 1, SizedInternalFormat.Rgba8, (uint)fbW, (uint)fbH);
            gl.TextureParameter(tex[i], TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            gl.TextureParameter(tex[i], TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            gl.TextureParameter(tex[i], TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TextureParameter(tex[i], TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

            gl.CreateFramebuffers(1, out fbo[i]);
            gl.NamedFramebufferTexture(fbo[i], FramebufferAttachment.ColorAttachment0, tex[i], 0);
        }

        // Publish texture names once
        SharedNames.Publish(tex[0], tex[1]);

        gl.Enable(GLEnum.Blend);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);

        int writeIdx = 0;
        var t0 = DateTime.UtcNow;

        while (!glfw.WindowShouldClose(win))
        {
            glfw.GetFramebufferSize(win, out fbW, out fbH);
            if (fbW == 0 || fbH == 0) { glfw.SwapBuffers(win); Thread.Sleep(16); continue; }

            // background (prove A runs)
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            gl.Viewport(0, 0, (uint)fbW, (uint)fbH);
            float t = (float)(DateTime.UtcNow - t0).TotalSeconds;
            gl.ClearColor(0.08f, 0.10f, 0.13f, 1f);
            gl.Clear((uint)ClearBufferMask.ColorBufferBit);

            // draw moving circle into offscreen (transparent)
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo[writeIdx]);
            gl.Viewport(0, 0, (uint)fbW, (uint)fbH);
            gl.ClearColor(0f, 0f, 0f, 0f);
            gl.Clear((uint)ClearBufferMask.ColorBufferBit);

            gl.UseProgram(progCircle);
            gl.BindVertexArray(vao);
            gl.Uniform1(locTime, t);
            gl.Uniform1(locPeriod, 9.5f);
            gl.Uniform1(locRadius, 36.0f);
            gl.Uniform4(locColor, 0.2f, 1.0f, 0.6f, 0.75f);
            gl.Uniform2(locRes, (float)fbW, (float)fbH);
            gl.Uniform1(locPathR, (float)(fbH / 2 - 40f));
            gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, null);

            // fence + publish index
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            nint fence = gl.FenceSync(SyncCondition.SyncGpuCommandsComplete, SyncBehaviorFlags.None);
            gl.Flush(); // make fence visible across contexts

            var oldFence = Interlocked.Exchange(ref _publishedFence, fence);
            if (oldFence != 0) gl.DeleteSync(oldFence);
#pragma warning disable CS0420 // A reference to a volatile field will not be treated as volatile
            Volatile.Write(ref _publishedIndex, writeIdx);
#pragma warning restore CS0420 // A reference to a volatile field will not be treated as volatile

            // blit to A (so A also shows it)
            gl.UseProgram(progBlit);
            gl.BindVertexArray(vao);
            gl.ActiveTexture(TextureUnit.Texture0);
            gl.BindTexture(TextureTarget.Texture2D, tex[writeIdx]);
            if (uTex >= 0) gl.Uniform1(uTex, 0);
            gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, null);

            // ping-pong
            writeIdx ^= 1;

            glfw.SwapBuffers(win);
            Thread.Sleep(16); // ~60fps
        }

        // Cleanup
        for (int i = 0; i < 2; i++) { gl.DeleteFramebuffer(fbo[i]); gl.DeleteTexture(tex[i]); }
        var leftover = Interlocked.Exchange(ref _publishedFence, 0);
        if (leftover != 0) gl.DeleteSync(leftover);
        gl.DeleteProgram(progCircle);
        gl.DeleteProgram(progBlit);
        gl.DeleteVertexArray(vao);
        gl.DeleteBuffer(vbo);
        gl.DeleteBuffer(ebo);

        // Destroy this window on UI thread; keep host alive for others
        host.DestroyWindow(win);
        glfw.MakeContextCurrent(null);
    }

    // -------------------- Consumer (Window B) --------------------
    private static void RenderB_Consumer(WindowHandle* win)
    {
        var host = SharedGlfwHost.Instance;
        var glfw = host.Glfw;

        glfw.MakeContextCurrent(win);
        glfw.SwapInterval(0);

        var gl = GL.GetApi(glfw.GetProcAddress);

        var (vao, vbo, ebo) = GlShader.CreateFullScreenQuad(gl);
        uint progBlit = GlShader.CreateProgram(gl, GlShader.LayerShader.VS, GlShader.LayerShader.FS);
        int  uTex     = gl.GetUniformLocation(progBlit, "uTex");

        // Blend for transparency
        gl.Enable(GLEnum.Blend);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);

        // Wait until producer published texture names
        while (!glfw.WindowShouldClose(win) && (SharedNames.Tex0 == 0 || SharedNames.Tex1 == 0))
            Thread.Sleep(1);

        var t0 = DateTime.UtcNow;

        while (!glfw.WindowShouldClose(win))
        {
            glfw.GetFramebufferSize(win, out int fbW, out int fbH);
            if (fbW == 0 || fbH == 0) { glfw.SwapBuffers(win); Thread.Sleep(16); continue; }

            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            gl.Viewport(0, 0, (uint)fbW, (uint)fbH);
            float t = (float)(DateTime.UtcNow - t0).TotalSeconds;
            gl.ClearColor(0.12f + 0.05f * MathF.Sin(t * 0.5f), 0.09f, 0.07f, 1f);
            gl.Clear((uint)ClearBufferMask.ColorBufferBit);

            // Wait latest fence (if any)
            var fence = Interlocked.Exchange(ref _publishedFence, 0);
            if (fence != 0)
            {
                gl.WaitSync(fence, SyncBehaviorFlags.None, ulong.MaxValue);
                gl.DeleteSync(fence);
            }

#pragma warning disable CS0420 // A reference to a volatile field will not be treated as volatile
            int idx = Volatile.Read(ref _publishedIndex);
#pragma warning restore CS0420 // A reference to a volatile field will not be treated as volatile
            if (idx >= 0)
            {
                uint tex = (idx == 0) ? SharedNames.Tex0 : SharedNames.Tex1;
                gl.UseProgram(progBlit);
                gl.BindVertexArray(vao);
                gl.ActiveTexture(TextureUnit.Texture0);
                gl.BindTexture(TextureTarget.Texture2D, tex);
                if (uTex >= 0) gl.Uniform1(uTex, 0);
                gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, null);
            }

            glfw.SwapBuffers(win);
            Thread.Sleep(16);
        }

        gl.DeleteProgram(progBlit);
        gl.DeleteVertexArray(vao);
        gl.DeleteBuffer(vbo);
        gl.DeleteBuffer(ebo);

        host.DestroyWindow(win);
        glfw.MakeContextCurrent(null);
    }
}