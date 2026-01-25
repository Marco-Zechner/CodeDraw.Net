using Silk.NET.OpenGL;
using Silk.NET.GLFW;
using MarcoZechner.CodeDrawDotNet.Interfaces;
using MarcoZechner.CodeDrawDotNet.Engine;

namespace MarcoZechner.CodeDrawDotNet.Api.Graphics.Actions;

internal sealed unsafe class DrawTriangles2DAction(
    float[] interleavedPos2Color4, // [x,y,r,g,b,a] * N
    int vertexCount
) : IRenderAction
{
    public void Execute(GL gl, Glfw glfw, WindowHandle* window, int fbW, int fbH)
    {
        var windowKey = (nint)window;
        var (vao, vbo, prog, locViewport) = Gl2DResources.Get(windowKey);

        gl.UseProgram(prog);
        gl.Uniform2(locViewport, (float)fbW, (float)fbH);

        gl.BindVertexArray(vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);

        fixed (float* ptr = interleavedPos2Color4)
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(interleavedPos2Color4.Length * sizeof(float)),
                ptr,
                BufferUsageARB.StreamDraw);
        }

        gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)vertexCount);

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        gl.BindVertexArray(0);
        gl.UseProgram(0);
    }
}