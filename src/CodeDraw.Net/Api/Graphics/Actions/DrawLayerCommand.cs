using MarcoZechner.CodeDrawDotNet.Interfaces;

namespace MarcoZechner.CodeDrawDotNet.Api.Graphics.Actions;

internal sealed class DrawLayerCommand(ILayerHandle layer, bool premultiplyForCompositor = false) : IRenderCommand
{
    public ILayerHandle Layer => layer;
    public bool Premultiply => premultiplyForCompositor;
}