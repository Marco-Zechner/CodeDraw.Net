namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;

public abstract class CodeDrawShaderBase
{
    public abstract string Name { get; }
    public abstract string VertexSource { get; }
    public abstract string FragmentSource { get; }
}