using MarcoZechner.CodeDrawDotNet.DrawLayer;
using MarcoZechner.ColorDotNet.RGB;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Shaders;

public enum UniformType
{
    FLOAT1,
    FLOAT2,
    FLOAT3,
    FLOAT4,
    TEX_2D,
    MAT3X3,
    COLOR,
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
    public readonly CodeDrawLayer? LayerRef;
    
    public readonly Matrix3x3 Mat;
    public readonly ColorF ColorF;

    private UniformValue(
        string name, 
        UniformType type,
        float a, 
        float b, 
        float c, 
        float d, 
        CodeDrawLayer? layerRef,
        Matrix3x3 mat,
        ColorF color)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Uniform name must not be null/empty.", nameof(name));

        Name = name;
        Type = type;
        
        A = a; B = b; C = c; D = d;
        LayerRef = layerRef;

        Mat = mat;
        ColorF = color;
    }

    public static UniformValue Float(string name, float v)
        => new(name, UniformType.FLOAT1, v, 0, 0, 0, null, Matrix3x3.Identity, default);

    public static UniformValue Float2(string name, float x, float y)
        => new(name, UniformType.FLOAT2, x, y, 0, 0, null, Matrix3x3.Identity, default);

    public static UniformValue Float2(string name, Vector2 v)
        => new(name, UniformType.FLOAT2, v.X, v.Y, 0, 0, null, Matrix3x3.Identity, default);
    
    public static UniformValue Float3(string name, float x, float y, float z)
        => new(name, UniformType.FLOAT3, x, y, z, 0, null, Matrix3x3.Identity, default);
    
    public static UniformValue Float4(string name, float x, float y, float z, float w)
        => new(name, UniformType.FLOAT4, x, y, z, w, null, Matrix3x3.Identity, default);

    public static UniformValue Float4(string name, Vector2 v1, Vector2 v2)
        => new(name, UniformType.FLOAT4, v1.X, v1.Y, v2.X, v2.Y, null, Matrix3x3.Identity, default);
    
    public static UniformValue Tex2D(string name, CodeDrawLayer layerRef)
        => new(name, UniformType.TEX_2D, 0, 0, 0, 0, layerRef, Matrix3x3.Identity, default);

    // ReSharper disable once InconsistentNaming
    public static UniformValue Mat3x3(string name, Matrix3x3 m)
        => new(name, UniformType.MAT3X3, 0, 0, 0, 0, null, m, default);
    
    public static UniformValue Color(string name, ColorF c)
        => new(name, UniformType.MAT3X3, 0, 0, 0, 0, null, Matrix3x3.Identity, c);
}