using MarcoZechner.CodeDrawDotNet.Drawing;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfGpu;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Composition;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Primitives;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Transform;
using MarcoZechner.CodeDrawDotNet.Text;
using MarcoZechner.CodeDrawDotNet.Window;
using MarcoZechner.ColorDotNet.HSV;
using MarcoZechner.ColorDotNet.RGB;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.PrototypeTest;

public class SdfPrototype2
{
    // --- persistent nodes (allocated once) ---
    private readonly SdfRectNode[] _barRects = new SdfRectNode[6];
    private readonly SdfMaterialDef[] _barMaterials = new SdfMaterialDef[6];
    private readonly SdfTransformNode[] _barXf = new SdfTransformNode[6];
    private readonly ISdf2Node[] _barChildren = new ISdf2Node[6];

    [ConstructorPrototype("SdfPrototype2")]
    public SdfPrototype2()
    {
        var ups = 0f;
        var updates = 0;
        var upsAccum = 0f;

        // --- Styles ---
        var baseFont = FontRef.FromFile(
            @"C:\DevProjects\CodeDraw.Net\tests\CodeDraw.Net.Tests.Manual\resources\fonts\FiraCode-VF.ttf"
        );
        
        var styleHud = new TextStyle
        {
            Font = baseFont.WithVariant(FontVariant.Regular),
            SizePx = 18,
            Align = TextAlign.Left,
            VAlign = TextVAlign.Top,
            Color = new ColorF(1f, 1f, 1f, 0.95f),
            Background = new ColorF(0f, 0f, 0f, 0.55f),
            DebugMode = TextDebugMode.None,
            DebugRects = DebugRectMode.Outline,
            DebugOutlinePx = 1,
            MonospaceSnapLineAlignToCells = true
        };
        
        // build graph once
        for (var i = 0; i < 6; i++)
        {
            _barRects[i] = new SdfRectNode { Rect = new RectWh(0, 0, 30, 80) };

            // different color per bar
            var hue = i / 6f;
            var color = new ColorHsvF((int)(hue * 360f), 0.7f, 1f); // or your own hsv helper

            var style = new DrawStyle(
                new Paint(color, default(Stroke)),
                FeatherPx: 1.0f
            );

            _barMaterials[i] = new SdfMaterialDef(style);

            // attach material to the primitive
            ISdf2Node tagged = new SdfMaterialNode
            {
                Child = _barRects[i],
                Material = _barMaterials[i]
            };

            _barXf[i] = new SdfTransformNode
            {
                Child = tagged,
                LocalToParent = Matrix3x3.CreateRotation(i * 60f),
            };

            _barChildren[i] = _barXf[i];
        }
        
        var barsCenterCircle = new SdfCircleNode {
            Radius = 10,
            Center = Vector2.Zero,
        };

        var offsetCircle = new SdfTransformNode {
            Child = barsCenterCircle,
            LocalToParent = Matrix3x3.CreateTranslation(0, 20),
        };

        var barsUnion = new SdfSmoothUnionNode
        {
            K = 25f,
            Children = _barChildren,
        };

        var barsSub = new SdfSubtractNode {
            A = barsUnion,
            Bs = [offsetCircle],
        };
        
        // var barStyle = new DrawStyle(
            // new Paint(new ColorF(0.6f, 0.4f, 1f, 1f), default(Stroke)),
            // FeatherPx: 1.0f
        // );

        // ---- window loop ----
        using var app = CodeDrawHost.Started();
        var win = new CodeDrawWindow(900, 600, 50, 50, "SDF Prototype 2");
        var time = 0f;

        win.OnUpdate += ctx =>
        {
            var layer = ctx.Win.Layer;
            
            var dt = ctx.DeltaSeconds;

            updates++;
            upsAccum += dt;
            if (upsAccum >= 0.25f)
            {
                ups = updates / upsAccum;
                updates = 0;
                upsAccum = 0f;
            }
            
            time += ctx.DeltaSeconds;

            layer.Clear(0.1f, 0.1f, 0.12f, 1f);

            // animate: only change transforms, mark dirty via property setters
            
            for (var i = 0; i < 6; i++)
            {
                var outwardsOffset = MathG.Sin(60*i+200*time) * 25f + 100;
                var rect = _barRects[i];
                _barRects[i].Rect = new Rect(new Vector2(0, outwardsOffset), rect.Rect.Size, OriginLocation.BottomCenter);
            }
            
            offsetCircle.LocalToParent = Matrix3x3.CreateRotation(time * 200f) * Matrix3x3.CreateTranslation(0f, 20f);
            
            using (layer.ScopeTranslate(layer.Width/2, layer.Height/2))
            using (layer.ScopeRotate(time * 20))
            using (layer.ScopeScale(2, 2))
            {
                layer.DrawSdf(barsSub);
                // barsSub.DrawDebugRect(layer, ((ColorF)Colors.WHITE) with { A = 0.5f }, barStyle);
            }
            
            var hud = $"UPS: {ups:0.0}";
            var hudSize = layer.MeasureText(hud, styleHud);
            layer.DrawText(hud, 20, layer.Height - 20 - hudSize.Y, styleHud);

            layer.Render();
        };

        app.WaitForClose();
    }
}