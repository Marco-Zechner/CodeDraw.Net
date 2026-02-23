using System.Runtime.InteropServices;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfGpu;

// Keep in sync with GLSL struct Material (std430).
// Size ends up 64 bytes (16-aligned) which is std430-friendly.
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct GpuSdfMaterial
{
    // vec4 fillColor
    public float FillR, FillG, FillB, FillA;

    // vec4 strokeColor
    public float StrokeR, StrokeG, StrokeB, StrokeA;

    public float StrokeThickness;
    public float FeatherPx;
    public int HasFill;
    public int HasStroke;

    public int RuleFirst;
    public int RuleCount;

    public int Pad0;
    public int Pad1;
}