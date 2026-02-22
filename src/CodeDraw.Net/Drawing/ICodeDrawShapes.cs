using MarcoZechner.CodeDrawDotNet.DrawLayer;
using MarcoZechner.CodeDrawDotNet.Shaders;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing;

public interface ICodeDrawShapes : ICodeDrawTransformStack
{
    // --- primitives ---
    public void Rect(in Rect r, in Paint paint);
    public void RoundedRect(in Rect r, float radius, in Paint paint);

    public void Circle(Vector2 center, float radius, in Paint paint);
    public void Ellipse(Vector2 center, Vector2 radius, in Paint paint);

    public void Line(Vector2 point0, Vector2 point1, in Stroke stroke);

    public void Triangle(in Vector2 a, in Vector2 b, in Vector2 c, in Paint paint);

    public void Polyline(ReadOnlySpan<Vector2> points, in Stroke stroke, bool closed = false);
    public void Polygon(ReadOnlySpan<Vector2> points, in Paint paint);

    // --- path entry ---
    public IPathBuilder Path(in Paint paint = default);

    // --- groups / collections ---
    public IShapeCollectionBuilder ShapeCollection(in Matrix3x3? initialTransform = null);

    // --- your existing stuff stays ---
    public CodeDrawLayer.BlitSrcStage Blit(CodeDrawLayer src);
    public void CustomDrawRect(Rect rect, CodeDrawShader shader, Uniforms uniforms);
    public void PostProcess(CodeDrawShader shader, Uniforms uniforms);
}