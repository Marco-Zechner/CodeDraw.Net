using MarcoZechner.CodeDrawDotNet.Interfaces;

namespace MarcoZechner.CodeDrawDotNet.Api;

public unsafe class CodeDrawWindow<TRenderer> : CodeDrawWindowBase
    where TRenderer : IAttachableRenderer, IHostBootstrap<TRenderer>, new()
{
    public CodeDrawWindow(string title) : base(title)
    {
        TRenderer.EnsureHost(); //todo do we need this? yes for now
    }

    protected override IAttachableRenderer CreateRenderer() => new TRenderer();
}
