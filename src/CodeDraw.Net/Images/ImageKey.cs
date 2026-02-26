namespace MarcoZechner.CodeDrawDotNet.Images;

public readonly record struct ImageKey(string AbsPath)
{
    public override string ToString() => AbsPath;
}