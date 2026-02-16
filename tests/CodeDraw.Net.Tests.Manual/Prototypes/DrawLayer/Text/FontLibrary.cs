using SharpFont;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer.Text;

public sealed class FontLibrary : IDisposable
{
    public Library Lib { get; } = new();

    public void Dispose() => Lib.Dispose();
}