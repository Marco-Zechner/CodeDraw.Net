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
    public static TransformScope PushTransformScope(
        this ICodeDrawTransformStack stack,
        in Matrix3x3 m,
        TransformCombine combine = TransformCombine.MultiplyCurrent)
    {
        stack.PushTransform(m, combine);
        return new TransformScope(stack);
    }

    public static TransformScope TranslateScope(this ICodeDrawTransformStack stack, float x, float y)
        => stack.PushTransformScope(Matrix3x3.CreateTranslationF(x, y));

    public static TransformScope ScaleScope(this ICodeDrawTransformStack stack, float sx, float sy)
        => stack.PushTransformScope(Matrix3x3.CreateScaleF(sx, sy));

    public static TransformScope RotateScopeDeg(this ICodeDrawTransformStack stack, float deg)
        => stack.PushTransformScope(Matrix3x3.CreateRotationF(deg));

    public static TransformScope RotateAroundScopeDeg(this ICodeDrawTransformStack stack, float px, float py, float deg)
    {
        // Current * (T(px,py) * R * T(-px,-py))
        var t0 = Matrix3x3.CreateTranslationF(px, py);
        var r  = Matrix3x3.CreateRotationF(deg);
        var t1 = Matrix3x3.CreateTranslationF(-px, -py);
        return stack.PushTransformScope(t0 * r * t1);
    }
}