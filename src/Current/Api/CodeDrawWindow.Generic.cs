using MarcoZechner.CodeDrawDotNet.Engine.Implementations;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Api;

public unsafe class CodeDrawWindow<TRenderer>(string title) : CodeDrawWindowBase(title)
    where TRenderer : AbstractWindowRenderer, new()
{
    protected override AbstractWindowRenderer CreateRenderer(WindowHandle* native, string title)
    {
        var r = new TRenderer();
        r.Attach(native, title);
        return r;
    }
}
