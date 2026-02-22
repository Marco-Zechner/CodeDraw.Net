using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing;

public interface ICodeDrawTransformStack
{
    // Most common operations
    void PushTransform(in Matrix3x3 m, TransformCombine combine = TransformCombine.MultiplyCurrent);
    void PopTransform();

    // Optional, but nice for convenience/debugging
    Matrix3x3 CurrentTransform { get; }
}