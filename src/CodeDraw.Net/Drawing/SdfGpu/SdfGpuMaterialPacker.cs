using System.Runtime.InteropServices;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfGpu;

internal sealed class SdfGpuMaterialPacker
{
    // Reference-equality reuse is fine to start with (one node holds one material instance).
    private readonly Dictionary<SdfMaterialDef, int> _matIndex = new();
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

    public int GetOrAdd(SdfMaterialDef def)
    {
        if (_matIndex.TryGetValue(def, out var idx))
            return idx;

        var ruleFirst = _rules.Count;
        var ruleCount = def.Rules.Count;

        foreach (var r in def.Rules)
        {
            _rules.Add(new GpuSdfColorRule
            {
                Mode = (int)r.Mode,
                R = r.Color.R, G = r.Color.G, B = r.Color.B, A = r.Color.A,
                A0 = r.A,
                B0 = r.B,
                Feather = MathF.Max(0f, r.FeatherPx),
            });
        }

        var style = def.Style;
        var fill = style.Paint.Fill;
        var stroke = style.Paint.Stroke;

        // Note: Style.Opacity is not baked here; if you want global per-shape opacity,
        // multiply it into FillA/StrokeA at packing time.
        var mat = new GpuSdfMaterial
        {
            FillR = fill.R, FillG = fill.G, FillB = fill.B, FillA = fill.A,
            StrokeR = stroke.Color.R, StrokeG = stroke.Color.G, StrokeB = stroke.Color.B, StrokeA = stroke.Color.A,
            StrokeThickness = MathF.Max(0f, stroke.Thickness),
            FeatherPx = MathF.Max(0f, style.FeatherPx),
            HasFill = fill.A > 0f ? 1 : 0,
            HasStroke = (stroke.Thickness > 0f && stroke.Color.A > 0f) ? 1 : 0,
            RuleFirst = ruleFirst,
            RuleCount = ruleCount,
        };

        idx = _mats.Count;
        _mats.Add(mat);
        _matIndex.Add(def, idx);
        return idx;
    }
}