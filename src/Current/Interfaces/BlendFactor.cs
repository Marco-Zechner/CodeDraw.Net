namespace MarcoZechner.CodeDrawDotNet;

/// <summary>
/// Full custom blend description, including factors and equations for color and alpha.
/// </summary>
public readonly record struct BlendDesc(
    BlendFactor SrcColor, BlendFactor DstColor, BlendOp ColorOp,
    BlendFactor SrcAlpha, BlendFactor DstAlpha, BlendOp AlphaOp);

/// <summary>Blend factor enum for custom blending.</summary>
public enum BlendFactor
{
    Zero, One,
    SrcColor, OneMinusSrcColor,
    DstColor, OneMinusDstColor,
    SrcAlpha, OneMinusSrcAlpha,
    DstAlpha, OneMinusDstAlpha,
    ConstantColor, OneMinusConstantColor,
    ConstantAlpha, OneMinusConstantAlpha
}