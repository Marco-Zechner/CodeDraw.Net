namespace MarcoZechner.CodeDrawDotNet.Shaders;

/// <summary>
/// Public shader descriptor backed by ShaderKey (vertex+fragment base paths, absolute).
/// Uses FileCache + ShaderStore (same pipeline as internal engine shaders).
///
/// Works for:
/// - Layer copy shaders (DrawLayer with custom effect)
/// - Custom rect shaders (CustomDrawRect), later
/// </summary>
public sealed class CodeDrawShader
{
    internal ShaderKey Key { get; }

    private CodeDrawShader(ShaderKey key) => Key = key;

    /// <summary>
    /// Create shader from absolute base path(s) without extension.
    /// Example: "C:\...\rect" means "rect.vert" + "rect.frag".
    /// </summary>
    public static CodeDrawShader FromBaseAbs(string vertBaseAbs, string? fragBaseAbs = null)
        => new(new ShaderKey(Path.GetFullPath(vertBaseAbs), Path.GetFullPath(fragBaseAbs ?? vertBaseAbs)));

    /// <summary>Load from engine shader folder (assembly/bin resolved) using ShaderPath.Engine(name).</summary>
    public static CodeDrawShader Engine(string name, string folder = "resources/shaders")
        => FromBaseAbs(ShaderPath.Engine(name, folder));

    public static CodeDrawShader Engine((string vertName, string fragName) shader, string folder = "resources/shaders")
        => FromBaseAbs(ShaderPath.Engine(shader.vertName, folder), ShaderPath.Engine(shader.fragName, folder));
    
    /// <summary>Load from app shader folder (AppContext.BaseDirectory) using ShaderPath.App(name).</summary>
    public static CodeDrawShader App(string name, string folder = "resources/shaders")
        => FromBaseAbs(ShaderPath.App(name, folder));
    
    public static CodeDrawShader App((string vertName, string fragName) shader, string folder = "resources/shaders")
        => FromBaseAbs(ShaderPath.App(shader.vertName, folder), ShaderPath.App(shader.fragName, folder));

    /// <summary>Load from csproj dev folder using ShaderPath.CsProject(name).</summary>
    public static CodeDrawShader CsProject(string name, string folder = "resources/shaders")
        => FromBaseAbs(ShaderPath.CsProject(name, folder));
    
    public static CodeDrawShader CsProject((string vertName, string fragName) shader, string folder = "resources/shaders")
        => FromBaseAbs(ShaderPath.CsProject(shader.vertName, folder), ShaderPath.CsProject(shader.fragName, folder));

    /// <summary>Load from git root dev folder using ShaderPath.GitRoot(name).</summary>
    public static CodeDrawShader GitRoot(string name, string folder = "resources/shaders")
        => FromBaseAbs(ShaderPath.GitRoot(name, folder));
    
    public static CodeDrawShader GitRoot((string vertName, string fragName) shader, string folder = "resources/shaders")
        => FromBaseAbs(ShaderPath.GitRoot(shader.vertName, folder), ShaderPath.GitRoot(shader.fragName, folder));

    public override string ToString() => $"CustomShader({Key})";
}
