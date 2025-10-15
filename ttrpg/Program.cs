
using MarcoZechner.CodeDrawDotNet;
using MarcoZechner.ColorLib;
using MarcoZechner.Math;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using SkiaSharp;

namespace MarcoZechner.ttrpg;

public class Program
{
    private static CodeDraw _dm_window = null!;
    private static CodeDraw _player_window = null!;
    private static string _map_path = string.Empty;
    private static ImageHandle? _map_image;

    private static float _zoom = 0.01f;
    private static Vector2<double> _offset = Vector2<double>.Zero;
    private static float _player_zoom = 0.01f;
    private static Vector2<double> _player_offset = Vector2<double>.Zero;

    private static Vector2<double> _mousePos;
    private static Vector2<double> _lastMousePos = Vector2<double>.Zero;

    #region Flags
    private static bool _isPanning = false;
    private static bool _tokenScaling = false;
    private static Token? _draggingToken = null;
    private static bool _measuringDistance = false;
    private static bool _tokenFontScaling = false;
    #endregion


    public static void Main(string[] args)
    {
        _dm_window = new("TTRPG_DM", true)
        {
            AutoRender = true
        };
        _dm_window.OnLoad += LoadDM;
        _dm_window.OnFileDrop += LoadMap;
        _dm_window.OnRender += RenderDM;
        _dm_window.OnScroll += MouseScroll;
        _dm_window.OnMouseButtonDown += MouseButtonDown;
        _dm_window.OnMouseButtonUp += MouseButtonUp;
        _dm_window.OnCursorPos += MouseMove;
        _dm_window.OnResizeEnd += AdjustToFitRatio;
        _dm_window.OnKeyDown += KeyDownDM;
        _dm_window.OnKeyUp += KeyUpDM;


        _player_window = new("TTRPG_Player", true)
        {
            AutoRender = true
        };
        _player_window.OnLoad += LoadPlayer;
        _player_window.OnRender += RenderPlayer;
        _player_window.OnKeyDown += KeyDownPlayer;

        TokenFactory.CreateToken(new Vector2<double>(0, 0), Color.RED, 1.0f, true);
        TokenFactory.CreateToken(new Vector2<double>(100, 0), Color.GREEN, 1.0f, true, TokenStatus.Dead);
        TokenFactory.CreateToken(new Vector2<double>(200, 0), Color.BLUE, 1.0f, false);

        _dm_window.Run();
        _player_window.Run();

        Task.Run(() =>
        {
            while (_dm_window.IsRunning || _player_window.IsRunning)
            {
                Update();
                Thread.Sleep(16);
            }
        });

        CodeDraw.WaitForOpenWindows();
        _map_image?.Dispose();
        _map_image = null;
    }

    private static void Update()
    {
        // update loop for shared logic
    }

    private static void LoadDM()
    {
        _dm_window.Size = new Vector2<int>(16, 9) * 100;
        _dm_window.AlwaysOnTop = true;
    }

    private static void LoadPlayer() => _player_window.Size = new Vector2<int>(16, 9) * 100;

    private static void LoadMap(int count, string[] paths)
    {
        _map_path = paths.FirstOrDefault() ?? string.Empty;
        if (string.IsNullOrEmpty(_map_path) || !File.Exists(_map_path))
            return;
        _map_image = ImageHandler.LoadImage(_map_path);

        _zoom = GenericMath.Clamp(1.0f / MathF.Max(_map_image.NaturalSize.X / _dm_window.Size.X, _map_image.NaturalSize.Y / _dm_window.Size.Y), 0.01f, 10f);
        _offset = Vector2<double>.Zero;

        Token.globalScale = _map_image.NaturalSize.X / 20.0f;
    }

    private static void RenderDM(double dt, SKCanvas canvas, GL gl)
    {
        _dm_window.Clear(Color.DARK_GRAY);

        TokenFactory.TriggerTokenEvents(ScreenToMap(_mousePos, TopLeft(_dm_window.Size, _map_image?.NaturalSize ?? Vector2<float>.Zero, _zoom, _offset), _zoom));

        if (_map_image != null)
        {
            Vector2<float> n = _map_image.NaturalSize;
            float z = _zoom;
            Vector2 imageSize = n * z;

            _dm_window.Shapes.DrawImage(_map_image, _dm_window.Size / 2 - (Vector2<int>)imageSize / 2 + (Vector2<int>)_offset, imageSize);

            Vector2<double> tl = TopLeft(_dm_window.Size, n, z, _offset);
            foreach (var token in TokenFactory.Tokens)
            {
                // 3.1 Position: stays constant on the map
                Vector2<float> posScreen = (Vector2<float>)MapToScreen(token.Position, tl, z);

                float r = token.Size * z * Token.globalScale;

                float alpha = 1.0f;
                if (!token.VisibleToPlayers)
                    alpha = 0.3f;

                float ang = token.Rotation * (MathF.PI / 180f); // deg -> rad
                float c = MathF.Cos(ang), s = MathF.Sin(ang);

                // outer triangle (points defined in local/map space pointing to the right)
                float r1 = r;
                Vector2<float> a1 = posScreen + Rotate(new Vector2<float>(0f, -r1), c, s);
                Vector2<float> b1 = posScreen + Rotate(new Vector2<float>(r1 * 1.3f, 0f), c, s);
                Vector2<float> c1 = posScreen + Rotate(new Vector2<float>(0f, r1), c, s);
                _dm_window.Shapes.DrawColor = Color.BLACK with { A = alpha };
                _dm_window.Shapes.FillTriangle(a1, b1, c1);

                // inner triangle
                float r2 = r * 0.98f;
                Vector2<float> a2 = posScreen + Rotate(new Vector2<float>(0f, -r2), c, s);
                Vector2<float> b2 = posScreen + Rotate(new Vector2<float>(r2 * 1.25f, 0f), c, s);
                Vector2<float> c2 = posScreen + Rotate(new Vector2<float>(0f, r2), c, s);
                _dm_window.Shapes.DrawColor = token.Color with { A = alpha };
                _dm_window.Shapes.FillTriangle(a2, b2, c2);

                // circles
                _dm_window.Shapes.DrawColor = Color.BLACK with { A = alpha };
                _dm_window.Shapes.FillCircle(posScreen, r);
                _dm_window.Shapes.DrawColor = token.StatusColor with { A = alpha };
                _dm_window.Shapes.FillCircle(posScreen, r * 0.98f);
                _dm_window.Shapes.DrawColor = Color.BLACK with { A = alpha };
                _dm_window.Shapes.FillCircle(posScreen, r * 0.90f);
                _dm_window.Shapes.DrawColor = token.Color with { A = alpha };
                _dm_window.Shapes.FillCircle(posScreen, r * 0.8f);
                Color textColor = token.Color.GetBrightness() < 0.5f ? Color.WHITE : Color.BLACK;
                _dm_window.Shapes.DrawColor = textColor;
                _dm_window.Shapes.TextFormat = new TextFormat()
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Middle,
                    FontSize = token.FontSize,
                };
                _dm_window.Shapes.DrawText(posScreen, token.Name);
            }
            
            if (_measuringDistance)
            {
                _dm_window.Shapes.DrawColor = Color.YELLOW;
                _dm_window.Shapes.LineWidth = 10;
                _dm_window.Shapes.DrawLine((Vector2<float>)_lastMousePos, (Vector2<float>)_mousePos);
                _dm_window.Shapes.DrawColor = Color.DARK_GRAY with { A = 0.5f };
                double dist = (_mousePos - _lastMousePos).Length;
                _dm_window.Shapes.FillCircle((Vector2<float>)_lastMousePos, (float)dist);
                _dm_window.Shapes.DrawColor = Color.ORANGE;
                _dm_window.Shapes.DrawCircle((Vector2<float>)_lastMousePos, (float)dist);

                double defaultTokenSize = 2.0f * _zoom * Token.globalScale; // represents 5 feet
                double distanceInFeet = dist / defaultTokenSize * 5.0;
                double distanceInMeters = distanceInFeet * 0.3048;
                _dm_window.Shapes.TextFormat = new TextFormat()
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    FontSize = 24,
                };
                _dm_window.Shapes.DrawColor = Color.BLACK;
                _dm_window.Shapes.DrawText((Vector2)_mousePos, $"{distanceInFeet:0.##} ft\n{distanceInMeters:0.##} m");
            }
        }
        else
        {
            _dm_window.Shapes.TextFormat = new TextFormat()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Middle,
                FontSize = 48,
            };
            _dm_window.Shapes.DrawColor = Color.WHITE;
            _dm_window.Shapes.DrawText(_dm_window.Size / 2, "Drop a map image file here");
        }
        _dm_window.Show();
    }

    private static void RenderPlayer(double dt, SKCanvas canvas, GL gl)
    {
        _player_window.Clear(Color.DARK_GRAY);

        if (_map_image != null)
        {
            float zDM = _zoom;                        // DM zoom (controller)
            Vector2<double> offDM = _offset;          // DM offset (controller)
            var sizeDM = _dm_window.Size;             // Vector2<int>

            // Player (reacts)
            var sizePL = _player_window.Size;         // Vector2<int>

            // --- Policy: FIT (recommended) ---
            // Ensures the entire DM world-rect is visible on the player.
            float ratioW = (float)sizePL.X / sizeDM.X;
            float ratioH = (float)sizePL.Y / sizeDM.Y;
            float zPL = zDM * MathF.Min(ratioW, ratioH);

            // Keep the same world center: offset scales with zoom
            Vector2<double> offPL = offDM * (double)(zPL / zDM);

            // Store/apply to your player renderer variables
            _player_zoom = zPL;
            _player_offset = offPL;

            Vector2<float> n = _map_image.NaturalSize;
            float z = _player_zoom;
            Vector2 imageSize = n * z;
            _player_window.Shapes.DrawImage(_map_image, _player_window.Size / 2 - (Vector2<int>)imageSize / 2 + (Vector2<int>)_player_offset, imageSize);

            Vector2<double> tl = TopLeft(_player_window.Size, n, z, _player_offset);
            foreach (var token in TokenFactory.Tokens)
            {
                if (!token.VisibleToPlayers)
                    continue;
                // 3.1 Position: stays constant on the map
                Vector2<float> posScreen = (Vector2<float>)MapToScreen(token.Position, tl, z);

                float r = token.Size * z * Token.globalScale;

                float ang = token.Rotation * (MathF.PI / 180f); // deg -> rad
                float c = MathF.Cos(ang), s = MathF.Sin(ang);

                // outer triangle (points defined in local/map space pointing to the right)
                float r1 = r;
                Vector2<float> a1 = posScreen + Rotate(new Vector2<float>(0f, -r1), c, s);
                Vector2<float> b1 = posScreen + Rotate(new Vector2<float>(r1 * 1.3f, 0f), c, s);
                Vector2<float> c1 = posScreen + Rotate(new Vector2<float>(0f, r1), c, s);
                _player_window.Shapes.DrawColor = Color.BLACK;
                _player_window.Shapes.FillTriangle(a1, b1, c1);

                // inner triangle
                float r2 = r * 0.98f;
                Vector2<float> a2 = posScreen + Rotate(new Vector2<float>(0f, -r2), c, s);
                Vector2<float> b2 = posScreen + Rotate(new Vector2<float>(r2 * 1.25f, 0f), c, s);
                Vector2<float> c2 = posScreen + Rotate(new Vector2<float>(0f, r2), c, s);
                _player_window.Shapes.DrawColor = token.Color;
                _player_window.Shapes.FillTriangle(a2, b2, c2);

                // circles
                _player_window.Shapes.DrawColor = Color.BLACK;
                _player_window.Shapes.FillCircle(posScreen, r);
                _player_window.Shapes.DrawColor = token.StatusColor;
                _player_window.Shapes.FillCircle(posScreen, r * 0.98f);
                _player_window.Shapes.DrawColor = Color.BLACK;
                _player_window.Shapes.FillCircle(posScreen, r * 0.90f);
                _player_window.Shapes.DrawColor = token.Color;
                _player_window.Shapes.FillCircle(posScreen, r * 0.80f);
                Color textColor = token.Color.GetBrightness() < 0.5f ? Color.WHITE : Color.BLACK;
                _player_window.Shapes.DrawColor = textColor;
                _player_window.Shapes.TextFormat = new TextFormat()
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Middle,
                    FontSize = token.FontSize,
                };
                _player_window.Shapes.DrawText(posScreen, token.Name);
            }

            if (_measuringDistance)
            {
                var localMousePos = DmScreenToPlayerScreen(_mousePos);
                var localLastMousePos = DmScreenToPlayerScreen(_lastMousePos);

                _player_window.Shapes.DrawColor = Color.YELLOW;
                _player_window.Shapes.LineWidth = 10;
                _player_window.Shapes.DrawLine((Vector2<float>)localLastMousePos, (Vector2<float>)localMousePos);
                _player_window.Shapes.DrawColor = Color.DARK_GRAY with { A = 0.5f };
                double dist = (localMousePos - localLastMousePos).Length;
                _player_window.Shapes.FillCircle((Vector2<float>)localLastMousePos, (float)dist);
                _player_window.Shapes.DrawColor = Color.ORANGE;
                _player_window.Shapes.DrawCircle((Vector2<float>)localLastMousePos, (float)dist);

                double defaultTokenSize = 2.0f * _zoom * Token.globalScale; // represents 5 feet
                double distanceInFeet = dist / defaultTokenSize * 5.0;
                double distanceInMeters = distanceInFeet * 0.3048;
                _player_window.Shapes.TextFormat = new TextFormat()
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    FontSize = 24,
                };
                _player_window.Shapes.DrawColor = Color.BLACK;
                _player_window.Shapes.DrawText((Vector2)localMousePos, $"{distanceInFeet:0.##} ft\n{distanceInMeters:0.##} m");
            }
        }
        else
        {
            _player_window.Shapes.TextFormat = new TextFormat()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Middle,
                FontSize = 48,
            };
            _player_window.Shapes.DrawColor = Color.WHITE;
            _player_window.Shapes.DrawText(_player_window.Size / 2, "Waiting for DM to load a map");
        }
        _player_window.Show();
    }

    private static void MouseButtonDown(MouseButton button)
    {
        switch (button)
        {
            case MouseButton.Right:
                _isPanning = true;
                _lastMousePos = _dm_window.GetCursorPos();
                break;
            case MouseButton.Left:
                if (_dm_window.IsKeyDown(Keys.M))
                {
                    _measuringDistance = true;
                    _lastMousePos = _dm_window.GetCursorPos();
                } else if (TokenFactory.MouseOverToken != null)
                {
                    _draggingToken = TokenFactory.MouseOverToken;
                    _lastMousePos = _dm_window.GetCursorPos();
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
                break;
        }
    }

    private static void MouseMove(Vector2<double> position)
    {
        _mousePos = position;

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
                _player_window.Decorated = !_player_window.Decorated;
                int offsetY = 30;
                if (!_player_window.Decorated)
                {
                    _player_window.Size += new Vector2<int>(0, offsetY);
                    _player_window.Position -= new Vector2<int>(0, offsetY);
                    return;
                }
                _player_window.Size -= new Vector2<int>(0, offsetY);
                _player_window.Position += new Vector2<int>(0, offsetY);
                break;
        }
    }

    private static void KeyDownDM(Keys key)
    {
        switch (key)
        {
            case Keys.T:
                _tokenScaling = true;
                break;

            case Keys.F11:
                _dm_window.Decorated = !_dm_window.Decorated;
                int offsetY = 30;
                if (!_dm_window.Decorated)
                {
                    _dm_window.Size += new Vector2<int>(0, offsetY);
                    _dm_window.Position -= new Vector2<int>(0, offsetY);
                    return;
                }
                _dm_window.Size -= new Vector2<int>(0, offsetY);
                _dm_window.Position += new Vector2<int>(0, offsetY);
                break;
            case Keys.F12:
                _dm_window.AlwaysOnTop = !_dm_window.AlwaysOnTop;
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
                break;
            case Keys.N:
                Vector2<double> mapPos = ScreenToMap(_mousePos, TopLeft(_dm_window.Size, _map_image?.NaturalSize ?? Vector2<float>.Zero, _zoom, _offset), _zoom);
                TokenFactory.CreateToken(mapPos, Color.WHITE, 1.0f, false);
                break;
            case Keys.V:
                if (_dm_window.IsKeyDown(Keys.ControlLeft))
                    TryPasteFromClipboard();
                break;
            case Keys.ControlLeft:
                _tokenFontScaling = true;
                break;
           }
    }


    private static void KeyUpDM(Keys key)
    {
        if (key == Keys.T)
        {
            _tokenScaling = false;
        }
        if (key == Keys.ControlLeft)
        {
            _tokenFontScaling = false;
        }
    }

    private static void TryPasteFromClipboard()
    {
        if (TokenFactory.MouseOverToken == null)
            return;

        if (_dm_window.Clipboard == null)
            return;

        string text = _dm_window.Clipboard;
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
            Token.globalScale *= factor;
            return;
        }

        if (_map_image == null)
            return;

        // 1) Compute new zoom (with clamp)
        float oldZ = _zoom;
        float newZ = oldZ * MathF.Pow(1.1f, (float)scroll.Y);
        newZ = GenericMath.Clamp(newZ, 0.01f, 10f);

        if (MathF.Abs(newZ - oldZ) < 1e-9f)
            return; // no effective change

        // 2) Keep the point under the cursor fixed
        Vector2<double> m = _mousePos; // mouse in window coords
        Vector2<float> n = _map_image.NaturalSize; // image natural size (pixels)
        Vector2<double> c = _dm_window.Size / 2.0;

        // current top-left on screen
        Vector2<double> tl = c - (Vector2<double>)(n * oldZ) / 2.0 + _offset;

        // image-space point under cursor (in natural pixels)
        Vector2<double> p = (m - tl) / (double)oldZ;

        // new offset so that p stays under m after zoom
        Vector2<double> newOffset =
            m - p * (double)newZ - c + (Vector2<double>)(n * newZ) / 2.0;

        _zoom = newZ;
        _offset = newOffset;
    }

    private static void AdjustToFitRatio(Vector2<int> size)
    {
        float a = size.X * size.Y;  // current area

        // Target aspect = W/H (from player window)
        float r = _player_window.AspectRatio;

        // Only adjust if aspect differs meaningfully
        if (MathF.Abs(_dm_window.AspectRatio - r) > 0.01)
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

            _dm_window.Size = new Vector2<int>(best.W, best.H);
        }
    }

    static Vector2<double> TopLeft(Vector2<int> winSize, Vector2<float> n, float z, Vector2<double> o)
    {
        Vector2<double> c = winSize / 2.0;
        return c - (Vector2<double>)(n * z) / 2.0 + o; // TL of the image on screen
    }

    static Vector2<double> MapToScreen(Vector2<double> mapPx, Vector2<double> tl, float z)
    {
        return tl + mapPx * (double)z;
    }

    static Vector2<double> ScreenToMap(Vector2<double> screenPx, Vector2<double> tl, float z)
    {
        return (screenPx - tl) / (double)z;
    }

    static Vector2<float> Rotate(Vector2<float> v, float cosA, float sinA)
    {
        // x' = x*cos - y*sin ; y' = x*sin + y*cos
        return new Vector2<float>(v.X * cosA - v.Y * sinA, v.X * sinA + v.Y * cosA);
    }

    static Vector2<double> DmScreenToPlayerScreen(Vector2<double> dmScreen)
    {
        if (_map_image == null)
            throw new InvalidOperationException("No map loaded");
        Vector2<float> n = _map_image.NaturalSize;

        // DM: screen → map
        Vector2<double> tl_dm = TopLeft(_dm_window.Size, n, _zoom, _offset);
        Vector2<double> mapPx = (Vector2<double>)((dmScreen - tl_dm) / _zoom);

        // Player: map → screen
        Vector2<double> tl_pl = TopLeft(_player_window.Size, n, _player_zoom, _player_offset);
        Vector2<double> plScreen = tl_pl + mapPx * (double)_player_zoom;

        return plScreen;
    }
}
