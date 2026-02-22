using MarcoZechner.CodeDrawDotNet.DrawLayer;
using MarcoZechner.CodeDrawDotNet.Shaders;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing;

public interface ICodeDrawShapes : ICodeDrawTransformStack
{
    void Rect(in Rect r, in DrawStyle style);
    void RoundedRect(in Rect r, float radius, in DrawStyle style);

    void Circle(Vector2 center, float radius, in DrawStyle style);
    void Ellipse(Vector2 center, Vector2 radius, in DrawStyle style);

    void Line(Vector2 p0, Vector2 p1, in Stroke stroke, BlendMode blend = BlendMode.SOURCE_OVER_ALPHA, float opacity = 1f);

    void Triangle(in Vector2 a, in Vector2 b, in Vector2 c, in DrawStyle style);

    void Polyline(ReadOnlySpan<Vector2> points, in Stroke stroke, bool closed = false,
        BlendMode blend = BlendMode.SOURCE_OVER_ALPHA, float opacity = 1f);
    void Polygon(ReadOnlySpan<Vector2> points, in DrawStyle style);

    IPathBuilder Path(in DrawStyle style = default);
    IShapeCollectionBuilder ShapeCollection(in Matrix3x3? initialTransform = null);

    CodeDrawLayer.BlitSrcStage Blit(CodeDrawLayer src);
    void CustomDrawRect(Rect rect, CodeDrawShader shader, Uniforms uniforms);
    void PostProcess(CodeDrawShader shader, Uniforms uniforms);
}