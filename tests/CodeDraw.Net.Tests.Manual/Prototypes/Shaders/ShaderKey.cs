namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;

/// <summary>
/// Identifies a shader program by its vertex+fragment "base paths" (absolute, without extension).
/// Example: "C:\...\rect" + ".vert" / ".frag".
/// </summary>
public readonly record struct ShaderKey(string VertBaseAbs, string FragBaseAbs)
{
    public string VertPath => VertBaseAbs + ".vert";
    public string FragPath => FragBaseAbs + ".frag";

    public override string ToString()
    {
        var v = Path.GetFileName(VertBaseAbs);
        var f = Path.GetFileName(FragBaseAbs);
        return string.Equals(v, f, StringComparison.OrdinalIgnoreCase) ? v : $"{v}__{f}";
    }
}