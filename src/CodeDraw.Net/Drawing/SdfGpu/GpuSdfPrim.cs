using System.Runtime.InteropServices;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfGpu;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct GpuSdfPrim
{
    public int Type;
    public int Op;
    public int MatId;
    public int Pad1;

    // mat4 worldToLocal (column-major in GLSL). 16 floats.
    public float W2L00, W2L10, W2L20, W2L30; // column 0
    public float W2L01, W2L11, W2L21, W2L31; // column 1
    public float W2L02, W2L12, W2L22, W2L32; // column 2
    public float W2L03, W2L13, W2L23, W2L33; // column 3

    // Generic params (vec4 aligned)
    public float P0x, P0y, P0z, P0w;
    public float P1x, P1y, P1z, P1w;

    public float K;
    public float Pad2, Pad3, Pad4;

    // ReSharper disable once InconsistentNaming
    public void SetWorldToLocalFromAffine3x3(in Matrix3x3 w2L)
    {
        // Robust: infer columns by transforming basis points.
        var p00 = Matrix3x3.TransformAffine(w2L, new Vector2(0f, 0f));
        var p10 = Matrix3x3.TransformAffine(w2L, new Vector2(1f, 0f));
        var p01 = Matrix3x3.TransformAffine(w2L, new Vector2(0f, 1f));

        var xAxis = p10 - p00;
        var yAxis = p01 - p00;
        var t = p00;

        W2L00 = xAxis.X; W2L10 = xAxis.Y; W2L20 = 0f; W2L30 = 0f;
        W2L01 = yAxis.X; W2L11 = yAxis.Y; W2L21 = 0f; W2L31 = 0f;
        W2L02 = 0f;      W2L12 = 0f;      W2L22 = 1f; W2L32 = 0f;
        W2L03 = t.X;     W2L13 = t.Y;     W2L23 = 0f; W2L33 = 1f;
    }
}