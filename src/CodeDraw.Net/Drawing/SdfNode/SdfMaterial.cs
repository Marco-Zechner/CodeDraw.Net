namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode;

public sealed class SdfMaterial(in DrawStyle style, SdfColorOverwrite overwrite = SdfColorOverwrite.OnlyDefault)
{
    public DrawStyle Style = style;
    public SdfColorOverwrite Overwrite = overwrite;
    public readonly List<SdfColorRule> Rules = [];

}