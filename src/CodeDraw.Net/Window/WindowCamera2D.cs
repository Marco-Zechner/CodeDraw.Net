using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Window;

public enum CameraResizePolicy
{
    Manual,            // never touch anything automatically (pure matrix / user-controlled)
    KeepScale,         // keep world scale constant -> view grows/shrinks with window
    KeepViewSize,      // keep view size constant -> zoom changes when window changes
}

public sealed class WindowCamera2D
{
    // Canonical mapping: window px -> layer px
    public Matrix3X3 WindowToLayer
    {
        get => _w2l;
        set
        {
            _w2l = value;
            _invDirty = true;
            _matrixLocked = true; // user took over
        }
    }

    public Matrix3X3 LayerToWindow { get { EnsureInverse(); return _l2w; } }

    // Param mode knobs (optional)
    public Vector2 PositionGlobal { get; set; } = new(0, 0);
    public Vector2 PositionLocal  { get; set; } = new(0, 0);
    public Vector2 ViewSizeLayer  { get; set; } = new(0, 0); // if 0 => "auto"
    public float RotationDegCw    { get; set; } = 0f;

    public CameraResizePolicy ResizePolicy { get; set; } = CameraResizePolicy.KeepScale;

    // Let user explicitly switch back to param mode:
    public void UseParams()
    {
        _matrixLocked = false;
    }

    // Called by window when present-mode is Camera.
    public void OnWindowResized(float oldW, float oldH, float newW, float newH)
    {
        if (_matrixLocked) return;
        if (ResizePolicy == CameraResizePolicy.Manual) return;
        if (oldW <= 0 || oldH <= 0 || newW <= 0 || newH <= 0) return;

        switch (ResizePolicy)
        {
            case CameraResizePolicy.KeepScale:
            {
                // Keep sx=viewX/windowW constant => viewX scales with windowW.
                // Only if ViewSizeLayer is not "auto"; if it's auto, it naturally follows newW/newH anyway.
                if (ViewSizeLayer.X > 0) ViewSizeLayer = ViewSizeLayer.WithX(ViewSizeLayer.X * (newW / oldW));
                if (ViewSizeLayer.Y > 0) ViewSizeLayer = ViewSizeLayer.WithY(ViewSizeLayer.Y * (newH / oldH));
                break;
            }

            case CameraResizePolicy.KeepViewSize:
            {
                // do nothing to ViewSizeLayer; just rebuild with new window size => zoom changes
                break;
            }
        }

        Rebuild(newW, newH);
    }

    public void Rebuild(float windowW, float windowH)
    {
        if (windowW <= 0 || windowH <= 0)
            throw new ArgumentOutOfRangeException(nameof(windowW), "Window size must be positive.");

        var view = ViewSizeLayer;
        if (view.X <= 0) view = view.WithX(windowW);
        if (view.Y <= 0) view = view.WithY(windowH);

        var pivotWin = new Vector2(windowW * 0.5f, windowH * 0.5f);

        float sx = view.X / windowW;
        float sy = view.Y / windowH;

        float rotCCW = -RotationDegCw;

        var TnegPivot = Matrix3X3.CreateTranslation(-pivotWin.X, -pivotWin.Y);
        var S         = Matrix3X3.CreateScale(sx, sy);
        var Tlocal    = Matrix3X3.CreateTranslation(PositionLocal.X, PositionLocal.Y);
        var R         = Matrix3X3.CreateRotation(rotCCW);
        var Tglobal   = Matrix3X3.CreateTranslation(PositionGlobal.X, PositionGlobal.Y);

        _w2l = Tglobal * R * Tlocal * S * TnegPivot;
        _invDirty = true;
        _matrixLocked = false; // we’re in param-land now
    }

    public Vector2 WindowToLayerPoint(Vector2 windowPx) => Matrix3X3.TransformAffine(_w2l, windowPx);
    public Vector2 LayerToWindowPoint(Vector2 layerPx) { EnsureInverse(); return Matrix3X3.TransformAffine(_l2w, layerPx); }

    private Matrix3X3 _w2l = Matrix3X3.Identity;
    private Matrix3X3 _l2w = Matrix3X3.Identity;
    private bool _invDirty = true;
    private bool _matrixLocked = false;

    private void EnsureInverse()
    {
        if (!_invDirty) return;
        if (!Matrix3X3.TryInvert(_w2l, out _l2w))
            throw new InvalidOperationException("Camera matrix is not invertible.");
        _invDirty = false;
    }
}
