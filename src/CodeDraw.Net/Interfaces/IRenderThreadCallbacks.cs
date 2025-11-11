using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Interfaces;

public interface IRenderThreadCallbacks {
    void OnLoaded(GL gl, Glfw glfw, nint window);
    void OnPresented(long token);
}
