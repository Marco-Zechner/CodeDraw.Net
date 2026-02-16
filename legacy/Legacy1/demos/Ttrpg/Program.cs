using Legacy1.MarcoZechner.CodeDrawDotNet;
using Legacy1.MarcoZechner.CodeDrawDotNet.CodeDrawSettings.FontSettings;
using MarcoZechner.ColorDotNet;
using MarcoZechner.MathDotNet;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using SkiaSharp;

namespace MarcoZechner.Ttrpg;

public class Program
{
    private static CodeDraw _dmWindow = null!;
    private static CodeDraw _playerWindow = null!;
    // private static string _map_path = string.Empty;
    // private static ImageHandle? _map_image;
    private static readonly List<ImageHandle> _maps = [];
    private static readonly List<Vector2<double>> _mapPositions = []; // per-map world offset (in image px units)
    private static readonly List<int> _mapRotations = [];             // 0, 90, 180, 270
    private static readonly List<float> _mapScales = [];

    private static int _activeMapIndex = -1;

    private static float _zoom = 0.01f;
    private static Vector2<double> _offset = Vector2<double>.Zero;
    private static float _playerZoom = 0.01f;
    private static Vector2<double> _playerOffset = Vector2<double>.Zero;

    private static Vector2<double> _mousePos;
    private static Vector2<double> _lastMousePos = Vector2<double>.Zero;

    #region Flags
    private static bool _isPanning = false;
    private static bool _tokenScaling = false;
    private static Token? _draggingToken = null;
    private static bool _measuringDistance = false;
    private static bool _tokenFontScaling = false;
    private static bool _mapModification = false; // ALT held?
    private static bool _draggingMap = false;
    #endregion


    public static void Main(string[] args)
    {
        _dmWindow = new("TTRPG_DM", true)
        {
            AutoRender = true
        };
        _dmWindow.OnLoad += LoadDm;
        _dmWindow.OnRender += RenderDm;
        var inputDm = _dmWindow.Input;
        inputDm.OnFileDrop += LoadMap;
        inputDm.OnScroll += MouseScroll;
        inputDm.OnMouseButtonDown += MouseButtonDown;
        inputDm.OnMouseButtonUp += MouseButtonUp;
        inputDm.OnCursorPos += MouseMove;
        _dmWindow.OnResizeEnd += AdjustToFitRatio;
        inputDm.OnKeyDown += KeyDownDm;
        inputDm.OnKeyUp += KeyUpDm;


        _playerWindow = new("TTRPG_Player", true)
        {
            AutoRender = true
        };
        _playerWindow.OnLoad += LoadPlayer;
        _playerWindow.OnRender += RenderPlayer;
        _playerWindow.Input.OnKeyDown += KeyDownPlayer;

        TokenFactory.CreateToken(new Vector2<double>(0, 0), Color.Red, 1.0f, true);
        TokenFactory.CreateToken(new Vector2<double>(100, 0), Color.Green, 1.0f, true, TokenStatus.DEAD);
        TokenFactory.CreateToken(new Vector2<double>(200, 0), Color.Blue, 1.0f, false);

        _dmWindow.Run();
        _playerWindow.Run();

        Task.Run(() =>
        {
            while (_dmWindow.IsRunning || _playerWindow.IsRunning)
            {
                Update();
                Thread.Sleep(16);
            }
        });

        SharedGlManager.Instance.WaitForOpenWindows();
        // _map_image?.Dispose();
        // _map_image = null;
    }

    private static void Update()
    {
        // update loop for shared logic
    }

    private static void LoadDm()
    {
        _dmWindow.Size = new Vector2<int>(16, 9) * 100;
        _dmWindow.AlwaysOnTop = true;
    }

    private static void LoadPlayer() => _playerWindow.Size = new Vector2<int>(16, 9) * 100;

    private static void LoadMap(int count, string[] paths)
    {
        foreach (var p in paths)
        {
            if (string.IsNullOrWhiteSpace(p) || !File.Exists(p)) continue;
            var img = ImageHandler.LoadImage(p);
            _maps.Add(img);
            _mapPositions.Add(Vector2<double>.Zero);
            _mapRotations.Add(0);
            _mapScales.Add(1f);
            _activeMapIndex = _maps.Count - 1; // last loaded is active
        }

        if (_maps.Count == 0) return;

        // fit the *active* map to the DM window as initial camera zoom
        var n = _maps[_activeMapIndex].NaturalSize;
        _zoom = MathG.Clamp(1.0f / MathF.Max(n.X / _dmWindow.Size.X, n.Y / _dmWindow.Size.Y), 0.01f, 10f);
        _offset = Vector2<double>.Zero;

        // token baseline scale tie-in (keep your behavior; use active image width)
        Token.GlobalScale = n.X / 20.0f;
    }


    private static void RenderDm(double dt, SKCanvas canvas, GL gl)
    {
        _dmWindow.Clear(Color.DarkGray);

        TokenFactory.TriggerTokenEvents(ScreenToMap(_mousePos, _dmWindow.Size, _offset, _zoom));

        if (_maps.Count > 0)
        {
            Vector2<double> cdm = _dmWindow.Size / 2.0;

            for (int i = 0; i < _maps.Count; i++)
            {
                var img = _maps[i];
                int rot = _mapRotations[i];
                float s = _mapScales[i];
                var posWorld = _mapPositions[i]; // world offset in image px units

                // oriented natural size depending on 0/90/180/270
                Vector2<float> n = OrientedNaturalSize(img, rot);

                // total scale that hits the screen: camera zoom * per-map scale
                float z = _zoom * s;
                Vector2 imageSize = n * z;

                // screen position of this map's top-left:
                // place map centered at camera, add camera pan, then add this map's world offset * camera zoom
                var screenTl = cdm - (Vector2<double>)imageSize / 2.0 + _offset + posWorld * (double)_zoom;

                // draw
                if (rot % 360 == 0)
                {
                    _dmWindow.Shapes.DrawImage(img,
                        (Vector2<int>)screenTl,
                        imageSize);
                }
                else
                {
                    // 90° steps – we can emulate by drawing with swapped size and a pivot shift.
                    // If your CodeDraw supports rotation, replace with that call.
                    // Fallback: draw via SKCanvas transform.
                    var center = (Vector2<float>)(screenTl + (Vector2<double>)imageSize / 2.0);

                    // Use Skia matrix to rotate around center:
                    canvas.Save();
                    canvas.Translate(center.X, center.Y);
                    canvas.RotateDegrees(rot);
                    // after rotation, draw the unrotated image centered:
                    var rawN = img.NaturalSize; // unrotated size in image px
                    var drawSize = rawN * z;    // scale with total z
                    var tl = new SKPoint(-drawSize.X / 2f, -drawSize.Y / 2f);

                    // NOTE: ImageHandle likely wraps a Skia image internally; if you have an API to draw onto SKCanvas, use it:
                    // e.g., img.DrawOn(canvas, tl, drawSize);
                    // If not, consider adding such a method. As a generic fallback, we draw through CodeDraw with no rotation (not ideal).
                    // --- preferred (pseudo) ---
                    // img.Draw(canvas, tl, drawSize);
                    // --------------------------

                    // Fallback (no rotation capability in Shapes): approximate by swapping size (only correct for 90/270 if your API lacks rotation)
                    canvas.Restore();
                    _dmWindow.Shapes.DrawImage(img,
                        (Vector2<int>)(center - new Vector2<float>(imageSize.Y, imageSize.X) / 2f),
                        new Vector2(imageSize.Y, imageSize.X));
                }
            }

            // camera-only token rendering (independent of maps)
            float zCam = _zoom;
            foreach (var token in TokenFactory.Tokens)
            {
                // world(map) -> screen
                Vector2<float> posScreen = (Vector2<float>)(cdm + _offset + token.Position * (double)zCam);

                // token radius: constant on the map (scales with camera zoom)
                float r = token.Size * zCam * Token.GlobalScale;

                float alpha = token.VisibleToPlayers ? 1.0f : 0.3f;

                float ang = token.Rotation * (MathF.PI / 180f);
                float c = MathF.Cos(ang), s = MathF.Sin(ang);

                // arrow triangles
                float r1 = r;
                Vector2<float> a1 = posScreen + Rotate(new Vector2<float>(0f, -r1), c, s);
                Vector2<float> b1 = posScreen + Rotate(new Vector2<float>(r1 * 1.3f, 0f), c, s);
                Vector2<float> c1 = posScreen + Rotate(new Vector2<float>(0f,  r1), c, s);
                _dmWindow.Shapes.DrawColor = Color.Black with { A = alpha };
                _dmWindow.Shapes.FillTriangle(a1, b1, c1);

                float r2 = r * 0.98f;
                Vector2<float> a2 = posScreen + Rotate(new Vector2<float>(0f, -r2), c, s);
                Vector2<float> b2 = posScreen + Rotate(new Vector2<float>(r2 * 1.25f, 0f), c, s);
                Vector2<float> c2 = posScreen + Rotate(new Vector2<float>(0f,  r2), c, s);
                _dmWindow.Shapes.DrawColor = token.Color with { A = alpha };
                _dmWindow.Shapes.FillTriangle(a2, b2, c2);

                // circles
                _dmWindow.Shapes.DrawColor = Color.Black with { A = alpha };
                _dmWindow.Shapes.FillCircle(posScreen, r);
                _dmWindow.Shapes.DrawColor = token.StatusColor with { A = alpha };
                _dmWindow.Shapes.FillCircle(posScreen, r * 0.98f);
                _dmWindow.Shapes.DrawColor = Color.Black with { A = alpha };
                _dmWindow.Shapes.FillCircle(posScreen, r * 0.90f);
                _dmWindow.Shapes.DrawColor = token.Color with { A = alpha };
                _dmWindow.Shapes.FillCircle(posScreen, r * 0.80f);

                // label
                Color textColor = token.Color.GetBrightness() < 0.5f ? Color.White : Color.Black;
                _dmWindow.Shapes.DrawColor = textColor;
                _dmWindow.Shapes.TextFormat = new TextFormat()
                {
                    HorizontalAlignment = HorizontalAlignment.CENTER,
                    VerticalAlignment = VerticalAlignment.MIDDLE,
                    FontSize = token.FontSize,
                };
                _dmWindow.Shapes.DrawText(posScreen, token.Name);
            }

            if (_measuringDistance)
            {
                _dmWindow.Shapes.DrawColor = Color.Yellow;
                _dmWindow.Shapes.LineWidth = 10;
                _dmWindow.Shapes.DrawLine((Vector2<float>)_lastMousePos, (Vector2<float>)_mousePos);
                _dmWindow.Shapes.DrawColor = Color.DarkGray with { A = 0.5f };
                double dist = (_mousePos - _lastMousePos).Length;
                _dmWindow.Shapes.FillCircle((Vector2<float>)_lastMousePos, (float)dist);
                _dmWindow.Shapes.DrawColor = Color.Orange;
                _dmWindow.Shapes.DrawCircle((Vector2<float>)_lastMousePos, (float)dist);

                double defaultTokenSize = 2.0f * _zoom * Token.GlobalScale; // represents 5 feet
                double distanceInFeet = dist / defaultTokenSize * 5.0;
                double distanceInMeters = distanceInFeet * 0.3048;
                _dmWindow.Shapes.TextFormat = new TextFormat()
                {
                    HorizontalAlignment = HorizontalAlignment.CENTER,
                    VerticalAlignment = VerticalAlignment.BOTTOM,
                    FontSize = 24,
                };
                _dmWindow.Shapes.DrawColor = Color.Black;
                _dmWindow.Shapes.DrawText((Vector2)_mousePos, $"{distanceInFeet:0.##} ft\n{distanceInMeters:0.##} m");
            }
        }
        else
        {
            _dmWindow.Shapes.TextFormat = new TextFormat()
            {
                HorizontalAlignment = HorizontalAlignment.CENTER,
                VerticalAlignment = VerticalAlignment.MIDDLE,
                FontSize = 48,
            };
            _dmWindow.Shapes.DrawColor = Color.White;
            _dmWindow.Shapes.DrawText(_dmWindow.Size / 2, "Drop a map image file here");
        }
        _dmWindow.Show();
    }

private static void RenderPlayer(double dt, SKCanvas canvas, GL gl)
{
    _playerWindow.Clear(Color.DarkGray);

    // --- Player camera follows DM (FIT policy) ---
    float zDm = _zoom;                       // DM zoom (controller)
    Vector2<double> offDm = _offset;         // DM offset (controller)
    var sizeDm = _dmWindow.Size;            // Vector2<int>
    var sizePl = _playerWindow.Size;        // Vector2<int>

    float ratioW = (float)sizePl.X / sizeDm.X;
    float ratioH = (float)sizePl.Y / sizeDm.Y;
    float zPl = zDm * MathF.Min(ratioW, ratioH);   // FIT
    Vector2<double> offPl = offDm * (double)(zPl / zDm);

    _playerZoom = zPl;
    _playerOffset = offPl;

    if (_maps.Count > 0)
    {
        Vector2<double> cpl = _playerWindow.Size / 2.0;

        // --- Draw all maps (same as DM, but with player camera) ---
        for (int i = 0; i < _maps.Count; i++)
        {
            var img = _maps[i];
            int rot = _mapRotations[i];
            float s = _mapScales[i];
            var posWorld = _mapPositions[i];

            Vector2<float> n = OrientedNaturalSize(img, rot);
            float z = _playerZoom * s;
            Vector2 imageSize = n * z;

            var screenTl = cpl - (Vector2<double>)imageSize / 2.0
                                + _playerOffset
                                + posWorld * (double)_playerZoom;

            if (rot % 360 == 0)
            {
                _playerWindow.Shapes.DrawImage(img, (Vector2<int>)screenTl, imageSize);
            }
            else
            {
                var center = (Vector2<float>)(screenTl + (Vector2<double>)imageSize / 2.0);

                canvas.Save();
                canvas.Translate(center.X, center.Y);
                canvas.RotateDegrees(rot);
                var rawN = img.NaturalSize;
                var drawSize = rawN * z; // scale with total z
                var tl = new SKPoint(-drawSize.X / 2f, -drawSize.Y / 2f);

                // TODO: if ImageHandle exposes a Skia draw, call it here (preferred)
                canvas.Restore();

                // Fallback (approximate 90/270 by swapping size)
                _playerWindow.Shapes.DrawImage(
                    img,
                    (Vector2<int>)(center - new Vector2<float>(imageSize.Y, imageSize.X) / 2f),
                    new Vector2(imageSize.Y, imageSize.X)
                );
            }
        }

        // --- Tokens (camera-only) ---
        float zCam = _playerZoom;
        foreach (var token in TokenFactory.Tokens)
        {
            if (!token.VisibleToPlayers) continue;

            Vector2<float> posScreen = (Vector2<float>)(cpl + _playerOffset + token.Position * (double)zCam);
            float r = token.Size * zCam * Token.GlobalScale;

            float ang = token.Rotation * (MathF.PI / 180f);
            float c = MathF.Cos(ang), s = MathF.Sin(ang);

            // arrow triangles
            float r1 = r;
            Vector2<float> a1 = posScreen + Rotate(new Vector2<float>(0f, -r1), c, s);
            Vector2<float> b1 = posScreen + Rotate(new Vector2<float>(r1 * 1.3f, 0f), c, s);
            Vector2<float> c1 = posScreen + Rotate(new Vector2<float>(0f,  r1), c, s);
            _playerWindow.Shapes.DrawColor = Color.Black;
            _playerWindow.Shapes.FillTriangle(a1, b1, c1);

            float r2 = r * 0.98f;
            Vector2<float> a2 = posScreen + Rotate(new Vector2<float>(0f, -r2), c, s);
            Vector2<float> b2 = posScreen + Rotate(new Vector2<float>(r2 * 1.25f, 0f), c, s);
            Vector2<float> c2 = posScreen + Rotate(new Vector2<float>(0f,  r2), c, s);
            _playerWindow.Shapes.DrawColor = token.Color;
            _playerWindow.Shapes.FillTriangle(a2, b2, c2);

            // circles
            _playerWindow.Shapes.DrawColor = Color.Black;
            _playerWindow.Shapes.FillCircle(posScreen, r);
            _playerWindow.Shapes.DrawColor = token.StatusColor;
            _playerWindow.Shapes.FillCircle(posScreen, r * 0.98f);
            _playerWindow.Shapes.DrawColor = Color.Black;
            _playerWindow.Shapes.FillCircle(posScreen, r * 0.90f);
            _playerWindow.Shapes.DrawColor = token.Color;
            _playerWindow.Shapes.FillCircle(posScreen, r * 0.80f);

            // label
            Color textColor = token.Color.GetBrightness() < 0.5f ? Color.White : Color.Black;
            _playerWindow.Shapes.DrawColor = textColor;
            _playerWindow.Shapes.TextFormat = new TextFormat()
            {
                HorizontalAlignment = HorizontalAlignment.CENTER,
                VerticalAlignment = VerticalAlignment.MIDDLE,
                FontSize = token.FontSize,
            };
            _playerWindow.Shapes.DrawText(posScreen, token.Name);
        }

        var localMousePos = DmScreenToPlayerScreen(_mousePos);


        // --- Distance measure (world-accurate even if z differs) ---
        if (_measuringDistance)
        {
            var localLastMousePos = DmScreenToPlayerScreen(_lastMousePos);

            _playerWindow.Shapes.DrawColor = Color.Yellow;
            _playerWindow.Shapes.LineWidth = 10;
            _playerWindow.Shapes.DrawLine((Vector2<float>)localLastMousePos, (Vector2<float>)localMousePos);
            _playerWindow.Shapes.DrawColor = Color.DarkGray with { A = 0.5f };
            double distPx = (localMousePos - localLastMousePos).Length;
            _playerWindow.Shapes.FillCircle((Vector2<float>)localLastMousePos, (float)distPx);
            _playerWindow.Shapes.DrawColor = Color.Orange;
            _playerWindow.Shapes.DrawCircle((Vector2<float>)localLastMousePos, (float)distPx);

            // Convert to WORLD distance (independent of screen zoom)
            double worldDist = distPx / _playerZoom;                    // pixels -> world(map) units
            double feetPerWorldUnit = 5.0 / (2.0 * Token.GlobalScale);   // since 2*globalScale world units == 5 ft
            double distanceInFeet = worldDist * feetPerWorldUnit;
            double distanceInMeters = distanceInFeet * 0.3048;

            _playerWindow.Shapes.TextFormat = new TextFormat()
            {
                HorizontalAlignment = HorizontalAlignment.CENTER,
                VerticalAlignment = VerticalAlignment.BOTTOM,
                FontSize = 24,
            };
            _playerWindow.Shapes.DrawColor = Color.Black;
            _playerWindow.Shapes.DrawText((Vector2)localMousePos, $"{distanceInFeet:0.##} ft\n{distanceInMeters:0.##} m");
        }

        //small cursor cross
        _playerWindow.Shapes.DrawColor = Color.Black;
        _playerWindow.Shapes.LineWidth = 5;
        _playerWindow.Shapes.DrawLine(new Vector2<float>((float)localMousePos.X - 10, (float)localMousePos.Y), new Vector2<float>((float)localMousePos.X + 10, (float)localMousePos.Y));
        _playerWindow.Shapes.DrawLine(new Vector2<float>((float)localMousePos.X, (float)localMousePos.Y - 10), new Vector2<float>((float)localMousePos.X, (float)localMousePos.Y + 10));
        _playerWindow.Shapes.DrawColor = Color.White;
        _playerWindow.Shapes.LineWidth = 3;
        _playerWindow.Shapes.DrawLine(new Vector2<float>((float)localMousePos.X - 10, (float)localMousePos.Y), new Vector2<float>((float)localMousePos.X + 10, (float)localMousePos.Y));
        _playerWindow.Shapes.DrawLine(new Vector2<float>((float)localMousePos.X, (float)localMousePos.Y - 10), new Vector2<float>((float)localMousePos.X, (float)localMousePos.Y + 10));
    }
    else
    {
        _playerWindow.Shapes.TextFormat = new TextFormat()
        {
            HorizontalAlignment = HorizontalAlignment.CENTER,
            VerticalAlignment = VerticalAlignment.MIDDLE,
            FontSize = 48,
        };
        _playerWindow.Shapes.DrawColor = Color.White;
        _playerWindow.Shapes.DrawText(_playerWindow.Size / 2, "Waiting for DM to load a map");
    }

    _playerWindow.Show();
}


    private static void MouseButtonDown(MouseButton button)
    {
        switch (button)
        {
            case MouseButton.Right:
                _isPanning = true;
                _lastMousePos = _dmWindow.Input.GetCursorPos();
                break;
            case MouseButton.Left:
                if (_mapModification && _maps.Count > 0)
                {
                    _activeMapIndex = PickMapUnderMouse(_dmWindow.Input.GetCursorPos());
                    if (_activeMapIndex >= 0)
                    {
                        _draggingMap = true;
                        _lastMousePos = _dmWindow.Input.GetCursorPos();
                    }
                    return;
                }

                // your existing measuring/token drag logic
                if (_dmWindow.Input.GetKey(Keys.M))
                {
                    _measuringDistance = true;
                    _lastMousePos = _dmWindow.Input.GetCursorPos();
                    return;
                }

                if (TokenFactory.MouseOverToken != null)
                {
                    _draggingToken = TokenFactory.MouseOverToken;
                    _lastMousePos = _dmWindow.Input.GetCursorPos();
                }
                break;
        }

    }

    private static void MouseButtonUp(MouseButton button)
    {
        switch (button)
        {
            case MouseButton.Right:
                _isPanning = false;
                break;
            case MouseButton.Left:
                _draggingToken = null;
                _measuringDistance = false;
                _draggingMap = false;
                break;
        }
    }

    private static void MouseMove(Vector2<double> position)
    {
        _mousePos = position;

        if (_draggingMap && _activeMapIndex >= 0)
        {
            var deltaScreen = position - _lastMousePos;
            // convert screen delta to world (image px) delta using camera zoom only
            _mapPositions[_activeMapIndex] += deltaScreen / (double)_zoom;
            _lastMousePos = position;
            return;
        }

        if (_isPanning)
        {
            Vector2<double> delta = position - _lastMousePos;
            _offset += delta;
            _lastMousePos = position;
        }

        if (_draggingToken != null)
        {
            Vector2<double> delta = position - _lastMousePos;
            Vector2<double> mapDelta = delta / (double)_zoom;
            _draggingToken.Position += mapDelta;
            _lastMousePos = position;
        }
    }

    private static void KeyDownPlayer(Keys key)
    {
        switch (key)
        {
            case Keys.F11:
                _playerWindow.Decorated = !_playerWindow.Decorated;
                int offsetY = 30;
                if (!_playerWindow.Decorated)
                {
                    _playerWindow.Size += new Vector2<int>(0, offsetY);
                    _playerWindow.Position -= new Vector2<int>(0, offsetY);
                    return;
                }
                _playerWindow.Size -= new Vector2<int>(0, offsetY);
                _playerWindow.Position += new Vector2<int>(0, offsetY);
                break;
        }
    }

    private static void KeyDownDm(Keys key)
    {
        switch (key)
        {
            case Keys.AltLeft:
            case Keys.AltRight:
                _mapModification = true;
                break;

            case Keys.T:
                _tokenScaling = true;
                break;

            case Keys.F11:
                _dmWindow.Decorated = !_dmWindow.Decorated;
                int offsetY = 30;
                if (!_dmWindow.Decorated)
                {
                    _dmWindow.Size += new Vector2<int>(0, offsetY);
                    _dmWindow.Position -= new Vector2<int>(0, offsetY);
                    return;
                }
                _dmWindow.Size -= new Vector2<int>(0, offsetY);
                _dmWindow.Position += new Vector2<int>(0, offsetY);
                break;
            case Keys.F12:
                _dmWindow.AlwaysOnTop = !_dmWindow.AlwaysOnTop;
                break;
            case Keys.Space:
                if (TokenFactory.MouseOverToken != null)
                {
                    var token = TokenFactory.MouseOverToken;
                    token.Status = (TokenStatus)(((int)token.Status + 1) % Enum.GetValues<TokenStatus>().Length);
                }
                break;
            case Keys.H:
                if (TokenFactory.MouseOverToken != null)
                {
                    TokenFactory.MouseOverToken.VisibleToPlayers = !TokenFactory.MouseOverToken.VisibleToPlayers;
                }
                break;
            case Keys.Delete:
                if (TokenFactory.MouseOverToken != null)
                {
                    TokenFactory.DeleteToken(TokenFactory.MouseOverToken);
                }
                else if (_dmWindow.Input.GetKey(Keys.ControlLeft) && _maps.Count > 0)
                {
                    int idx = PickMapUnderMouse(_dmWindow.Input.GetCursorPos());
                    if (idx >= 0) DeleteMapAt(idx);
                }
                break;
            case Keys.N:
                Vector2<double> mapPos = ScreenToMap(_mousePos, _dmWindow.Size, _offset, _zoom);
                TokenFactory.CreateToken(mapPos, Color.White, 1.0f, false);
                break;
            case Keys.V:
                if (_dmWindow.Input.GetKey(Keys.ControlLeft))
                    TryPasteFromClipboard();
                break;
            case Keys.F:
                _tokenFontScaling = true;
                break;
           }
    }


    private static void KeyUpDm(Keys key)
    {
        if (key == Keys.AltLeft || key == Keys.AltRight)
            _mapModification = false;
        if (key == Keys.T)
        {
            _tokenScaling = false;
        }
        if (key == Keys.F)
        {
            _tokenFontScaling = false;
        }
    }

    private static void TryPasteFromClipboard()
    {
        if (TokenFactory.MouseOverToken == null)
            return;

        if (_dmWindow.Clipboard == null)
            return;

        string text = _dmWindow.Clipboard;
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (text.StartsWith('#') && text.Length == 7)
        {
            try
            {
                TokenFactory.MouseOverToken.Color = new Color(text);
            }
            catch { }
        } else {
            TokenFactory.MouseOverToken.Name = text;
        }
    }


    private static void MouseScroll(Vector2<double> scroll)
    {
        if (_tokenFontScaling)
        {
            if (TokenFactory.MouseOverToken != null)
            {
                const float sensitivity = 0.12f;              // tweak
                float factor = MathF.Pow(1f + sensitivity, (float)scroll.Y);
                TokenFactory.MouseOverToken.FontSize = (int)(TokenFactory.MouseOverToken.FontSize * factor);
            }
            return;
        }

        if (_draggingToken != null)
        {
            _draggingToken.Rotation += (float)scroll.Y * 5f;
            return;
        }

        if (_tokenScaling)
        {
            const float sensitivity = 0.12f;              // tweak
            float factor = MathF.Pow(1f + sensitivity, (float)scroll.Y);
            Token.GlobalScale *= factor;
            return;
        }

        if (_draggingMap && _activeMapIndex >= 0)
        {
            // multiplicative (relative) scaling – size-aware
            const float sensitivity = 0.12f;
            float factor = MathF.Pow(1f + sensitivity, (float)scroll.Y);
            _mapScales[_activeMapIndex] = MathG.Clamp(_mapScales[_activeMapIndex] * factor, 0.01f, 100f);
            return;
        }

        float oldZ = _zoom;
        float newZ = oldZ * MathF.Pow(1.1f, (float)scroll.Y);
        newZ = MathG.Clamp(newZ, 0.01f, 10f);

        if (MathF.Abs(newZ - oldZ) < 1e-9f)
            return; // no effective change

        // 2) Keep the world point under the cursor fixed
        Vector2<double> m = _mousePos;                 // mouse in window coords
        Vector2<double> c = _dmWindow.Size / 2.0;     // window center

        // world point currently under the mouse (before zoom)
        Vector2<double> world = (m - (c + _offset)) / (double)oldZ;

        // new offset so that the same world point stays under the mouse
        Vector2<double> newOffset = m - c - world * (double)newZ;

        _zoom = newZ;
        _offset = newOffset;
    }

    private static void AdjustToFitRatio(Vector2<int> size)
    {
        float a = size.X * size.Y;  // current area

        // Target aspect = W/H (from player window)
        float r = _playerWindow.AspectRatio;

        // Only adjust if aspect differs meaningfully
        if (MathF.Abs(_dmWindow.AspectRatio - r) > 0.01)
        {
            // Exact real-valued solution with same area and target aspect:
            // H* = sqrt(A / r), W* = r * H*
            float hExact = MathF.Sqrt(a / r);
            float wExact = r * hExact;

            // Try a few integer candidates around the optimum and pick minimal area change
            int hFloor = (int)MathF.Floor(hExact);
            int hCeil = (int)MathF.Ceiling(hExact);

            (int W, int H) best = ((int)MathF.Max(1, MathF.Round(wExact)),
                                (int)MathF.Max(1, MathF.Round(hExact)));
            long bestDelta = long.MaxValue;
            long targetArea = size.X * (long)size.Y;

            // Small neighborhood search to counter rounding effects
            foreach (int h in new[] { hFloor - 1, hFloor, hCeil, hCeil + 1 })
            {
                if (h <= 0) continue;
                int w = (int)MathF.Round(r * h);
                if (w <= 0) continue;

                long area = (long)w * h;
                long delta = (long)MathF.Abs(area - targetArea);

                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = (w, h);
                }
            }

            // Optionally: snap to even sizes (comment out if not needed)
            if ((best.W & 1) != 0) best.W--;
            if ((best.H & 1) != 0) best.H--;

            _dmWindow.Size = new Vector2<int>(best.W, best.H);
        }
    }

    // static Vector2<double> TopLeft(Vector2<int> winSize, Vector2<float> n, float z, Vector2<double> o)
    // {
    //     Vector2<double> c = winSize / 2.0;
    //     return c - (Vector2<double>)(n * z) / 2.0 + o; // TL of the image on screen
    // }

    // static Vector2<double> MapToScreen(Vector2<double> mapPx, Vector2<double> tl, float z)
    // {
    //     return tl + mapPx * (double)z;
    // }

    private static Vector2<double> ScreenToMap(Vector2<double> screenPx, Vector2<int> winSize, Vector2<double> offset, float z)
    {
        Vector2<double> c = winSize / 2.0;
        return (screenPx - (c + offset)) / (double)z;
    }

    private static Vector2<float> Rotate(Vector2<float> v, float cosA, float sinA)
    {
        // x' = x*cos - y*sin ; y' = x*sin + y*cos
        return new Vector2<float>(v.X * cosA - v.Y * sinA, v.X * sinA + v.Y * cosA);
    }

    private static Vector2<double> DmScreenToPlayerScreen(Vector2<double> dmScreen)
    {
        // DM: screen -> world
        Vector2<double> cdm = _dmWindow.Size / 2.0;
        Vector2<double> world = (dmScreen - (cdm + _offset)) / (double)_zoom;

        // Player: world -> screen
        Vector2<double> cpl = _playerWindow.Size / 2.0;
        Vector2<double> plScreen = cpl + _playerOffset + world * (double)_playerZoom;

        return plScreen;
    }

    private static Vector2<float> OrientedNaturalSize(ImageHandle img, int rotDeg)
    {
        rotDeg = ((rotDeg % 360) + 360) % 360;
        var n = img.NaturalSize;
        if (rotDeg == 90 || rotDeg == 270)
            return new Vector2<float>(n.Y, n.X);
        return n;
    }

    private static (Vector2<double> TL, Vector2<double> Size) GetMapScreenRect(int i, Vector2<int> winSize)
    {
        var img = _maps[i];
        int rot = _mapRotations[i];
        float s = _mapScales[i];
        var posWorld = _mapPositions[i];

        Vector2<double> c = winSize / 2.0;
        Vector2<float> nOriented = OrientedNaturalSize(img, rot);
        float z = _zoom * s;                       // total scale for this map
        Vector2 sizePx = nOriented * z;

        // same formula you use in RenderDM
        Vector2<double> tl = c - (Vector2<double>)sizePx / 2.0
                            + _offset
                            + posWorld * (double)_zoom;

        return (tl, (Vector2<double>)sizePx);
    }

    private static int PickMapUnderMouse(Vector2<double> mouseScreen)
    {
        for (int i = _maps.Count - 1; i >= 0; i--)
        {
            var (tl, size) = GetMapScreenRect(i, _dmWindow.Size);
            if (mouseScreen.X >= tl.X && mouseScreen.X <= tl.X + size.X &&
                mouseScreen.Y >= tl.Y && mouseScreen.Y <= tl.Y + size.Y)
            {
                return i;
            }
        }
        return -1;
    }

    private static void DeleteMapAt(int index)
    {
        if (index < 0 || index >= _maps.Count) return;

        // dispose image if needed
        // try { _maps[index].Dispose(); } catch { /* ignore */ }

        _maps.RemoveAt(index);
        _mapPositions.RemoveAt(index);
        _mapRotations.RemoveAt(index);
        _mapScales.RemoveAt(index);

        // fix active/dragging indices/flags
        if (_activeMapIndex == index)
        {
            _activeMapIndex = -1;
            _draggingMap = false;
        }
        else if (_activeMapIndex > index)
        {
            _activeMapIndex--;
        }
    }
}
