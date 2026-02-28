using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode;

namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf;

internal readonly record struct SdfActiveMaterial(SdfMaterial Material, SdfColorOverwrite Overwrite);