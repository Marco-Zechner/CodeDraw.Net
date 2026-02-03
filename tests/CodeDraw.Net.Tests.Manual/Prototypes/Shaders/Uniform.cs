namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;

/// <summary>
/// Lightweight immutable bag of UniformValue.
/// </summary>
public readonly struct Uniforms
{
    public static readonly Uniforms Empty = new([]);

    public readonly UniformValue[] Values;

    public Uniforms(UniformValue[]? values)
    {
        Values = values ?? [];
    }

    public static Uniforms Of(params UniformValue[] values) => new(values);
}