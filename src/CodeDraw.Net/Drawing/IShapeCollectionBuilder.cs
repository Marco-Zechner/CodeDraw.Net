using MarcoZechner.CodeDrawDotNet.Shaders;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing;

public interface IShapeCollectionBuilder
{
    public IShapeCollectionBuilder Translate(float x, float y);
    public IShapeCollectionBuilder Scale(float sx, float sy);
    public IShapeCollectionBuilder RotateDeg(float deg);
    public IShapeCollectionBuilder RotateAround(float px, float py, float deg);
    public IShapeCollectionBuilder Pivot(float ax, float ay);  // sets pivot in normalized bbox space, applied at Draw()

    public IShapeCollectionBuilder AddRect(in Rect r, in Paint paint);
    public IShapeCollectionBuilder AddCircle(float cx, float cy, float radius, in Paint paint);
    public IShapeCollectionBuilder AddPolygon(ReadOnlySpan<Vector2> pts, in Paint paint);
    public IShapeCollectionBuilder AddPath(Action<IPathBuilder> build, in Paint paint);

    public IShapeCollectionBuilder ApplyShader(CodeDrawShader shader, Uniforms uniforms); // optional: executes after drawing this collection into layer
    public void Draw();
}