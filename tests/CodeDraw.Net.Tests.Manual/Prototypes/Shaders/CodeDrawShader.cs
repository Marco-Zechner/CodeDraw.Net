namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;

/// <summary>
/// User-facing shader handle. No GL. No Dispose.
/// Can represent either fixed source, or a program loaded from a ShaderStore.
/// </summary>
public sealed class CodeDrawShader
{
    internal enum Kind
    {
        FIXED_SOURCES,
        STORE_PROGRAM
    }

    internal Kind ShaderKind { get; }

    public string Name { get; }

    // Fixed sources
    internal string? FixedVs { get; }
    internal string? FixedFs { get; }

    // Store-backed
    internal ShaderStore? Store { get; }
    internal string? StoreProgramName { get; } // logical name inside the store (e.g. "myShader")

    // 1) inline sources
    public CodeDrawShader(string name, string vertSource, string fragSource)
    {
        Name = name;
        ShaderKind = Kind.FIXED_SOURCES;
        FixedVs = vertSource;
        FixedFs = fragSource;
    }

    // 2) from baseclass
    public CodeDrawShader(CodeDrawShaderBase src)
    {
        Name = src.Name;
        ShaderKind = Kind.FIXED_SOURCES;
        FixedVs = src.VertexSource;
        FixedFs = src.FragmentSource;
    }

    // internal ctor for store-backed shader
    internal CodeDrawShader(ShaderStore store, string programName, string displayName)
    {
        Store = store;
        StoreProgramName = programName;
        Name = displayName;
        ShaderKind = Kind.STORE_PROGRAM;
    }

    public static implicit operator CodeDrawShader(CodeDrawShaderBase src) => new(src);
}