using MarcoZechner.CodeDrawDotNet.Interfaces;

namespace MarcoZechner.CodeDrawDotNet.Api;

public unsafe class CodeDrawWindow<TRenderer> : CodeDrawWindowBase
    where TRenderer : IAttachableRenderer,  new()
{
    public CodeDrawWindow(string title) : base(title)
    {
        // TRenderer.EnsureHost(); // IHostBootstrap<TRenderer>, //todo do we need this?
    }

    protected override IAttachableRenderer CreateRenderer() => new TRenderer();
}
