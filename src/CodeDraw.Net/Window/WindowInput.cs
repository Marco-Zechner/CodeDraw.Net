using MarcoZechner.MathDotNet;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Window;

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
        
        public Vector2<double> MousePos => new(MouseX, MouseY);

        public double WheelDx { get; private set; }
        public double WheelDy { get; private set; }
        
        public Vector2<double> WheelDelta => new(WheelDx, WheelDy);

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