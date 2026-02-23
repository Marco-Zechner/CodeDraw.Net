

using System.Runtime.InteropServices;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfGpu;

// std430 alignment: keep everything 16-byte friendly.
// int4 (16 bytes)
// vec4 color (16)
// float a,b,feather,step (16)
// vec4 color2 (16)
// => total 64 bytes
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct GpuSdfColorRule
{
    public int Mode;
    public int Pad0, Pad1, Pad2;

    // colorA
    public float R, G, B, A;

    // a,b,feather,step
    public float A0;      // sdMin (or threshold A for other modes)
    public float B0;      // sdMax (or threshold B for other modes)
    public float Feather; // transition width / window feather
    public float Step;    // px step size (only for stepped gradient)

    // colorB (only for gradient modes)
    public float R2, G2, B2, A2;
}