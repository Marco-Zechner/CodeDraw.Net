namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;

public enum UniformType
{
    FLOAT1,
    FLOAT2,
    FLOAT3,
    FLOAT4,
}

/// <summary>
/// A single uniform value for CustomShader usage.
/// NOTE: Built-in uniforms are reserved and cannot be set by user:
/// uPosSize, uRes, uTime, uColor, uTex
/// </summary>
public readonly struct UniformValue
{
    public readonly string Name;
    public readonly UniformType Type;
    public readonly float A, B, C, D;

    private UniformValue(string name, UniformType type, float a, float b, float c, float d)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Uniform name must not be null/empty.", nameof(name));

        Name = name;
        Type = type;
        A = a; B = b; C = c; D = d;
    }

    public static UniformValue Float(string name, float v)
        => new(name, UniformType.FLOAT1, v, 0, 0, 0);

    public static UniformValue Float2(string name, float x, float y)
        => new(name, UniformType.FLOAT2, x, y, 0, 0);

    public static UniformValue Float3(string name, float x, float y, float z)
        => new(name, UniformType.FLOAT3, x, y, z, 0);

    public static UniformValue Float4(string name, float x, float y, float z, float w)
        => new(name, UniformType.FLOAT4, x, y, z, w);
}