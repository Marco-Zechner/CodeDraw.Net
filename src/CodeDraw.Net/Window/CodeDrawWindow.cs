using System.Diagnostics;
using System.Runtime.CompilerServices;
using MarcoZechner.CodeDrawDotNet.DrawLayer;
using MarcoZechner.CodeDrawDotNet.Shaders;
using MarcoZechner.ColorDotNet;
using MarcoZechner.MathDotNet;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Window;

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
        
        internal void ClearHeldStates()
        {
            _keysHeld.Clear();
            _mouseHeld.Clear();
            _modsDown = ModifierKeys.NONE;
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
                        // case InputAction.Repeat:
                            // not triggered for mouse buttons.
                            // break;
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

    public readonly record struct UpdateContext(CodeDrawWindow Win, WindowInput Input, float DeltaSeconds, long Tick);

    private readonly SharedGlfwHost _host;
    private nint _winPtr;
    private WindowHandle* Win => (WindowHandle*)Volatile.Read(ref _winPtr);
    internal nint WindowHandle => Volatile.Read(ref _winPtr);      
    public bool IsOpen => _nativeOpen && _host.IsWindowAliveById(WindowId);

    private Thread? _presentThread;
    private Thread? _updateThread;

    private volatile bool _closing;     // "logical" closing request for update loop
    private volatile bool _nativeOpen;  // whether a native window currently exists
    private volatile bool _presentStop; // stop signal for present thread
    
    private int _disposed;
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;
    public bool UpdateWhileClosed { get; set; } = false;
    
    public WindowCamera2D Camera { get; } = new();
    
    public Vector2 ProjectLayerToWindow(Vector2 layerPx)
        => Camera.LayerToWindowPoint(layerPx);

    public Vector2 UnprojectWindowToLayer(Vector2 windowPx)
        => Camera.WindowToLayerPoint(windowPx);
    
    private readonly float[] _tmpMat3 = new float[9];

    private void UploadMat3_RowMajor(GL gl, int loc, in Matrix3X3 m)
    {
        // row-major layout
        _tmpMat3[0] = m.M11; _tmpMat3[1] = m.M12; _tmpMat3[2] = m.M13;
        _tmpMat3[3] = m.M21; _tmpMat3[4] = m.M22; _tmpMat3[5] = m.M23;
        _tmpMat3[6] = m.M31; _tmpMat3[7] = m.M32; _tmpMat3[8] = m.M33;

        // transpose=true because GLSL expects column-major when transpose=false
        gl.UniformMatrix3(loc, 1, true, _tmpMat3);
    }
    
    // only used for final cleanup once. Close/Open should not touch this.
    private int _releasedIdOnce;
    
    private void ReleaseIdOnceFinal()
    {
        if (Interlocked.Exchange(ref _releasedIdOnce, 1) != 0) return;
        _host.ReleaseWindowId(WindowId);
    }

    public int WindowId { get; }
    
    
    public string DebugName => $"[Window:{WindowId}:'{Title}']";

    private WindowState _preMinimizeState = WindowState.Windowed;
    public CodeDrawLayer Layer { get; private set; }
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
        if (ReferenceEquals(Layer, layer)) return;

        // a window always needs a layer.
        if (layer == null) return;

        Layer = layer;
        _keepLastFrameUntilReady = keepLastFrameUntilReady;

        _host.AssignWindowLayer(WindowId, layer);
    }

    public bool ShouldClose => _closing || IsDisposed;

    public CodeDrawWindow(
        SharedGlfwHost host,
        int w, int h,
        int x, int y,
        string title,
        bool autoOpen = true, bool stealFocusOnOpen = false)
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
            TransparentAlpha: false,
            StealFocusOnOpen: stealFocusOnOpen,
            PresentMode: WindowPresentMode.FitStretch,
            BackgroundColor: Color.Transparent
        ).Normalize();

        WindowId = _host.ReserveWindowId();

        // Layer exists regardless of open/close. It may be resized when settings change.
        Layer = new CodeDrawLayer(_host, w, h, $"{WindowId}:'{title}'");
        _host.RegisterAutoLayerOwner(WindowId, Layer); 

        // Always run update thread (it can idle when closed).
        _updateThread = new Thread(UpdateLoop) { IsBackground = true, Name = $"Update:{title}" };
        _updateThread.Start();

        if (autoOpen)
            Open();
    }

    public CodeDrawWindow(SharedGlfwHost host, int w, int h, string title, bool autoOpen = true)
        : this(host, w, h, 50, 120, title, autoOpen) {}

    
    public void Close()
    {
        if (IsDisposed) return;
        if (!_nativeOpen) return;

        _nativeOpen = false;

        StopPresentThread();

        // Destroy native window by id (host owns mapping)
        _host.DestroyWindowById(WindowId);

        Volatile.Write(ref _winPtr, 0);
        Input.ClearHeldStates();
    }
    
    /// <summary>
    /// Opens (or reopens) the native window using current settings, or optional overrides.
    /// </summary>
    public void Open(WindowSettingsSnapshot? overrideSettings = null)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, nameof(CodeDrawWindow));
        if (_nativeOpen) return;

        _nativeOpen = true;
        _presentStop = false;

        if (overrideSettings.HasValue)
            Settings = overrideSettings.Value; // will store snapshot; no host call because Win==null

        var raw = RawSettings;

        var created = _host.CreateOrRecreateWindowForId(
            WindowId,
            raw.WindowPosition.X, raw.WindowPosition.Y,
            raw.Size.X, raw.Size.Y,
            raw.Title,
            owner: this,
            raw.StealFocusOnOpen);

        Volatile.Write(ref _winPtr, (nint)created);

        // apply snapshot to fresh window
        _host.ApplyWindowSettingsSync(created, WindowId, raw,
            WindowDirty.Title |
            WindowDirty.Border |
            WindowDirty.WindowPos |
            WindowDirty.CanvasSize |
            WindowDirty.WindowState |
            WindowDirty.AlwaysOnTop |
            WindowDirty.ClickThrough);

        _presentThread = new Thread(PresentLoop) { IsBackground = true, Name = $"Presenter:{raw.Title}" };
        _presentThread.Start();

        _startFired = false;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        Console.WriteLine("Window " + DebugName + " Disposing...");
        
        try { Close(); } catch { /* ignored */ }

        _closing = true;
        WaitForCloseThreads();

        ReleaseIdOnceFinal();

        _host.NotifyWindowDisposed(WindowId);
        Layer = null!; // Layer can now be set null, since window is dead.
    }
    
    private void StopPresentThread()
    {
        _presentStop = true;
        var p = Interlocked.Exchange(ref _presentThread, null);
        if (p is { IsAlive: true }) p.Join();
        _presentStop = false;
    }

    private void WaitForCloseThreads()
    {
        var u = Interlocked.Exchange(ref _updateThread, null);
        if (u is { IsAlive: true }) u.Join();

        var p = Interlocked.Exchange(ref _presentThread, null);
        if (p is { IsAlive: true }) p.Join();
    }

    private void HandleEvent(object evt)
    {
        switch (evt)
        {
            case SharedGlfwHost.MouseMoveEvent mm when mm.WindowId == WindowId:
            {
                var cs = Settings.Size;
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
                Close();
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

        Vector2<int> pos = default;
        var notify = false;

        Vector2<int> oldClient;

        lock (_settingsLock)
        {
            oldClient = _settings.ClientSize;
            _settings = _settings with { Size = new Vector2<int>(w, h) };

            if (_settings.State == WindowState.Windowed)
            {
                pos = _settings.WindowPosition;
                notify = true;
            }
        }

        // After updating settings, read new client size (outside lock is fine; Settings locks anyway)
        var newClient = Settings.Size;

        // Auto camera policy only when PresentMode==Camera
        var snap = Settings;
        if (snap.PresentMode == WindowPresentMode.Camera)
            Camera.OnWindowResized(oldClient.X, oldClient.Y, newClient.X, newClient.Y);

        if (notify)
            _host.NotifyWindowedRect(WindowId, pos.X, pos.Y, w, h);
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

    // ============================================================
    // UpdateLoop: should survive close/open.
    // - if closed, drain queues (optional) but don’t touch _win
    // ============================================================
    private void UpdateLoop()
    {
        var sw = Stopwatch.StartNew();
        var lastTicks = sw.ElapsedTicks;
        long tick = 0;

        while (!_closing && !IsDisposed)
        {
            var loopStartTicks = sw.ElapsedTicks;
            var deltaSec = (float)((loopStartTicks - lastTicks) / (double)Stopwatch.Frequency);
            lastTicks = loopStartTicks;

            Input.BeginUpdateFrame();

            // Even if closed, you can still drain input queues (it’ll be empty).
            _host.DrainWindowInput(WindowId, HandleEvent);
            _host.PumpHostInputForWindow(this);

            // Fire OnStart once per Open() session (since we reset _startFired on Open)
            if (!_startFired && OnStart != null && IsOpen)
            {
                _startFired = true;
                try { OnStart(this); }
                catch (Exception ex) { Console.WriteLine($"[OnStart error] {ex}"); }
            }

            if (IsOpen || UpdateWhileClosed)
            {
                var cb = OnUpdate;
                if (cb != null)
                {
                    try { cb(new UpdateContext(this, Input, deltaSec, tick)); }
                    catch (Exception ex) { Console.WriteLine($"[OnUpdate error] {ex}"); }
                }
            }
            else
                Thread.Sleep(50);

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

    // ============================================================
    // PresentLoop: runs only while native window is open. It dies on Close().
    // ============================================================
    private void PresentLoop()
    {
        var win = (WindowHandle*)Volatile.Read(ref _winPtr);
        if (win == null) return;

        try
        {
            LockedGlfw.MakeContextCurrent(win);
            LockedGlfw.SwapInterval(0);
            var gl = GL.GetApi(LockedGlfw.GetProcAddress);

            var (vao, vbo, ebo) = ShaderCompiler.CreateFullScreenQuad(gl);

            var progBlit = new AutoProgram(this, ShaderPath.Engine("layerShader"));
            var uBlitTex = new AutoUniform(gl, this, progBlit, "uTex");
            var uForceOpaque = new AutoUniform(gl, this, progBlit, "uForceOpaque");
            var uPresentMode = new AutoUniform(gl, this, progBlit, "uPresentMode");
            var uWindowSize  = new AutoUniform(gl, this, progBlit, "uWindowSizePx");
            var uLayerSize   = new AutoUniform(gl, this, progBlit, "uLayerSizePx");
            var uW2L         = new AutoUniform(gl, this, progBlit, "uWindowToLayer");
            var uBg          = new AutoUniform(gl, this, progBlit, "uBackground");

            gl.Disable(GLEnum.Blend);

            uint lastTex = 0;
            long lastSeq = 0;
            CodeDrawLayer? lastLayerRef = null;

            while (!_presentStop && IsOpen && !_closing && !IsDisposed)
            {
                const double RESIZE_TIMEOUT = 100; //ms
                if (_host.IsWindowInLiveResize(WindowId, RESIZE_TIMEOUT))
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

                    if (layer.TryGetLatest(out var tex, out _, out _, out _, out var seq))
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
                    if (snap.PresentMode == WindowPresentMode.Camera)
                    {
                        var bg = snap.BackgroundColor;
                        gl.Disable(GLEnum.ScissorTest);
                        gl.ClearColor(bg.R, bg.G, bg.B, opaque ? 1f : bg.A);
                        gl.Clear((uint)ClearBufferMask.ColorBufferBit);
                    }

                    gl.Enable(GLEnum.ScissorTest);
                    gl.Scissor(0, 0, (uint)client.X, (uint)client.Y);
                    gl.Viewport(0, 0, (uint)client.X, (uint)client.Y);
                    
                    gl.UseProgram(progBlit);
                    gl.BindVertexArray(vao);
                    gl.ActiveTexture(GLEnum.Texture0);
                    gl.BindTexture(GLEnum.Texture2D, lastTex);
                    if (uBlitTex >= 0) gl.Uniform1(uBlitTex, 0);
                    if (uForceOpaque >= 0) gl.Uniform1(uForceOpaque, opaque ? 1 : 0);
                    if (uPresentMode >= 0) gl.Uniform1(uPresentMode, (int)snap.PresentMode);
                    if (uWindowSize >= 0) Uniform2F(gl, uWindowSize, client.X, client.Y);
                    if (uLayerSize >= 0)  Uniform2F(gl, uLayerSize, layer.Width, layer.Height);
                    
                    if (snap.PresentMode == WindowPresentMode.Camera && uW2L >= 0)
                    {
                        var m = Camera.WindowToLayer;
                        UploadMat3_RowMajor(gl, uW2L, m);
                    }
                    
                    gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, null);
                    gl.BindTexture(GLEnum.Texture2D, 0);
                    gl.BindVertexArray(0);
                    gl.UseProgram(0);
                }

                gl.Disable(GLEnum.ScissorTest);

                LockedGlfw.SwapBuffers(win);

                // var err = gl.GetError();
                // if (err != GLEnum.NoError)
                //     Console.WriteLine($"GL error: {err}");
            }

            ShaderStore.DisposeConsumer(gl, this);
            gl.DeleteVertexArray(vao);
            gl.DeleteBuffer(vbo);
            gl.DeleteBuffer(ebo);

            LockedGlfw.MakeContextCurrent(null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PresentLoop error] {ex}");
        }
        finally
        {
            // On Close() we destroy via host by id, so just detach context.
            try { LockedGlfw.MakeContextCurrent(null); }
            catch { /* ignored */ }
        }
    }
    
    internal void Host_SetNativeHandle(WindowHandle* newWin)
    {
        Volatile.Write(ref _winPtr, (nint)newWin);
    }

    internal void Host_ClearNativeHandle()
    {
        Volatile.Write(ref _winPtr, 0);
        Input.ClearHeldStates();
    }
    
    internal void Host_RestartPresenterIfOpen()
    {
        if (!_nativeOpen || IsDisposed) return;

        // stop old presenter
        StopPresentThread();

        // start new
        var raw = RawSettings;
        _presentThread = new Thread(PresentLoop) { IsBackground = true, Name = $"Presenter:{raw.Title}" };
        _presentThread.Start();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Uniform2F(GL gl, int loc, float x, float y)
        => gl.Uniform2(loc, x, y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Uniform4F(GL gl, int loc, float x, float y, float z, float w)
        => gl.Uniform4(loc, x, y, z, w);
}