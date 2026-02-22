using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode;

/// <summary>
/// Runtime-editable SDF graph node (mutable fields allowed).
/// Compiled form (ISdf2) is immutable and cached via SdfCompiler.
/// </summary>
public interface ISdf2Node
{
    internal ISdf2 Build(SdfCompileContext ctx);
}