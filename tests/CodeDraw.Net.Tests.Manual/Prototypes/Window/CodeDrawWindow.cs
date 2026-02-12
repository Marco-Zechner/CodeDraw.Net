using System.Diagnostics;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;
using MarcoZechner.MathDotNet;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Window;

public sealed unsafe partial class CodeDrawWindow : IDisposable, IShaderConsumer
{
    //TODO: add a "IsFocused" property that reflects whether this window is currently focused/active in the OS, so users know if mousePos is valid for example.

    public sealed class WindowInput
    {
        private readonly HashSet<Keys> _keysHeld = [];
        private readonly HashSet<MouseButton> _mouseHeld = [];

        private readonly HashSet<Keys> _keysDown = [];
        private readonly HashSet<Keys> _keysUp = [];
        private readonly HashSet<MouseButton> _mouseDown = [];
        private readonly HashSet<MouseButton> _mouseUp = [];

        private ModifierKeys _modsDown = ModifierKeys.NONE;

        public double MouseX { get; private set; }
        public double MouseY { get; private set; }

        public double WheelDx { get; private set; }
        public double WheelDy { get; private set; }

        /// <summary>
        /// Checks if the specified modifier key(s) are currently toggled on (CapsLock, NumpadLock) or held down (Shift, Ctrl, Alt, Super).
        ///
        /// </summary>
        /// <param name="mod"></param>
        /// <returns></returns>
        public bool GetModifierState(ModifierKeys mod) => _modsDown.HasFlag(mod);

        public bool GetKey(Keys key) => _keysHeld.Contains(key);
        public bool GetKeyDown(Keys key) => _keysDown.Contains(key);
        public bool GetKeyUp(Keys key) => _keysUp.Contains(key);

        /// <summary>
        /// <para>
        /// Checks if the specified combination of modifier keys is currently held down.
        /// This works by converting the ModifierKeys into their corresponding Keys (e.g., Shift -> ShiftLeft and ShiftRight) and checking if any of those keys are currently held down.
        /// </para>
        /// <b>NOTE:</b> Sticky-Keys like CapsLock will reflect if they are held down, NOT their toggled state.
        /// To check the toggled state of sticky keys, use <see cref="GetModifierState"/> instead.
        /// </summary>
        /// <param name="mods"></param>
        /// <returns></returns>
        public bool GetKey(ModifierKeys mods) => _keysHeld.Overlaps(mods.ToKeys());
        /// <summary>
        /// <para>
        /// Checks if the specified combination of modifier keys was pressed down in the current frame.
        /// This works by converting the ModifierKeys into their corresponding Keys (e.g., Shift -> ShiftLeft and ShiftRight) and checking if any of those keys were pressed down in the current frame.
        /// </para>
        /// <b>NOTE:</b> Sticky-Keys like CapsLock will reflect if they are held down, NOT their toggled state.
        /// To check the toggled state of sticky keys, use <see cref="GetModifierState"/> instead.
        /// </summary>
        /// <param name="mods"></param>
        /// <returns></returns>
        public bool GetKeyDown(ModifierKeys mods) => _keysDown.IsSupersetOf(mods.ToKeys());
        /// <summary>
        /// <para>
        /// Checks if the specified combination of modifier keys was released in the current frame.
        /// This works by converting the ModifierKeys into their corresponding Keys (e.g., Shift -> ShiftLeft and ShiftRight) and checking if any of those keys were released in the current frame.
        /// </para>
        /// <b>NOTE:</b> Sticky-Keys like CapsLock will reflect if they are held down, NOT their toggled state.
        /// To check the toggled state of sticky keys, use <see cref="GetModifierState"/> instead.
        /// </summary>
        /// <param name="mods"></param>
        /// <returns></returns>
        public bool GetKeyUp(ModifierKeys mods) => _keysUp.IsSupersetOf(mods.ToKeys());

        public bool GetMouseButton(MouseButton b) => _mouseHeld.Contains(b);
        public bool GetMouseButtonDown(MouseButton b) => _mouseDown.Contains(b);
        public bool GetMouseButtonUp(MouseButton b) => _mouseUp.Contains(b);

        /// <summary>
        /// Returns the current state of modifier keys (Shift, Ctrl, Alt, Super, CapsLock, NumLock).
        /// These are updated on any key or mouse button event, and reflect the state at the time of that event.
        /// </summary>
        /// <returns></returns>
        public ModifierKeys GetModifiers() => _modsDown;

        public HashSet<Keys> GetAllKeys() => _keysHeld.ToHashSet();
        public HashSet<Keys> GetAllKeysDown() => _keysDown.ToHashSet();
        public HashSet<Keys> GetAllKeysUp() => _keysUp.ToHashSet();

        public HashSet<MouseButton> GetAllMouseButtons() => _mouseHeld.ToHashSet();
        public HashSet<MouseButton> GetAllMouseButtonsDown() => _mouseDown.ToHashSet();
        public HashSet<MouseButton> GetAllMouseButtonsUp() => _mouseUp.ToHashSet();

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
                    MouseX = mm.X; MouseY = mm.Y; break;

                case SharedGlfwHost.MouseWheelEvent mw:
                    WheelDx += mw.Dx; WheelDy += mw.Dy; break;

                case SharedGlfwHost.MouseButtonEvent mb:
                {
                    _modsDown = mb.Mods;
                    switch (mb.Action)
                    {
                        case InputAction.Press:
                            _mouseHeld.Add(mb.Button);
                            _mouseDown.Add(mb.Button);
                            break;
                        default:
                        case InputAction.Release:
                            _mouseHeld.Remove(mb.Button);
                            _mouseUp.Add(mb.Button);
                            break;
                        case InputAction.Repeat:
                            // not triggered for mouse buttons.
                            break;
                    }
                    break;
                }

                case SharedGlfwHost.KeyEvent ke:
                {
                    _modsDown = ke.Mods;
                    switch (ke.Action)
                    {
                        case InputAction.Press:
                            _keysHeld.Add(ke.Key);
                            _keysDown.Add(ke.Key);
                            break;
                        default:
                        case InputAction.Release:
                            _keysHeld.Remove(ke.Key);
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
        float DeltaSeconds,
        long Tick
    );

    private readonly SharedGlfwHost _host;
    private readonly WindowHandle* _win;
    internal nint WindowHandle => (nint)_win;

    private Thread? _presentThread;
    private Thread? _updateThread;

    private volatile bool _closing;
    private int _disposed;
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;
    private int _windowDestroyed; // 0 = not yet, 1 = done

    private WindowState _preMinimizeState = WindowState.Windowed;
    
    public CodeDrawLayer? Layer { get; private set; }

    public int WindowId { get; }
    
    public string DebugName => $"[Window id={WindowId} title='{Title}']";

    public WindowInput Input { get; } = new();

    public int UpdateDelayMs { get; set; } = 16;
    public int PresentWaitTimeoutMs { get; set; } = 16;

    private volatile bool _keepLastFrameUntilReady = true;

    public Action<CodeDrawWindow>? OnStart { get; set; }
    public Action<UpdateContext>? OnUpdate { get; set; }
    public Action<CodeDrawWindow>? OnClose { get; set; }

    private bool _startFired;

    public void SetPresentedLayer(CodeDrawLayer? layer, bool keepLastFrameUntilReady = true)
    {
        Layer = layer;
        _keepLastFrameUntilReady = keepLastFrameUntilReady;
    }

    public bool ShouldClose => _closing || IsDisposed;

    public CodeDrawWindow(SharedGlfwHost host, int w, int h, int x, int y, string title)
    {
        _host = host;

        _settings = new WindowSettingsSnapshot(
            WindowPosition: new Vector2<int>(x, y),
            Size: new Vector2<int>(w, h),
            Title: title,
            AlwaysOnTop: false,
            FrameMode: WindowFrameMode.Decorated,
            ResizeMode: WindowResizeMode.Resizable,
            MinSize: Vector2<int>.Zero,
            MaxSize: Vector2<int>.Zero,
            AspectRatio: Vector2<int>.Zero,
            State: WindowState.Windowed,
            ClickThrough: false,
            TransparentAlpha: true
        ).Normalize();

        _win = host.CreateWindow(x, y, w, h, Title);
        _host.RegisterWindowObject(_win, this);
        WindowId = host.GetWindowId(_win);

        Layer = new CodeDrawLayer(host, w, h, "WindowLayer:" + WindowId);

        _presentThread = new Thread(PresentLoop) { IsBackground = true, Name = $"Presenter:{Title}" };
        _updateThread  = new Thread(UpdateLoop)  { IsBackground = true, Name = $"Update:{Title}" };

        _presentThread.Start();
        _updateThread.Start();
    }

    public CodeDrawWindow(SharedGlfwHost host, int w, int h, string title)
        : this(host, w, h, 50, 120, title) {}

    private void DestroyWindowOnce()
    {
        if (Interlocked.Exchange(ref _windowDestroyed, 1) != 0) return;
        _host.UnregisterWindowObject(_win);
        _host.DestroyWindow(_win);
    }

    public void Close()
    {
        if (_closing) return;
        _closing = true;

        var win = _win;
        _host.InvokeHostAsync(() =>
        {
            if (!_host.IsWindowAlive(win)) return;
            LockedGlfw.SetWindowShouldClose(win, true);
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

        Layer?.Dispose();
        Layer = null;
    }

    private void HandleEvent(object evt)
    {
        switch (evt)
        {
            case SharedGlfwHost.MouseMoveEvent mm when mm.WindowId == WindowId:
            {
                // clamp using client size
                var cs = Settings.Size; // client size
                var x = mm.X;
                var y = mm.Y;
                if (x < 0) x = 0;
                if (y < 0) y = 0;
                if (x > cs.X - 1) x = cs.X - 1;
                if (y > cs.Y - 1) y = cs.Y - 1;
                evt = mm with { X = x, Y = y };
                break;
            }
            
            case SharedGlfwHost.WindowCloseRequestedEvent cl when cl.WindowId == WindowId:
                _closing = true;

                var win = _win;
                _host.InvokeHostAsync(() =>
                {
                    if (!_host.IsWindowAlive(win)) return;
                    LockedGlfw.SetWindowShouldClose(win, true);
                });
                return;
                
            case SharedGlfwHost.WindowPosEvent wp when wp.WindowId == WindowId:
                ApplyOsPosToSettings(wp.X, wp.Y);
                break;

            case SharedGlfwHost.WindowSizeEvent ws when ws.WindowId == WindowId:
                ApplyOsSizeToSettings(ws.W, ws.H);
                break;
            
            case SharedGlfwHost.WindowMaximizedEvent mx when mx.WindowId == WindowId:
                ApplyOsMaximizedToSettings(mx.IsMaximized);
                break;

            case SharedGlfwHost.WindowIconifiedEvent ic when ic.WindowId == WindowId:
                ApplyOsIconifiedToSettings(ic.IsIconified);
                break;
        }

        Input.Apply(evt);
    }

    private void ApplyOsPosToSettings(int x, int y)
    {
        lock (_settingsLock)
        {
            // don’t call Settings property setter here
            _settings = _settings with { WindowPosition = new Vector2<int>(x, y) };
        }
    }

    private void ApplyOsSizeToSettings(int w, int h)
    {
        if (w < 1) w = 1;
        if (h < 1) h = 1;

        lock (_settingsLock)
        {
            _settings = _settings with { Size = new Vector2<int>(w, h) };

            if (_settings.State == WindowState.Windowed)
            {
                // We can safely record a full windowed rect using the event data + current pos.
                var p = _settings.WindowPosition;
                _host.NotifyWindowedRect(WindowId, p.X, p.Y, w, h); // implement: thread-safe dict update
            }
        }
    }
    
    private void ApplyOsMaximizedToSettings(bool isMaximized)
    {
        lock (_settingsLock)
        {
            // Only OS-driven for REAL maximize; ignore if you're in manual modes.
            if (_settings.State is WindowState.BorderlessMaximized or WindowState.BorderlessFullscreen)
                return;

            if (isMaximized)
            {
                _settings = _settings with { State = WindowState.Maximized };
            }
            else
            {
                // When user drags titlebar out of maximize, Windows un-maximizes -> we must unlock settings
                if (_settings.State == WindowState.Maximized)
                    _settings = _settings with { State = WindowState.Windowed };
            }
        }
    }

    private void ApplyOsIconifiedToSettings(bool isIconified)
    {
        lock (_settingsLock)
        {
            if (_settings.State is WindowState.BorderlessMaximized or WindowState.BorderlessFullscreen)
                return;

            if (isIconified)
            {
                if (_settings.State == WindowState.Minimized)
                    return;

                _preMinimizeState = _settings.State;
                _settings = _settings with { State = WindowState.Minimized };
            }
            else
            {
                if (_settings.State != WindowState.Minimized)
                    return;

                var target = _preMinimizeState;
                if (target == WindowState.Minimized) target = WindowState.Windowed;
                _settings = _settings with { State = target };
            }
        }
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
            _host.PumpHostInputForWindow(this);

            if (!_startFired && OnStart != null)
            {
                _startFired = true;
                try { OnStart(this); }
                catch (Exception ex) { Console.WriteLine($"[OnStart error] {ex}"); }
            }

            var cb = OnUpdate;
            if (cb != null)
            {
                try { cb(new UpdateContext(this, Input, deltaSec, tick)); }
                catch (Exception ex) { Console.WriteLine($"[OnUpdate error] {ex}"); }
            }

            tick++;

            var elapsedMs = (int)((sw.ElapsedTicks - loopStartTicks) * 1000.0 / Stopwatch.Frequency);
            var sleepMs = UpdateDelayMs - elapsedMs;
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
            LockedGlfw.MakeContextCurrent(_win);
            LockedGlfw.SwapInterval(0);
            var gl = GL.GetApi(LockedGlfw.GetProcAddress);

            // gl.Enable(GLEnum.DebugOutput);
            // gl.Enable(GLEnum.DebugOutputSynchronous);
            //
            // gl.DebugMessageCallback((source, type, id, severity, length, message, userParam) =>
            // {
            //     var msg = new string((sbyte*)message, 0, length);
            //     Console.WriteLine($"GL Debug Message: Source={source}, Type={type}, ID={id}, Severity={severity}, Message={msg}");
            // }, null);
            // gl.DebugMessageControl(GLEnum.DontCare, GLEnum.DontCare, GLEnum.DebugSeverityNotification, 0, null, false);

            var (vao, vbo, ebo) = ShaderCompiler.CreateFullScreenQuad(gl);

            var progBlit = new AutoProgram(this, ShaderPath.Engine("layerShader"));
            var uBlitTex = new AutoUniform(gl, this, progBlit, "uTex");
            var uForceOpaque = new AutoUniform(gl, this, progBlit, "uForceOpaque");

            gl.Disable(GLEnum.Blend);

            uint lastTex = 0;
            long lastSeq = 0;
            CodeDrawLayer? lastLayerRef = null;

            while (!ShouldClose)
            {
                if (_host.IsWindowInLiveResize(WindowId))
                {
                    // skip a frame if the window is currently being resized by the user.
                    // if we call swapBuffer here at a bad time when the user is resizing, then it can crash the GPU driver... :/
                    Thread.Sleep(16);
                    continue;
                }

                var snap = Settings;
                var raw  = RawSettings; 

                var client = snap.Size;
                var physical = raw.Size;
                
                ShaderStore.CheckHotReload(gl, this);

                gl.Viewport(0, 0, (uint)physical.X, (uint)physical.Y);

                var opaque = !snap.TransparentAlpha;
                gl.Disable(GLEnum.ScissorTest);
                gl.ClearColor(0f, 0f, 0f, opaque ? 1f : 0f);
                gl.Clear((uint)ClearBufferMask.ColorBufferBit);

                gl.Enable(GLEnum.ScissorTest);
                gl.Scissor(0, 0, (uint)client.X, (uint)client.Y); // scissor in pixels, origin bottom-left in GL
                gl.Viewport(0, 0, (uint)client.X, (uint)client.Y);
                
                var layer = Layer;
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
                        if (tex != 0 && seq >= lastSeq)
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
                    if (uBlitTex >= 0) gl.Uniform1(uBlitTex, 0);
                    if (uForceOpaque >= 0) gl.Uniform1(uForceOpaque, opaque ? 1 : 0);
                    gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, null);
                    gl.BindTexture(GLEnum.Texture2D, 0);
                    gl.BindVertexArray(0);
                    gl.UseProgram(0);
                }
                
                gl.Disable(GLEnum.ScissorTest);

                LockedGlfw.SwapBuffers(_win);

                var err = gl.GetError();
                if (err != GLEnum.NoError)
                {
                    Console.WriteLine($"GL error: {err}");
                }
            }

            ShaderStore.DisposeConsumer(gl, this);

            gl.DeleteVertexArray(vao);
            gl.DeleteBuffer(vbo);
            gl.DeleteBuffer(ebo);

            LockedGlfw.MakeContextCurrent(null);
        }
        finally
        {
            DestroyWindowOnce();
        }
    }

}