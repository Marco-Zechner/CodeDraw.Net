using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Primitives;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode;

public static class Sdf
{
    public static SdfCircleNode Circle(Vector2 c, float r) => new() { Center = c, Radius = r };
    public static SdfEllipseNode Ellipse(Vector2 c, Vector2 r) => new() { Center = c, Radius = r };
    public static SdfPolygonNode Polygon(params Vector2[] vertices) => new() { Points = vertices };
    public static SdfPolylineNode Polyline(params Vector2[] vertices) => new() { Points = vertices };
    public static SdfRectNode Rect(Rect r) => new() { Rect = r };
    public static SdfRoundedRectNode RoundedRect(Rect r, float radius) => new() { Rect = r, Radius = radius };
    public static SdfSegmentNode Segment(Vector2 a, Vector2 b) => new() { P0 = a, P1 = b };
    public static SdfTriangleNode Triangle(Vector2 a, Vector2 b, Vector2 c) => new() { A =  a, B = b, C = c };
}