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
        var res = Gl2DResources.Get((nint)window);

        gl.UseProgram(res.Program2D);
        gl.Uniform2(res.LocViewport, (float)fbW, (float)fbH);

        uint vao = gl.GenVertexArray();
        uint vbo = gl.GenBuffer();

        gl.BindVertexArray(vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);

        fixed (float* ptr = interleavedPos2Color4)
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(interleavedPos2Color4.Length * sizeof(float)),
                ptr,
                BufferUsageARB.StreamDraw);
        }

        uint stride = (uint)((2 + 4) * sizeof(float));
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);

        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(float)));

        gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)vertexCount);

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        gl.BindVertexArray(0);

        gl.DeleteBuffer(vbo);
        gl.DeleteVertexArray(vao);

        gl.UseProgram(0);
    }
}