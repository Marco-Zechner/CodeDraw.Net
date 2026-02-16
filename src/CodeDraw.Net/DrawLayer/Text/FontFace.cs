using SharpFont;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer.Text;

public sealed class FontFace : IDisposable
{
    public Face Face { get; }
    public string Path { get; }

    public FontFace(FontLibrary lib, string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(path);

        Path = System.IO.Path.GetFullPath(path);
        Face = new Face(lib.Lib, Path);
    }

    public void SetPixelSize(uint px)
    {
        Face.SetPixelSizes(0, px);
    }

    public void Dispose() => Face.Dispose();
}