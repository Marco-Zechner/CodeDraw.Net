using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode;

internal sealed class SdfCompileContext
{
    internal readonly Dictionary<ISdf2Node, CacheEntry> Cache = new();
    internal readonly HashSet<ISdf2Node> Visiting = [];

    internal readonly record struct CacheEntry(ISdf2 Built, int Version);
}