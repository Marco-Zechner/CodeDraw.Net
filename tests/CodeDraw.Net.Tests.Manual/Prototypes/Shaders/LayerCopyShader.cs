namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;

/// <summary>
/// Public shader descriptor for copying one layer into another with a custom effect.
/// This is file-based and uses the global FileCache + ShaderStore system.
/// </summary>
public sealed class LayerCopyShader
{
    internal ShaderKey Key { get; }

    private LayerCopyShader(ShaderKey key) => Key = key;

    /// <summary>
    /// Create shader from absolute base path(s) without extension.
    /// Example: "C:\...\rect" means "rect.vert" + "rect.frag".
    /// </summary>
    public static LayerCopyShader FromBaseAbs(string vertBaseAbs, string? fragBaseAbs = null)
        => new(new ShaderKey(Path.GetFullPath(vertBaseAbs), Path.GetFullPath(fragBaseAbs ?? vertBaseAbs)));

    /// <summary>Load from engine shader folder (assembly/bin resolved) using ShaderPath.Engine(name).</summary>
    public static LayerCopyShader Engine(string name, string folder = "resources/shaders") => FromBaseAbs(ShaderPath.Engine(name, folder));

    /// <summary>Load from app shader folder (AppContext.BaseDirectory) using ShaderPath.App(name).</summary>
    public static LayerCopyShader App(string name, string folder = "resources/shaders") => FromBaseAbs(ShaderPath.App(name, folder));

    /// <summary>Load from csproj dev folder using ShaderPath.CsProject(name).</summary>
    public static LayerCopyShader CsProject(string name, string folder = "resources/shaders") => FromBaseAbs(ShaderPath.CsProject(name, folder));

    /// <summary>Load from git root dev folder using ShaderPath.GitRoot(name).</summary>
    public static LayerCopyShader GitRoot(string name, string folder = "resources/shaders") => FromBaseAbs(ShaderPath.GitRoot(name, folder));

    public override string ToString() => $"LayerCopyShader({Key})";
}