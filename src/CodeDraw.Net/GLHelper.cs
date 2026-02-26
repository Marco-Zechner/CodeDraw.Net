using System.Runtime.CompilerServices;
using MarcoZechner.MathDotNet;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet;

public static unsafe class GlHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Uniform1(GL gl, int loc, int val)
        => gl.Uniform1(loc, val);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Uniform1F(GL gl, int loc, float val)
        => gl.Uniform1(loc, val);    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Uniform2F(GL gl, int loc, float x, float y)
        => gl.Uniform2(loc, x, y);   
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Uniform3F(GL gl, int loc, float x, float y, float z)
        => gl.Uniform3(loc, x, y, z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Uniform4F(GL gl, int loc, float x, float y, float z, float w)
        => gl.Uniform4(loc, x, y, z, w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void UniformMat3(GL gl, int loc, in Matrix3x3 m, bool transpose = true)
    {
        // Column-major layout for GLSL mat3.
        // c# Matrix3x3 is row-major, so we transpose it by swapping indices.
        Span<float> tmp = [
            m.M11, m.M12, m.M13,
            m.M21, m.M22, m.M23,
            m.M31, m.M32, m.M33,
        ];

        fixed (float* p = tmp)
            gl.UniformMatrix3(loc, 1, transpose, p);
    }
}