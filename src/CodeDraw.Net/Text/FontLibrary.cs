using SharpFont;

namespace MarcoZechner.CodeDrawDotNet.Text;

public sealed class FontLibrary : IDisposable
{
    public Library Lib { get; } = new();

    public void Dispose() => Lib.Dispose();
}