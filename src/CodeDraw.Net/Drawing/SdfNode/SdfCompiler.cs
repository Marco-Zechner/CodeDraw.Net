using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode;

internal static class SdfCompiler
{
    internal static ISdf2 Compile(ISdf2Node node)
    {
        var ctx = new SdfCompileContext();
        return Compile(node, ctx);
    }

    internal static ISdf2 Compile(ISdf2Node node, SdfCompileContext ctx)
    {
        var v = GetVersion(node);

        if (ctx.Cache.TryGetValue(node, out var ce) && ce.Version == v)
            return ce.Built;

        if (!ctx.Visiting.Add(node))
            throw new InvalidOperationException("SDF graph cycle detected.");

        var result = node.Build(ctx);

        ctx.Visiting.Remove(node);
        ctx.Cache[node] = new SdfCompileContext.CacheEntry(result, v);
        return result;
    }

    private static int GetVersion(ISdf2Node node)
        => node is IVersionedSdfNode vn ? vn.Version : 0;
}