using System.Runtime.InteropServices;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfGpu;

// Keep in sync with GLSL struct ColorRule (std430).
// Size ends up 48 bytes (16-aligned).
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct GpuSdfColorRule
{
    public int Mode;
    public int Pad0, Pad1, Pad2;

    public float R, G, B, A;

    public float A0;      // threshold A
    public float B0;      // threshold B
    public float Feather; // transition width
    public float Pad3;
}