using System.Diagnostics;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;

public sealed unsafe class CodeDrawWindow : IDisposable
{
    public sealed class WindowInput
    {
        // held state
        private readonly HashSet<Keys> _keysHeld = [];
        private readonly HashSet<MouseButton> _mouseHeld = [];

        // edge state (per update tick)
        private readonly HashSet<Keys> _keysDown = [];
        private readonly HashSet<Keys> _keysUp = [];
        private readonly HashSet<MouseButton> _mouseDown = [];
        private readonly HashSet<MouseButton> _mouseUp = [];

        // last modifiers seen per key/button (best-effort, because modifiers are "event context")
        private readonly Dictionary<Keys, KeyModifiers> _keyMods = [];
        private readonly Dictionary<MouseButton, KeyModifiers> _mouseMods = [];

        public double MouseX { get; private set; }
        public double MouseY { get; private set; }

        public double WheelDx { get; private set; }
        public double WheelDy { get; private set; }

        public bool GetKey(Keys k) => _keysHeld.Contains(k);
        public bool GetKeyDown(Keys k) => _keysDown.Contains(k);
        public bool GetKeyUp(Keys k) => _keysUp.Contains(k);

        public bool GetMouseButton(MouseButton b) => _mouseHeld.Contains(b);
        public bool GetMouseButtonDown(MouseButton b) => _mouseDown.Contains(b);
        public bool GetMouseButtonUp(MouseButton b) => _mouseUp.Contains(b);

        public KeyModifiers GetKeyMods(Keys k) => _keyMods.TryGetValue(k, out var m) ? m : 0;
        public KeyModifiers GetMouseMods(MouseButton b) => _mouseMods.TryGetValue(b, out var m) ? m : 0;


        internal void BeginUpdateFrame()
        {
            WheelDx = 0;
            WheelDy = 0;

            _keysDown.Clear();
            _keysUp.Clear();
            _mouseDown.Clear();
            _mouseUp.Clear();
        }

        internal void Apply(object evt)
        {
            switch (evt)
            {
                case SharedGlfwHost.MouseMoveEvent mm:
                    MouseX = mm.X;
                    MouseY = mm.Y;
                    break;

                case SharedGlfwHost.MouseWheelEvent mw:
                    WheelDx += mw.Dx;
                    WheelDy += mw.Dy;
                    break;

                case SharedGlfwHost.MouseButtonEvent mb:
                {
                    _mouseMods[mb.Button] = mb.Mods;

                    switch (mb.Action)
                    {
                        case InputAction.Press:
                            if (_mouseHeld.Add(mb.Button))
                                _mouseDown.Add(mb.Button);
                            else
                                _mouseDown.Add(mb.Button);
                            break;

                        case InputAction.Release:
                            if (_mouseHeld.Remove(mb.Button))
                                _mouseUp.Add(mb.Button);
                            else
                                _mouseUp.Add(mb.Button);
                            break;
                    }
                    break;
                }

                case SharedGlfwHost.KeyEvent ke:
                {
                    _keyMods[ke.Key] = ke.Mods;

                    switch (ke.Action)
                    {
                        case InputAction.Press:
                            if (_keysHeld.Add(ke.Key))
                                _keysDown.Add(ke.Key);
                            else
                                _keysDown.Add(ke.Key);
                            break;

                        case InputAction.Release:
                            if (_keysHeld.Remove(ke.Key))
                                _keysUp.Add(ke.Key);
                            else
                                _keysUp.Add(ke.Key);
                            break;

                        case InputAction.Repeat:
                            _keysHeld.Add(ke.Key);
                            break;
                    }
                    break;
                }
            }
        }
    }

    public readonly record struct UpdateContext(
        CodeDrawWindow Win,
        WindowInput Input,
        float DeltaSeconds,      // time since last update tick
        long Tick
    );

    private readonly SharedGlfwHost _host;
    private readonly WindowHandle* _win;

    private Thread? _presentThread;
    private Thread? _updateThread;

    private volatile bool _closing;
    private int _disposed;
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;
    private int _windowDestroyed; // 0 = not yet, 1 = done
    public bool ShouldClose => _closing || IsDisposed;

    private CodeDrawLayer? _layer;
    public CodeDrawLayer? Layer => _layer;

    public int WindowId { get; }
    private string _title;

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            _host.EnqueueUi(() => _host.Glfw.SetWindowTitle(_win, _title));
        }
    }

    private bool _borderlessFullscreen;
    public void SetBorderlessFullscreen(bool enabled)
    {
        _borderlessFullscreen = enabled;
        _host.SetBorderlessFullscreenSafe(_win, enabled);
    }

    public void SetBorderlessFullscreen(bool enabled, int monitorIndex)
    {
        _borderlessFullscreen = enabled;
        _host.SetBorderlessFullscreenSafe(_win, enabled, monitorIndex);
    }

    public bool BorderlessFullscreen
    {
        get => _borderlessFullscreen;
        set => SetBorderlessFullscreen(value);
    }

    public WindowInput Input { get; } = new();

    // knobs (ms as requested)
    public int UpdateDelayMs { get; set; } = 16;
    public int PresentWaitTimeoutMs { get; set; } = 16;

    private volatile bool _keepLastFrameUntilReady = true;

    // Single-subscriber hooks (properties overwrite previous)
    public Action<CodeDrawWindow>? OnStart { get; set; }
    public Action<UpdateContext>? OnUpdate { get; set; }
    public Action<CodeDrawWindow>? OnClose { get; set; }

    private bool _startFired;

    public void SetPresentedLayer(CodeDrawLayer? layer, bool keepLastFrameUntilReady = true)
    {
        _layer = layer;
        _keepLastFrameUntilReady = keepLastFrameUntilReady;
    }


    public CodeDrawWindow(SharedGlfwHost host, int w, int h, string title)
    {
        _host = host;
        _title = title;
        _win = host.CreateWindow(w, h, _title);
        WindowId = host.GetWindowId(_win);

        _layer = new CodeDrawLayer(host, w, h);

        _presentThread = new Thread(PresentLoop) { IsBackground = true, Name = $"Presenter:{_title}" };
        _updateThread  = new Thread(UpdateLoop)  { IsBackground = true, Name = $"Update:{_title}" };

        _presentThread.Start();
        _updateThread.Start();
    }

    private void DestroyWindowOnce()
    {
        if (Interlocked.Exchange(ref _windowDestroyed, 1) != 0) return;

        var win = _win;
        _host.DestroyWindow(win); // host invokes this safely on UI thread
    }

    public void Close()
    {
        if (_closing) return;
        _closing = true;

        var win = _win;
        _host.EnqueueUi(() =>
        {
            if (!_host.IsWindowAlive(win)) return;
            _host.Glfw.SetWindowShouldClose(win, true);
        });
    }

    public void WaitForClose()
    {
        var p = Interlocked.Exchange(ref _presentThread, null);
        var u = Interlocked.Exchange(ref _updateThread, null);

        if (p is { IsAlive: true }) p.Join();
        if (u is { IsAlive: true }) u.Join();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        Close();
        WaitForClose();

        DestroyWindowOnce();

        _layer?.Dispose();
        _layer = null;
    }

    private void HandleEvent(object evt)
    {
        if (evt is SharedGlfwHost.WindowCloseRequestedEvent cl && cl.WindowId == WindowId)
        {
            _closing = true;

            var win = _win;
            _host.EnqueueUi(() =>
            {
                if (!_host.IsWindowAlive(win)) return;
                _host.Glfw.SetWindowShouldClose(win, true);
            });
            return;
        }

        Input.Apply(evt);
    }

    private void UpdateLoop()
    {
        var sw = Stopwatch.StartNew();
        var lastTicks = sw.ElapsedTicks;
        long tick = 0;

        while (!ShouldClose)
        {
            var loopStartTicks = sw.ElapsedTicks;
            var deltaSec = (float)((loopStartTicks - lastTicks) / (double)Stopwatch.Frequency);
            lastTicks = loopStartTicks;

            Input.BeginUpdateFrame();
            _host.DrainWindowInput(WindowId, HandleEvent);

            if (!_startFired && OnStart != null)
            {
                _startFired = true;
                try { OnStart(this); }
                catch (Exception ex) { Console.WriteLine($"[OnStart error] {ex}"); }
            }

            var cb = OnUpdate;
            if (cb != null)
            {
                try
                {
                    cb(new UpdateContext(this, Input, deltaSec, tick));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[OnUpdate error] {ex}");
                }
            }

            tick++;

            var elapsedMs = (int)((sw.ElapsedTicks - loopStartTicks) * 1000.0 / Stopwatch.Frequency);
            var delay = UpdateDelayMs;
            var sleepMs = delay - elapsedMs;
            if (sleepMs > 0) Thread.Sleep(sleepMs);
            else Thread.Yield();
        }

        if (OnClose == null) return;
        try { OnClose(this); }
        catch (Exception ex) { Console.WriteLine($"[OnClose error] {ex}"); }
    }

    private void PresentLoop()
    {
        try
        {
            var glfw = _host.Glfw;
            glfw.MakeContextCurrent(_win);
            glfw.SwapInterval(0);
            var gl = GL.GetApi(glfw.GetProcAddress);

            var (vao, vbo, ebo) = GlShader.CreateFullScreenQuad(gl);
            var progBlit = GlShader.CreateProgram(gl, GlShader.LayerShader.VS, GlShader.LayerShader.FS);
            var uTex = gl.GetUniformLocation(progBlit, "uTex");

            gl.Disable(GLEnum.Blend);

            uint lastTex = 0;
            long lastSeq = 0;
            CodeDrawLayer? lastLayerRef = null;

            while (!ShouldClose)
            {
                glfw.GetFramebufferSize(_win, out var fbW, out var fbH);
                if (fbW == 0 || fbH == 0) { glfw.SwapBuffers(_win); Thread.Sleep(16); continue; }

                gl.BindFramebuffer(GLEnum.Framebuffer, 0);
                gl.Viewport(0, 0, (uint)fbW, (uint)fbH);

                gl.ClearColor(0f, 0f, 0f, 0f);
                gl.Clear((uint)ClearBufferMask.ColorBufferBit);

                var layer = _layer;
                var keepLast = _keepLastFrameUntilReady;

                if (!ReferenceEquals(layer, lastLayerRef))
                {
                    lastLayerRef = layer;
                    lastSeq = 0;
                    if (!keepLast) lastTex = 0;
                }

                if (layer is { IsDisposed: false })
                {
                    layer.WaitForPublish(PresentWaitTimeoutMs);

                    if (layer.TryGetLatest(out var tex, out _, out _, out var fence, out var seq))
                    {
                        var ready = fence == 0;
                        if (!ready)
                        {
                            var s = gl.ClientWaitSync(fence, SyncObjectMask.Bit, 0);
                            ready = s is GLEnum.AlreadySignaled or GLEnum.ConditionSatisfied;
                            if (ready) layer.RequestRetireFence(fence);
                        }

                        if (ready && tex != 0 && seq >= lastSeq)
                        {
                            lastTex = tex;
                            lastSeq = seq;
                        }
                    }
                }

                if (lastTex != 0)
                {
                    gl.UseProgram(progBlit);
                    gl.BindVertexArray(vao);
                    gl.ActiveTexture(GLEnum.Texture0);
                    gl.BindTexture(GLEnum.Texture2D, lastTex);
                    if (uTex >= 0) gl.Uniform1(uTex, 0);
                    gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, null);
                    gl.BindTexture(GLEnum.Texture2D, 0);
                    gl.BindVertexArray(0);
                    gl.UseProgram(0);
                }

                glfw.SwapBuffers(_win);
            }

            gl.DeleteProgram(progBlit);
            gl.DeleteVertexArray(vao);
            gl.DeleteBuffer(vbo);
            gl.DeleteBuffer(ebo);

            glfw.MakeContextCurrent(null);
        }
        finally
        {
            DestroyWindowOnce();
        }
    }
}