using System.Runtime.InteropServices;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode;
using MarcoZechner.ColorDotNet.RGB;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfGpu;

internal sealed class SdfGpuMaterialPacker
{
    private readonly Dictionary<SdfMaterial, int> _matIndex = new();
    private readonly List<GpuSdfMaterial> _mats = new(16);
    private readonly List<GpuSdfColorRule> _rules = new(64);

    public ReadOnlySpan<GpuSdfMaterial> Materials => CollectionsMarshal.AsSpan(_mats);
    public ReadOnlySpan<GpuSdfColorRule> Rules => CollectionsMarshal.AsSpan(_rules);

    public void Clear()
    {
        _matIndex.Clear();
        _mats.Clear();
        _rules.Clear();
    }
       
    private const float DEFAULT_THICKNESS_PX = 1.5f;

    public int GetOrAdd(SdfMaterial mat, bool forceStrokeOnly)
    {
        if (_matIndex.TryGetValue(mat, out var idx))
            return idx;

        var ruleFirst = _rules.Count;
        var ruleCount = mat.Rules.Count;

        foreach (var r in mat.Rules)
        {
            var cB = r.ColorB ?? r.ColorA;
            _rules.Add(new GpuSdfColorRule
            {
                Mode = (int)r.Mode,

                R = r.ColorA.R, G = r.ColorA.G, B = r.ColorA.B, A = r.ColorA.A,
                A0 = r.A,
                B0 = r.B,
                Feather = MathF.Max(0f, r.FeatherPx),
                Step = MathF.Max(0f, r.StepPx),

                R2 = cB.R, G2 = cB.G, B2 = cB.B, A2 = cB.A,
            });
        }

        var style = mat.Style;

        var fill = style.Paint.Fill;
        var stroke = style.Paint.Stroke;

        // Detect whether an explicit stroke exists.
        var hasExplicitStroke = stroke is { Thickness: > 0f, Color.A: > 0f };

        // ---- forceStrokeOnly policy ----
        if (forceStrokeOnly)
        {
            // Disable fill always.
            fill = fill with { A = 0f };

            // If there's no stroke, synthesize one from fill (or white if fill is also transparent).
            if (!hasExplicitStroke)
            {
                // choose source color
                var c = style.Paint.Fill.A > 0f ? style.Paint.Fill : new ColorF(1f, 1f, 1f);

                // pick a reasonable default thickness

                stroke = stroke with
                {
                    Color = c,
                    Thickness = DEFAULT_THICKNESS_PX,
                };

                hasExplicitStroke = true;
            }
        }

        var gpu = new GpuSdfMaterial
        {
            FillR = fill.R, FillG = fill.G, FillB = fill.B, FillA = fill.A,

            StrokeR = stroke.Color.R, StrokeG = stroke.Color.G, StrokeB = stroke.Color.B, StrokeA = stroke.Color.A,
            StrokeThickness = MathF.Max(0f, stroke.Thickness),

            FeatherPx = MathF.Max(0f, style.FeatherPx),

            HasFill = !forceStrokeOnly && fill.A > 0f ? 1 : 0,
            HasStroke = hasExplicitStroke ? 1 : 0,

            RuleFirst = ruleFirst,
            RuleCount = ruleCount,
        };

        idx = _mats.Count;
        _mats.Add(gpu);
        _matIndex.Add(mat, idx);
        return idx;
    }
}