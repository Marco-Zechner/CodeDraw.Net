using MarcoZechner.Math;
using Topten.RichTextKit;

namespace MarcoZechner.SharpDraw.CodeDrawLib;

public partial class CodeDraw {
    
    
    #region Outline Shapes

    public void DrawSquare(Vector2 leftTop, float sideLength) {
        _drawBuffer.Enqueue(
            new RectShape(leftTop, new Vector2(sideLength, sideLength)), 
            new(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased), 
            IsInstantDraw);
    }

    public void DrawRectangle(Vector2 leftTop, Vector2 size) {
        _drawBuffer.Enqueue(
            new RectShape(leftTop, size), 
        new(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased), 
        IsInstantDraw);
    }

    public void DrawCircle(Vector2 center, float radius) {
        _drawBuffer.Enqueue(
            new CircleShape(center, radius), 
        new(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased), 
        IsInstantDraw);
    }

    public void DrawEllipse(Vector2 center, Vector2 size) {
        _drawBuffer.Enqueue(
            new EllipseShape(center, size), 
            new(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased), 
            IsInstantDraw);
   }

    public void DrawPie(Vector2 center, float radius, float startAngle, float sweepAngle, AngleUnit angleUnit = AngleUnit.Degrees) {
        _drawBuffer.Enqueue(
            new PieShape(center, radius, startAngle, sweepAngle, angleUnit), 
            new(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased), 
            IsInstantDraw);
    }

    public void DrawTriangle(Vector2 p1, Vector2 p2, Vector2 p3) {
        _drawBuffer.Enqueue(
            new TriangleShape(p1, p2, p3), 
            new(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased), 
            IsInstantDraw);
    }

    public void DrawPolygon(params Vector2[] vertices) {
        _drawBuffer.Enqueue(
            new PolygonShape(vertices), 
            new(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased), 
            IsInstantDraw);
    }
    
    #endregion

    #region Filled Shapes

    public void FillSquare(Vector2 leftTop, float sideLength) {
        _drawBuffer.Enqueue(
            new RectShape(leftTop, new Vector2(sideLength, sideLength)), 
            new(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased, true), 
            IsInstantDraw);
    }

    public void FillRectangle(Vector2 leftTop, Vector2 size) {
        _drawBuffer.Enqueue(
            new RectShape(leftTop, size), 
            new(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased, true), 
            IsInstantDraw);
    }

    public void FillCircle(Vector2 center, float radius) {
        _drawBuffer.Enqueue(
            new CircleShape(center, radius), 
            new(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased, true), 
            IsInstantDraw);
    }

    public void FillEllipse(Vector2 center, Vector2 size) {
        _drawBuffer.Enqueue(
            new EllipseShape(center, size), 
            new(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased, true),
            IsInstantDraw);
    }

    public void FillPie(Vector2 center, float radius, float startAngle, float sweepAngle, AngleUnit angleUnit = AngleUnit.Degrees) {
        _drawBuffer.Enqueue(
            new PieShape(center, radius, startAngle, sweepAngle, angleUnit), 
            new(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased, true), 
            IsInstantDraw);
    }

    public void FillTriangle(Vector2 p1, Vector2 p2, Vector2 p3) {
        _drawBuffer.Enqueue(
            new TriangleShape(p1, p2, p3), 
            new(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased, true), 
            IsInstantDraw);
    }


    public void FillPolygon(params Vector2[] vertices) {
        _drawBuffer.Enqueue(
            new PolygonShape(vertices), 
            new(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased, true), 
            IsInstantDraw);
    }

    #endregion

    #region Point, Lines, Curves

    public void DrawPoint(Vector2 point) {
        _drawBuffer.Enqueue(
            new PointShape(point), 
            new(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased), 
            IsInstantDraw);
    }
   
    public void DrawLine(Vector2 start, Vector2 end) {
        _drawBuffer.Enqueue(
            new LineShape(start, end), 
            new(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased), 
            IsInstantDraw);
    }

    public void DrawCurve(Vector2 start, Vector2 control, Vector2 end) {
        _drawBuffer.Enqueue(
            new CurveShape(start, control, end), 
            new(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased), 
            IsInstantDraw);
    }

    public void DrawBezier(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end) {
        _drawBuffer.Enqueue(
            new BezierShape(start, control1, control2, end), 
            new(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased), 
            IsInstantDraw);
    }

    public void DrawArc(Vector2 center, float radius, float startAngle, float sweepAngle, AngleUnit angleUnit = AngleUnit.Degrees) {
        _drawBuffer.Enqueue(
            new ArcShape(center, radius, startAngle, sweepAngle, angleUnit), 
            new(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased), 
            IsInstantDraw);
    }

    #endregion

    #region Curves Filled 

    public void FillCurve(Vector2 start, Vector2 control, Vector2 end) {
        _drawBuffer.Enqueue(
            new CurveShape(start, control, end), 
            new(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased, true), 
            IsInstantDraw);
    }

    public void FillBezier(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end) {
        _drawBuffer.Enqueue(
            new BezierShape(start, control1, control2, end), 
            new(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased, true),
            IsInstantDraw);
    }

    public void FillArc(Vector2 center, float radius, float startAngle, float sweepAngle, AngleUnit angleUnit = AngleUnit.Degrees) {
        _drawBuffer.Enqueue(
            new ArcShape(center, radius, startAngle, sweepAngle, angleUnit), 
            new(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased, true), 
            IsInstantDraw);
    }

    #endregion

    #region Text

    public void DrawText(Vector2 position, string text) {
        _drawBuffer.Enqueue(
            new TextShape(position, text, new(TextFormat)), 
            new(DrawColor, LineWidth, CornerStyle, CornerRadius, IsAntiAliased), 
            IsInstantDraw);
    }

    public Vector2 MeasureText(string text) {
        var rs = new RichString() {
            DefaultStyle = new Style() {
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
}