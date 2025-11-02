using MarcoZechner.CodeDrawDotNet.Engine.Abstractions;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Api;

public unsafe class CodeDrawWindow<TRenderer> : CodeDrawWindowBase
    where TRenderer : AbstractWindowRenderer, IHostBootstrap<TRenderer>, new()
{
    public CodeDrawWindow(string title) : base(title)
    {
        TRenderer.EnsureHost();
    }

    protected override AbstractWindowRenderer CreateRenderer(WindowHandle* native, string title)
    {
        var host = CodeDrawRuntime.Host; 
        var r = (AbstractWindowRenderer)Activator.CreateInstance(
                            typeof(TRenderer),
                            args: [host, (nint)native, title])!;
        return r;
    }
}
