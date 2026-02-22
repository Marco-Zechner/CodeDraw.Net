using System.Runtime.CompilerServices;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing;

public readonly struct TransformScope : IDisposable
{
    private readonly ICodeDrawTransformStack? _stack;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TransformScope(ICodeDrawTransformStack stack)
    {
        _stack = stack;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        _stack?.PopTransform();
    }
}

public static class TransformScopeExtensions
{
    public static TransformScope ScopePushTransform(
        this ICodeDrawTransformStack stack,
        in Matrix3x3 m,
        TransformCombine combine = TransformCombine.MultiplyCurrent)
    {
        stack.PushTransform(m, combine);
        return new TransformScope(stack);
    }

    public static TransformScope ScopeTranslate(this ICodeDrawTransformStack stack, float x, float y)
        => stack.ScopePushTransform(Matrix3x3.CreateTranslation(x, y));

    public static TransformScope ScopeScale(this ICodeDrawTransformStack stack, float sx, float sy)
        => stack.ScopePushTransform(Matrix3x3.CreateScale(sx, sy));
    
    public static TransformScope ScopeScaleAround(this ICodeDrawTransformStack stack, float px, float py, float sx, float sy)
    {
        // Current * (T(px,py) * S * T(-px,-py))
        var t0 = Matrix3x3.CreateTranslation(px, py);
        var s  = Matrix3x3.CreateScale(sx, sy);
        var t1 = Matrix3x3.CreateTranslation(-px, -py);
        return stack.ScopePushTransform(t0 * s * t1);
    }

    public static TransformScope ScopeRotate(this ICodeDrawTransformStack stack, float angle, AngleUnit angleUnit = AngleUnit.Degrees, RotationDirection direction = RotationDirection.Clockwise)
        => stack.ScopePushTransform(Matrix3x3.CreateRotation(angle, angleUnit, direction));

    public static TransformScope ScopeRotateAround(this ICodeDrawTransformStack stack, float px, float py, float angle, AngleUnit angleUnit = AngleUnit.Degrees, RotationDirection direction = RotationDirection.Clockwise)
    {
        // Current * (T(px,py) * R * T(-px,-py))
        var t0 = Matrix3x3.CreateTranslation(px, py);
        var r  = Matrix3x3.CreateRotation(angle, angleUnit, direction);
        var t1 = Matrix3x3.CreateTranslation(-px, -py);
        return stack.ScopePushTransform(t0 * r * t1);
    }
    
    public static TransformScope ScopeShear(this ICodeDrawTransformStack stack, float shx, float shy)
        => stack.ScopePushTransform(Matrix3x3.CreateShear(shx, shy));
    
    public static TransformScope ScopeShearAround(this ICodeDrawTransformStack stack, float px, float py, float shx, float shy)
    {
        // Current * (T(px,py) * Sh * T(-px,-py))
        var t0 = Matrix3x3.CreateTranslation(px, py);
        var sh = Matrix3x3.CreateShear(shx, shy);
        var t1 = Matrix3x3.CreateTranslation(-px, -py);
        return stack.ScopePushTransform(t0 * sh * t1);
    }
}