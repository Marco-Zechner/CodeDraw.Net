using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode;

internal sealed class SdfCompileContext
{
    internal readonly Dictionary<ISdf2Node, ISdf2> Cache = new();
    internal readonly HashSet<ISdf2Node> Visiting = [];
}

internal static class SdfCompiler
{
    internal static ISdf2 Compile(ISdf2Node node)
    {
        var ctx = new SdfCompileContext();
        return Compile(node, ctx);
    }

    internal static ISdf2 Compile(ISdf2Node node, SdfCompileContext ctx)
    {
        if (ctx.Cache.TryGetValue(node, out var built))
            return built;

        if (!ctx.Visiting.Add(node))
            throw new InvalidOperationException("SDF graph cycle detected.");

        var result = node.Build(ctx);

        ctx.Visiting.Remove(node);
        ctx.Cache[node] = result;
        return result;
    }
}