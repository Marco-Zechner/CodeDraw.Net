using MarcoZechner.ColorLib;
using MarcoZechner.Math;
using Topten.RichTextKit;

namespace MarcoZechner.CodeDrawDotNet.Old1;

public partial class Shapes
{
    public readonly DrawQueue DrawBuffer = new();
    public int LineWidth { get; set; } = 1;
    public Color DrawColor { get; set; } = Color.BLACK;
    public CornerStyle CornerStyle { get; set; }
    public int CornerRadius { get; set; } = 0;
    public bool IsAntiAliased { get; set; }
    public TextFormat TextFormat { get; set; } = new TextFormat();
    public bool IsInstantDraw { get; set; } = false;
    
    #region Outline Shapes
    public void DrawSquare(Vector2 leftTop, float sideLength)
    {
        DrawBuffer.Enqueue(
            new RectShape(leftTop, new Vector2(sideLength, sideLength)),
            new ShapeSettings(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased),
            IsInstantDraw);
    }

    public void DrawRectangle(Vector2 leftTop, Vector2 size)
    {
        DrawBuffer.Enqueue(
            new RectShape(leftTop, size),
            new ShapeSettings(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased),
        IsInstantDraw);
    }

    public void DrawCircle(Vector2 center, float radius)
    {
        DrawBuffer.Enqueue(
            new CircleShape(center, radius),
            new ShapeSettings(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased),
        IsInstantDraw);
    }

    public void DrawEllipse(Vector2 center, Vector2 size)
    {
        DrawBuffer.Enqueue(
            new EllipseShape(center, size),
            new ShapeSettings(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased),
            IsInstantDraw);
    }

    public void DrawPie(Vector2 center, float radius, float startAngle, float sweepAngle, AngleUnit angleUnit = AngleUnit.Degrees)
    {
        DrawBuffer.Enqueue(
            new PieShape(center, radius, startAngle, sweepAngle, angleUnit),
            new ShapeSettings(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased),
            IsInstantDraw);
    }

    public void DrawTriangle(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        DrawBuffer.Enqueue(
            new TriangleShape(p1, p2, p3),
            new ShapeSettings(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased),
            IsInstantDraw);
    }

    public void DrawPolygon(params Vector2[] vertices)
    {
        DrawBuffer.Enqueue(
            new PolygonShape(vertices),
            new ShapeSettings(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased),
            IsInstantDraw);
    }

    #endregion

    #region Filled Shapes

    public void FillSquare(Vector2 leftTop, float sideLength)
    {
        DrawBuffer.Enqueue(
            new RectShape(leftTop, new Vector2(sideLength, sideLength)),
            new ShapeSettings(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased, true),
            IsInstantDraw);
    }

    public void FillRectangle(Vector2 leftTop, Vector2 size)
    {
        DrawBuffer.Enqueue(
            new RectShape(leftTop, size),
            new ShapeSettings(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased, true),
            IsInstantDraw);
    }

    public void FillCircle(Vector2 center, float radius)
    {
        DrawBuffer.Enqueue(
            new CircleShape(center, radius),
            new ShapeSettings(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased, true),
            IsInstantDraw);
    }

    public void FillEllipse(Vector2 center, Vector2 size)
    {
        DrawBuffer.Enqueue(
            new EllipseShape(center, size),
            new ShapeSettings(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased, true),
            IsInstantDraw);
    }

    public void FillPie(Vector2 center, float radius, float startAngle, float sweepAngle, AngleUnit angleUnit = AngleUnit.Degrees)
    {
        DrawBuffer.Enqueue(
            new PieShape(center, radius, startAngle, sweepAngle, angleUnit),
            new ShapeSettings(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased, true),
            IsInstantDraw);
    }

    public void FillTriangle(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        DrawBuffer.Enqueue(
            new TriangleShape(p1, p2, p3),
            new ShapeSettings(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased, true),
            IsInstantDraw);
    }


    public void FillPolygon(params Vector2[] vertices)
    {
        DrawBuffer.Enqueue(
            new PolygonShape(vertices),
            new ShapeSettings(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased, true),
            IsInstantDraw);
    }

    #endregion

    #region Point, Lines, Curves

    public void DrawPoint(Vector2 point)
    {
        DrawBuffer.Enqueue(
            new PointShape(point),
            new ShapeSettings(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased),
            IsInstantDraw);
    }

    public void DrawLine(Vector2 start, Vector2 end)
    {
        DrawBuffer.Enqueue(
            new LineShape(start, end),
            new ShapeSettings(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased),
            IsInstantDraw);
    }

    public void DrawCurve(Vector2 start, Vector2 control, Vector2 end)
    {
        DrawBuffer.Enqueue(
            new CurveShape(start, control, end),
            new ShapeSettings(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased),
            IsInstantDraw);
    }

    public void DrawBezier(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end)
    {
        DrawBuffer.Enqueue(
            new BezierShape(start, control1, control2, end),
            new ShapeSettings(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased),
            IsInstantDraw);
    }

    public void DrawArc(Vector2 center, float radius, float startAngle, float sweepAngle, AngleUnit angleUnit = AngleUnit.Degrees)
    {
        DrawBuffer.Enqueue(
            new ArcShape(center, radius, startAngle, sweepAngle, angleUnit),
            new ShapeSettings(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased),
            IsInstantDraw);
    }

    #endregion

    #region Curves Filled 

    public void FillCurve(Vector2 start, Vector2 control, Vector2 end)
    {
        DrawBuffer.Enqueue(
            new CurveShape(start, control, end),
            new ShapeSettings(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased, true),
            IsInstantDraw);
    }

    public void FillBezier(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end)
    {
        DrawBuffer.Enqueue(
            new BezierShape(start, control1, control2, end),
            new ShapeSettings(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased, true),
            IsInstantDraw);
    }

    public void FillArc(Vector2 center, float radius, float startAngle, float sweepAngle, AngleUnit angleUnit = AngleUnit.Degrees)
    {
        DrawBuffer.Enqueue(
            new ArcShape(center, radius, startAngle, sweepAngle, angleUnit),
            new ShapeSettings(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased, true),
            IsInstantDraw);
    }

    #endregion

    #region Text

    public void DrawText(Vector2 position, string text)
    {
        DrawBuffer.Enqueue(
            new TextShape(position, text, new(TextFormat)),
            new ShapeSettings(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased),
            IsInstantDraw);
    }

    public Vector2 MeasureText(string text)
    {
        var rs = new RichString()
        {
            DefaultStyle = new Style()
            {
                FontFamily = TextFormat.FontFamily,
                FontSize = TextFormat.FontSize,
                FontItalic = (TextFormat.FontStyle & FontStyle.Italic) == FontStyle.Italic,
                StrikeThrough = (TextFormat.FontStyle & FontStyle.Strikeout) == FontStyle.Strikeout ? StrikeThroughStyle.Solid : StrikeThroughStyle.None,
                Underline = (TextFormat.FontStyle & FontStyle.Underline) == FontStyle.Underline ? UnderlineStyle.Solid : UnderlineStyle.None,
            },
        }
        .Add(text)
        .Bold((TextFormat.FontStyle & FontStyle.Bold) == FontStyle.Bold);
        return new Vector2(rs.MeasuredWidth, rs.MeasuredHeight);
    }

    #endregion
    
    #region Image
    
    /// <summary>Draw image at a position using its natural pixel size.</summary>
    public void DrawImage(ImageHandle img, Vector2 position, bool antialias = true)
        => DrawImage(img, position, img.NaturalSize, antialias);

    /// <summary>Draw image scaled to a destination size.</summary>
    public void DrawImage(ImageHandle img, Vector2 position, Vector2 size, bool antialias = true)
    {
        // Enqueue a command so it’s rendered in your Render loop after transforms
        DrawBuffer.Enqueue(
            new ImageShape(img.Image, position, size, antialias),
            new ShapeSettings(DrawColor, LineWidth, CornerStyle, CornerRadius, antialias),
            IsInstantDraw
        );
    }

    /// <summary>Convenience: load from path and draw (natural size).</summary>
    public void DrawImage(string filePath, Vector2 position, bool antialias = true)
        => DrawImage(ImageHandler.LoadImage(filePath), position, antialias);

    /// <summary>Convenience: load from path and draw scaled.</summary>
    public void DrawImage(string filePath, Vector2 position, Vector2 size, bool antialias = true)
        => DrawImage(ImageHandler.LoadImage(filePath), position, size, antialias);
    
    #endregion
}