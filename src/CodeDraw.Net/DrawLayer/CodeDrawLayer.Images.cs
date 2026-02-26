using MarcoZechner.CodeDrawDotNet.DrawLayer.Commands;
using MarcoZechner.CodeDrawDotNet.Images;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer;

public sealed partial class CodeDrawLayer
{
    public void DrawImage(CodeDrawImage img, Rect<int> dstRectPx, ImageDrawOptions opt = new())
    {
        if (_disposed) return;

        Enqueue(
            new CmdDrawImage {
                Img = img,
                Opt = opt,
                DstRectPx = dstRectPx,
            }
        );
    }
}