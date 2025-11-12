using MarcoZechner.CodeDrawDotNet.Interfaces;

namespace MarcoZechner.CodeDrawDotNet.Api;

public unsafe class CodeDrawWindow<TRenderer>(string title) : CodeDrawWindowBase(title)
    where TRenderer : IAttachableRenderer, new()
{
    protected override IAttachableRenderer CreateRenderer() => new TRenderer();
}
