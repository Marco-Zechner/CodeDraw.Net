using MarcoZechner.ColorDotNet.RGB;

namespace MarcoZechner.CodeDrawDotNet.Drawing;

public interface IPathBuilder
{
    // internal: a pooled or stack scratch for points/verbs
    // store verbs: MoveTo/LineTo/QuadTo/CubicTo/ArcTo/Close

    public IPathBuilder MoveTo(float x, float y);
    public IPathBuilder LineTo(float x, float y);
    public IPathBuilder QuadTo(float cx, float cy, float x, float y);
    public IPathBuilder CubicTo(float cx1, float cy1, float cx2, float cy2, float x, float y);
    public IPathBuilder ArcTo(float cx, float cy, float radius, float startDeg, float sweepDeg, int segmentsHint = 0);
    public IPathBuilder Close();

    // optional style tweaks per path (override paint)
    public IPathBuilder Fill(ColorF fill);
    public IPathBuilder Stroke(in Stroke stroke);
    public IPathBuilder Paint(in Paint paint);
    public IPathBuilder Style(in DrawStyle style);

    public void Draw(); // enqueue as CmdPath
    
    public IShapeCollectionBuilder ReturnToShapeCollectionBuilder(IShapeCollectionBuilder shapeCollectionBuilder) => shapeCollectionBuilder;
}