using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Engine;

public unsafe interface IRenderAction
{
    /// <summary>Executes on the window’s render thread.</summary>
    void Execute(GL gl, Glfw glfw, WindowHandle* window, int fbW, int fbH);
}