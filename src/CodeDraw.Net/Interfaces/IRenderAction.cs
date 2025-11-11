using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Interfaces;

public interface IRenderAction
{
    /// <summary>Executes on the window’s render thread.</summary>
    unsafe void Execute(GL gl, Glfw glfw, WindowHandle* window, int fbW, int fbH);
}