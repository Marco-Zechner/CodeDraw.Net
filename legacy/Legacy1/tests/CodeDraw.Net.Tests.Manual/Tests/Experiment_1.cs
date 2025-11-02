using MarcoZechner.CodeDrawDotNet.Tests.Manual.Helpers;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Experiments;

public unsafe class Experiment_1
{
    private static Glfw _glfw = null!;
    private static WindowHandle* _winA;
    private static WindowHandle* _winB;
    private static volatile bool _running = true;

    // Cross-context publication (producer -> consumer)
    private static volatile int  _publishedIndex = -1; // 0 or 1
    private static volatile nint _publishedFence = 0;  // GLsync as nint

    public static void Run()
    {
        _glfw = Glfw.GetApi();
        if (!_glfw.Init()) throw new Exception("GLFW init failed");

        // GL 3.3 core
        _glfw.WindowHint(WindowHintInt.ContextVersionMajor, 3);
        _glfw.WindowHint(WindowHintInt.ContextVersionMinor, 3);
        _glfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core);

        // Create windows (A = share root, B shares with A)
        _winA = _glfw.CreateWindow(640, 480, "A (producer)", null, null);
        if (_winA == null) throw new Exception("Failed to create window A");

        _winB = _glfw.CreateWindow(640, 480, "B (consumer)", null, _winA);
        if (_winB == null) throw new Exception("Failed to create window B");

        // Bind each context once on creator thread (stability on Windows)
        _glfw.MakeContextCurrent(_winA); _glfw.MakeContextCurrent(null);
        _glfw.MakeContextCurrent(_winB); _glfw.MakeContextCurrent(null);

        // Start render threads
        var tA = new Thread(() => RenderA_Producer(_winA)) { IsBackground = true, Name = "Render-A" };
        var tB = new Thread(() => RenderB_Consumer(_winB)) { IsBackground = true, Name = "Render-B" };
        tA.Start(); tB.Start();

        // Event loop: keep running until BOTH are closed
        while (!(_glfw.WindowShouldClose(_winA) && _glfw.WindowShouldClose(_winB)))
        {
            _glfw.PollEvents();
            Thread.Sleep(1);
        }

        _running = false;
        tA.Join();
        tB.Join();

        _glfw.DestroyWindow(_winB);
        _glfw.DestroyWindow(_winA);
        _glfw.Terminate();
    }

    // -------------------- Producer (Window A) --------------------
    private static void RenderA_Producer(WindowHandle* win)
    {
        var gl = GL.GetApi(_glfw.GetProcAddress);

        _glfw.MakeContextCurrent(win);
        _glfw.SwapInterval(0); // independent swaps for testing

        // Geo & shaders
        var (vao, vbo, ebo) = GLShader.CreateFullScreenQuad(gl);
        uint progCircle = GLShader.CreateProgram(gl, GLShader.CircleShader.VS, GLShader.CircleShader.FS);
        uint progBlit   = GLShader.CreateProgram(gl, GLShader.LayerShader.VS, GLShader.LayerShader.FS);
        int  uTex       = gl.GetUniformLocation(progBlit, "uTex");

        // Uniform locations
        int locTime   = gl.GetUniformLocation(progCircle, "uTime");
        int locPeriod = gl.GetUniformLocation(progCircle, "uPeriod");
        int locRadius = gl.GetUniformLocation(progCircle, "uRadius");
        int locColor  = gl.GetUniformLocation(progCircle, "uColor");
        int locRes    = gl.GetUniformLocation(progCircle, "uResolution");
        int locPathR  = gl.GetUniformLocation(progCircle, "uPathRadius");

        _glfw.GetFramebufferSize(win, out int fbW, out int fbH);

        // Create shared ping-pong textures & FBOs
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

        // Publish the texture names once for the consumer
        SharedNames.Publish(tex[0], tex[1]);

        gl.Enable(GLEnum.Blend);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);

        int writeIdx = 0;
        var start = DateTime.UtcNow;

        while (_running && !_glfw.WindowShouldClose(win))
        {
            _glfw.GetFramebufferSize(win, out fbW, out fbH);
            if (fbW == 0 || fbH == 0) { _glfw.SwapBuffers(win); Thread.Sleep(16); continue; }

            // 1) Clear A's onscreen
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            gl.Viewport(0, 0, (uint)fbW, (uint)fbH);
            float t = (float)(DateTime.UtcNow - start).TotalSeconds;
            float sin = (float)MathF.Sin(t);
            float cos = (float)MathF.Cos(t);
            gl.ClearColor(sin, cos, 0.5f, 1f);
            gl.Clear((uint)ClearBufferMask.ColorBufferBit);

            // 2) Render circle into offscreen FBO (shared tex[writeIdx])
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo[writeIdx]);
            gl.Viewport(0, 0, (uint)fbW, (uint)fbH);
            gl.ClearColor(0f, 0f, 0f, 0f);
            gl.Clear((uint)ClearBufferMask.ColorBufferBit);

            gl.UseProgram(progCircle);
            gl.BindVertexArray(vao);

            // float t = (float)(DateTime.UtcNow - start).TotalSeconds;
            gl.Uniform1(locTime, t);
            gl.Uniform1(locPeriod, 9.5f);
            gl.Uniform1(locRadius, 36.0f);
            gl.Uniform4(locColor, 0.2f, 1.0f, 0.6f, 0.75f);
            gl.Uniform2(locRes, (float)fbW, (float)fbH);
            gl.Uniform1(locPathR, (float)(fbH / 2 - 40f));

            gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, null);

            // 3) Publish fence + index (correct Silk.NET overloads)
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            nint fence = gl.FenceSync(SyncCondition.SyncGpuCommandsComplete, SyncBehaviorFlags.None);
            gl.Flush(); // ensure visibility to other contexts

            var oldFence = Interlocked.Exchange(ref _publishedFence, fence);
            if (oldFence != 0) gl.DeleteSync(oldFence);

#pragma warning disable CS0420 // A reference to a volatile field will not be treated as volatile
            Volatile.Write(ref _publishedIndex, writeIdx);
#pragma warning restore CS0420 // A reference to a volatile field will not be treated as volatile

            // 4) Also show the just-written texture in A (blit pass)
            int readIdx = writeIdx;
            gl.UseProgram(progBlit);
            gl.BindVertexArray(vao);
            gl.ActiveTexture(TextureUnit.Texture0);
            gl.BindTexture(TextureTarget.Texture2D, tex[readIdx]);
            if (uTex >= 0) gl.Uniform1(uTex, 0);
            gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, null);

            // 5) Flip ping-pong index
            writeIdx ^= 1;

            _glfw.SwapBuffers(win);
            Thread.Sleep(16);
        }

        // Cleanup
        for (int i = 0; i < 2; i++)
        {
            gl.DeleteFramebuffer(fbo[i]);
            gl.DeleteTexture(tex[i]);
        }
        var leftoverFence = Interlocked.Exchange(ref _publishedFence, 0);
        if (leftoverFence != 0) gl.DeleteSync(leftoverFence);

        gl.DeleteProgram(progCircle);
        gl.DeleteProgram(progBlit);
        gl.DeleteVertexArray(vao);
        gl.DeleteBuffer(vbo);
        gl.DeleteBuffer(ebo);

        _glfw.MakeContextCurrent(null);
    }

    // -------------------- Consumer (Window B) --------------------
    private static void RenderB_Consumer(WindowHandle* win)
    {
        var gl = GL.GetApi(_glfw.GetProcAddress);

        _glfw.MakeContextCurrent(win);
        _glfw.SwapInterval(0);

        gl.Enable(GLEnum.Blend);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);

        var (vao, vbo, ebo) = GLShader.CreateFullScreenQuad(gl);
        uint progBlit = GLShader.CreateProgram(gl, GLShader.LayerShader.VS, GLShader.LayerShader.FS);
        int  uTex     = gl.GetUniformLocation(progBlit, "uTex");

        var start = DateTime.UtcNow;

        // Wait until producer published texture names
        while (_running && (SharedNames.Tex0 == 0 || SharedNames.Tex1 == 0))
            Thread.Sleep(1);

        while (_running && !_glfw.WindowShouldClose(win))
        {
            _glfw.GetFramebufferSize(win, out int fbW, out int fbH);
            if (fbW == 0 || fbH == 0) { _glfw.SwapBuffers(win); Thread.Sleep(16); continue; }

            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            gl.Viewport(0, 0, (uint)fbW, (uint)fbH);
            float t = (float)(DateTime.UtcNow - start).TotalSeconds;
            float sin = (float)MathF.Sin(t);
            float cos = (float)MathF.Cos(t);
            gl.ClearColor(0.5f, sin, cos, 1f);
            gl.Clear((uint)ClearBufferMask.ColorBufferBit);

            // Take the latest fence (if any), GPU-side wait, then delete it
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

            _glfw.SwapBuffers(win);
            Thread.Sleep(10);
        }

        gl.DeleteProgram(progBlit);
        gl.DeleteVertexArray(vao);
        gl.DeleteBuffer(vbo);
        gl.DeleteBuffer(ebo);

        _glfw.MakeContextCurrent(null);
    }

    // One-time handoff of the two shared texture names
    private static class SharedNames
    {
        public static volatile uint Tex0;
        public static volatile uint Tex1;

        public static void Publish(uint t0, uint t1)
        {
            Tex0 = t0; Tex1 = t1;
        }
    }
}
