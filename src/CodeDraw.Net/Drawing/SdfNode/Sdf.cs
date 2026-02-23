using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Composition;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Primitives;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Transform;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode;

public static class Sdf
{
    // ---- primitives ----
    public static SdfCircleNode Circle(Vector2 c, float r) => new() { Center = c, Radius = r };
    public static SdfEllipseNode Ellipse(Vector2 c, Vector2 r) => new() { Center = c, Radius = r };
    public static SdfPolygonNode Polygon(params Vector2[] vertices) => new() { Points = vertices };
    public static SdfPolylineNode Polyline(bool closed, float radius, params Vector2[] vertices) => new() { Points = vertices, Closed = closed, Radius = radius};
    public static SdfRectNode Rect(Rect r) => new() { Rect = r };
    public static SdfRoundedRectNode RoundedRect(Rect r, float radius) => new() { Rect = r, Radius = radius };
    public static SdfSegmentNode Segment(Vector2 a, Vector2 b) => new() { P0 = a, P1 = b };
    public static SdfTriangleNode Triangle(Vector2 a, Vector2 b, Vector2 c) => new() { A = a, B = b, C = c };

    // ---- N-ary composition ----
    public static SdfUnionNode Union(params ISdf2Node[] children) => new() { Children = children };
    public static SdfIntersectNode Intersect(params ISdf2Node[] children) => new() { Children = children };
    public static SdfSubtractNode Subtract(ISdf2Node a, params ISdf2Node[] bs) => new() { A = a, Bs = bs };

    // ---- N-ary smoothing (k = smoothing radius in distance units; 0 => hard op) ----
    public static SdfSmoothUnionNode SmoothUnion(float k, params ISdf2Node[] children) => new() { K = k, Children = children };
    public static SdfSmoothIntersectNode SmoothIntersect(float k, params ISdf2Node[] children) => new() { K = k, Children = children };
    public static SdfSmoothSubtractNode SmoothSubtract(float k, ISdf2Node a, params ISdf2Node[] bs) => new() { K = k, A = a, Bs = bs };
    
    // ---- transform ----
    
    public static SdfTransformNode Transform(ISdf2Node child, in Matrix3x3 localToParent)
        => new() { Child = child, LocalToParent = localToParent };

    public static SdfTransformNode Translate(ISdf2Node child, float x, float y)
        => Transform(child, Matrix3x3.CreateTranslation(x, y));

    public static SdfTransformNode Rotate(ISdf2Node child, float angle,
        AngleUnit unit = AngleUnit.Degrees, RotationDirection dir = RotationDirection.Clockwise)
        => Transform(child, Matrix3x3.CreateRotation(angle, unit, dir));

    public static SdfTransformNode RotateAround(ISdf2Node child, float px, float py, float angle,
        AngleUnit unit = AngleUnit.Degrees, RotationDirection dir = RotationDirection.Clockwise)
    {
        var t0 = Matrix3x3.CreateTranslation(px, py);
        var r  = Matrix3x3.CreateRotation(angle, unit, dir);
        var t1 = Matrix3x3.CreateTranslation(-px, -py);
        return Transform(child, t0 * r * t1);
    }

    public static SdfTransformNode Scale(ISdf2Node child, float sx, float sy)
        => Transform(child, Matrix3x3.CreateScale(sx, sy));

    public static SdfTransformNode Shear(ISdf2Node child, float shx, float shy)
        => Transform(child, Matrix3x3.CreateShear(shx, shy));
}