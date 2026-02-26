using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Images;

public sealed class CodeDrawImage
{
    internal ImageKey Key { get; }

    private CodeDrawImage(ImageKey key) => Key = key;

    public string AbsPath => Key.AbsPath;

    public bool Exists => File.Exists(Key.AbsPath);

    /// <summary>True once the image has been decoded at least once by ImageStore.</summary>
    public bool HasMeta => ImageStore.TryGetMeta(Key, out _, out _);

    public bool TryGetSize(out int w, out int h)
        => ImageStore.TryGetMeta(Key, out w, out h);

    public int Width => TryGetSize(out var w, out _) ? w : 0;
    public int Height => TryGetSize(out _, out var h) ? h : 0;
    public Vector2<int> Size => TryGetSize(out var w, out var h) ? new Vector2<int>(w, h) : Vector2<int>.Zero;

    public float AspectRatio
        => Height == 0 ? 0f : (float)Width / Height;

    public static CodeDrawImage FromFileAbs(string fileAbs)
        => new(new ImageKey(Path.GetFullPath(fileAbs)));

    public static CodeDrawImage Engine(string nameWithExt, string folder = "resources/images")
        => FromFileAbs(ImagePath.Engine(nameWithExt, folder));

    public static CodeDrawImage App(string nameWithExt, string folder = "resources/images")
        => FromFileAbs(ImagePath.App(nameWithExt, folder));

    public static CodeDrawImage CsProject(string nameWithExt, string folder = "resources/images")
        => FromFileAbs(ImagePath.CsProject(nameWithExt, folder));

    public static CodeDrawImage GitRoot(string nameWithExt, string folder = "resources/images")
        => FromFileAbs(ImagePath.GitRoot(nameWithExt, folder));

    public override string ToString()
        => TryGetSize(out var w, out var h)
            ? $"Image({w}x{h} '{Key.AbsPath}')"
            : $"Image(unknown '{Key.AbsPath}')";
}