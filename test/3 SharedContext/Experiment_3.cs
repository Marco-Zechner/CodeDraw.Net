// Experiment_3.cs
// Triple-buffered, mailbox-style sharing (no 'volatile' keyword).
// - SharedGlfwHost from Experiment_2 is reused as-is (UI thread + hidden share root).
// - Producer (A) writes into a 3-slot ring, fences, and PUBLISHES {index,fence,seq,generation}.
// - Consumer (B) polls the fence non-blocking (ClientWaitSync with 0 timeout).
//   If not ready, it reuses the last ready frame (no stutter).
// - A disables blending when blitting its own offscreen result to avoid trails.
//
// Requires:
//   - Silk.NET.GLFW
//   - Silk.NET.OpenGL
//   - Shader helpers: using MarcoZechner.CodeDrawDotNet.Helpers;

using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using MarcoZechner.Tests.Helpers;

namespace MarcoZechner.Tests.Test3;

public unsafe static class Experiment_3
{
    private const int SLOT_COUNT = 3;

    private struct Slot
    {
        public uint Tex;
        public uint Fbo;       // producer-only
        public nint Fence;     // GLsync; last write fence for this slot
        public int  W, H;
        public long Seq;       // debug/visibility
        public int  Generation;
    }

    // Atomically published payload from Producer -> Consumer
    private struct Publication
    {
        public int   Index;       // 0..2, -1 if none
        public nint  Fence;       // GLsync for Index
        public long  Seq;         // monotonic "freshness"
        public int   Generation;  // ring/version
    }

    // Shared state (accessed via Interlocked/Volatile.* only)
    private static readonly Slot[] _slots = new Slot[SLOT_COUNT];
    private static Publication _pub;            // written by producer, read by consumer
    private static long _seqCounter = 0;        // producer seq
    private static int _generation = 1;         // ring generation
    private static int _lastPublished = -1;     // producer bookkeeping

    public static void Run()
    {
        var host = SharedGlfwHost.Instance;
        host.Start();

        WindowHandle* winA = host.CreateWindow(800, 600, "A (producer, triple-buffer)");
        WindowHandle* winB = host.CreateWindow(800, 600, "B (consumer)");

        var tA = new Thread(() => RenderA_Producer(winA)) { IsBackground = true, Name = "Render-A" };
        var tB = new Thread(() => RenderB_Consumer(winB)) { IsBackground = true, Name = "Render-B" };
        tA.Start(); tB.Start();

        Console.WriteLine("Experiment_3 running. Close windows or press ENTER to stop.");
        Console.ReadLine();

        host.EnqueueUI(() =>
        {
            if (!host.Glfw.WindowShouldClose(winA)) host.Glfw.SetWindowShouldClose(winA, true);
            if (!host.Glfw.WindowShouldClose(winB)) host.Glfw.SetWindowShouldClose(winB, true);
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
        glfw.SetErrorCallback((error, description) =>
        {
            Console.WriteLine($"GLFW Error {error}: {description}");
        });

        glfw.MakeContextCurrent(win);
        glfw.SwapInterval(0);
        var gl = GL.GetApi(glfw.GetProcAddress);

        var (vao, vbo, ebo) = GLShader.CreateFullScreenQuad(gl);
        uint progCircle = GLShader.CreateProgram(gl, GLShader.CircleShader.VS, GLShader.CircleShader.FS);
        uint progBlit   = GLShader.CreateProgram(gl, GLShader.LayerShader.VS, GLShader.LayerShader.FS);
        int  uTex       = gl.GetUniformLocation(progBlit, "uTex");

        int locTime   = gl.GetUniformLocation(progCircle, "uTime");
        int locPeriod = gl.GetUniformLocation(progCircle, "uPeriod");
        int locRadius = gl.GetUniformLocation(progCircle, "uRadius");
        int locColor  = gl.GetUniformLocation(progCircle, "uColor");
        int locRes    = gl.GetUniformLocation(progCircle, "uResolution");
        int locPathR  = gl.GetUniformLocation(progCircle, "uPathRadius");

        glfw.GetFramebufferSize(win, out int fbW, out int fbH);
        int myGen = Interlocked.Increment(ref _generation);
        CreateRing(gl, fbW, fbH, myGen);

        gl.Enable(GLEnum.Blend);

        var t0 = DateTime.UtcNow;

        while (!glfw.WindowShouldClose(win))
        {
            glfw.GetFramebufferSize(win, out int w, out int h);
            if (w == 0 || h == 0) { glfw.SwapBuffers(win); Thread.Sleep(12); continue; }

            // Recreate ring on resize
            if (w != fbW || h != fbH)
            {
                fbW = w; fbH = h;
                myGen = Interlocked.Increment(ref _generation);
                CreateRing(gl, fbW, fbH, myGen);
            }

            int wi = AcquireWriteSlot();

            // Draw circle into offscreen slot (transparent)
            gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);

            gl.BindFramebuffer(FramebufferTarget.Framebuffer, _slots[wi].Fbo);
            gl.Viewport(0, 0, (uint)fbW, (uint)fbH);
            gl.ClearColor(0f, 0f, 0f, 0f);
            gl.Clear((uint)ClearBufferMask.ColorBufferBit);

            gl.UseProgram(progCircle);
            gl.BindVertexArray(vao);

            float t = (float)(DateTime.UtcNow - t0).TotalSeconds;
            gl.Uniform1(locTime, t);
            gl.Uniform1(locPeriod, 9.5f);
            gl.Uniform1(locRadius, 36.0f);
            gl.Uniform4(locColor, 0.2f, 1.0f, 0.6f, 1f);
            gl.Uniform2(locRes, (float)fbW, (float)fbH);
            gl.Uniform1(locPathR, (float)(fbH / 2 - 40f));
            gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, null);

            // Insert fence and publish (mailbox)
            gl.BlendFunc(GLEnum.Zero, GLEnum.OneMinusSrcAlpha);

            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            // If this slot still had a very old fence, retire it (we’re overwriting the slot)
            if (_slots[wi].Fence != 0) { gl.DeleteSync(_slots[wi].Fence); _slots[wi].Fence = 0; }

            nint fence = gl.FenceSync(SyncCondition.SyncGpuCommandsComplete, SyncBehaviorFlags.None);
            gl.Flush();
            _slots[wi].Fence = fence;

            var seq = Interlocked.Increment(ref _seqCounter);
            Volatile.Write(ref _slots[wi].Seq, seq);

            // Publish in order: Index, Fence, Generation, Seq (Seq last = freshness)
            Volatile.Write(ref _pub.Index, wi);
            Volatile.Write(ref _pub.Fence, fence);
            Volatile.Write(ref _pub.Generation, myGen);
            Volatile.Write(ref _pub.Seq, seq);
            _lastPublished = wi;

            // On-screen for A
            gl.Viewport(0, 0, (uint)fbW, (uint)fbH);
            gl.ClearColor(0.08f, 0.10f, 0.13f, 1f);   // visible background
            gl.Clear((uint)ClearBufferMask.ColorBufferBit);
            gl.UseProgram(progBlit);
            gl.BindVertexArray(vao);
            gl.ActiveTexture(TextureUnit.Texture0);
            gl.BindTexture(TextureTarget.Texture2D, _slots[wi].Tex);
            if (uTex >= 0) gl.Uniform1(uTex, 0);
            gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, null);

            glfw.SwapBuffers(win);
            Thread.Sleep(16); // ~60 FPS
        }

        // Cleanup ring
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            if (_slots[i].Fence != 0) { gl.DeleteSync(_slots[i].Fence); _slots[i].Fence = 0; }
            if (_slots[i].Fbo != 0) gl.DeleteFramebuffer(_slots[i].Fbo);
            if (_slots[i].Tex != 0) gl.DeleteTexture(_slots[i].Tex);
            _slots[i] = default;
        }

        gl.DeleteProgram(progCircle);
        gl.DeleteProgram(progBlit);
        gl.DeleteVertexArray(vao);
        gl.DeleteBuffer(vbo);
        gl.DeleteBuffer(ebo);

        SharedGlfwHost.Instance.DestroyWindow(win);
        glfw.MakeContextCurrent(null);
    }

    // Choose the next slot cyclically (mailbox-style)
    private static int AcquireWriteSlot()
    {
        int last = _lastPublished >= 0 ? _lastPublished : 0;
        return (last + 1) % SLOT_COUNT;
    }

    private static void CreateRing(GL gl, int w, int h, int newGen)
    {
        // Delete previous
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            if (_slots[i].Fence != 0) { gl.DeleteSync(_slots[i].Fence); _slots[i].Fence = 0; }
            if (_slots[i].Fbo != 0) gl.DeleteFramebuffer(_slots[i].Fbo);
            if (_slots[i].Tex != 0) gl.DeleteTexture(_slots[i].Tex);
            _slots[i] = default;
        }

        // Create new
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            gl.CreateTextures(TextureTarget.Texture2D, 1, out _slots[i].Tex);
            gl.TextureStorage2D(_slots[i].Tex, 1, SizedInternalFormat.Rgba8, (uint)w, (uint)h);
            gl.TextureParameter(_slots[i].Tex, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            gl.TextureParameter(_slots[i].Tex, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            gl.TextureParameter(_slots[i].Tex, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TextureParameter(_slots[i].Tex, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

            gl.CreateFramebuffers(1, out _slots[i].Fbo);
            gl.NamedFramebufferTexture(_slots[i].Fbo, FramebufferAttachment.ColorAttachment0, _slots[i].Tex, 0);

            _slots[i].W = w; _slots[i].H = h; _slots[i].Generation = newGen;
        }

        // Reset publication
        Volatile.Write(ref _pub.Index, -1);
        Volatile.Write(ref _pub.Fence, 0);
        Volatile.Write(ref _pub.Generation, newGen);
        Volatile.Write(ref _pub.Seq, 0);

        _lastPublished = -1;
    }

    // -------------------- Consumer (Window B) --------------------
    private static void RenderB_Consumer(WindowHandle* win)
    {
        var host = SharedGlfwHost.Instance;
        var glfw = host.Glfw;

        glfw.MakeContextCurrent(win);
        glfw.SwapInterval(0);
        var gl = GL.GetApi(glfw.GetProcAddress);

        var (vao, vbo, ebo) = GLShader.CreateFullScreenQuad(gl);
        uint progBlit = GLShader.CreateProgram(gl, GLShader.LayerShader.VS, GLShader.LayerShader.FS);
        int  uTex     = gl.GetUniformLocation(progBlit, "uTex");

        // B needs blending (producer offscreen is transparent)
        gl.Enable(GLEnum.Blend);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);

        var t0 = DateTime.UtcNow;
        int currentGen = Volatile.Read(ref _pub.Generation);
        int lastIdx = -1;

        while (!glfw.WindowShouldClose(win))
        {
            glfw.GetFramebufferSize(win, out int fbW, out int fbH);
            if (fbW == 0 || fbH == 0) { glfw.SwapBuffers(win); Thread.Sleep(12); continue; }

            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            gl.Viewport(0, 0, (uint)fbW, (uint)fbH);

            // pretty background so compositing is obvious
            float t = (float)(DateTime.UtcNow - t0).TotalSeconds;
            gl.ClearColor(0.11f + 0.05f * MathF.Sin(t * 0.7f), 0.09f, 0.07f, 1f);
            gl.Clear((uint)ClearBufferMask.ColorBufferBit);

            // Snapshot publication
            var p = new Publication
            {
                Index = Volatile.Read(ref _pub.Index),
                Fence = Volatile.Read(ref _pub.Fence),
                Generation = Volatile.Read(ref _pub.Generation),
                Seq = Volatile.Read(ref _pub.Seq)
            };

            // If generation changed, accept it and continue; producer will start publishing new frames
            if (p.Generation != 0 && p.Generation != currentGen)
                currentGen = p.Generation;

            int drawIdx = lastIdx;

            if (p.Index >= 0 && p.Generation == currentGen)
            {
                nint f = p.Fence;
                if (f != 0)
                {
                    // Non-blocking poll; reuse last good frame if not ready
                    var s = gl.ClientWaitSync(f, SyncObjectMask.Bit, 0);
                    if (s == GLEnum.AlreadySignaled || s == GLEnum.ConditionSatisfied)
                    {
                        gl.DeleteSync(f);
                        Volatile.Write(ref _pub.Fence, 0);
                        drawIdx = p.Index;
                        lastIdx = drawIdx;
                    }
                    else
                    {
                        // keep lastIdx (no stall)
                        if (lastIdx < 0) drawIdx = p.Index; // first frame fallback
                    }
                }
                else
                {
                    // fence already cleared; trust index
                    drawIdx = p.Index;
                    lastIdx = drawIdx;
                }
            }

            if (drawIdx >= 0)
            {
                gl.UseProgram(progBlit);
                gl.BindVertexArray(vao);
                gl.ActiveTexture(TextureUnit.Texture0);
                gl.BindTexture(TextureTarget.Texture2D, _slots[drawIdx].Tex);
                if (uTex >= 0) gl.Uniform1(uTex, 0);
                gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, null);
            }

            glfw.SwapBuffers(win);
            Thread.Sleep(12); // slightly tighter cadence helps perceived smoothness
        }

        gl.DeleteProgram(progBlit);
        gl.DeleteVertexArray(vao);
        gl.DeleteBuffer(vbo);
        gl.DeleteBuffer(ebo);

        SharedGlfwHost.Instance.DestroyWindow(win);
        glfw.MakeContextCurrent(null);
    }
}
