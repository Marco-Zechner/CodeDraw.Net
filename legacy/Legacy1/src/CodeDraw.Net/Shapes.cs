using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet;

public partial class Shapes
{
    #region Outline Shapes

    public void DrawSquare(float xLeft, float yTop, float sideLength)
        => DrawSquare(new Vector2(xLeft, yTop), sideLength);

    public void DrawRectangle(float xLeft, float yTop, float width, float height)
        => DrawRectangle(new Vector2(xLeft, yTop), new Vector2(width, height));

    public void DrawCircle(float xCenter, float yCenter, float radius)
        => DrawCircle(new Vector2(xCenter, yCenter), radius);

    public void DrawEllipse(float xCenter, float yCenter, float width, float height)
        => DrawEllipse(new Vector2(xCenter, yCenter), new Vector2(width, height));

    public void DrawPie(float xCenter, float yCenter, float radius, float startAngle, float sweepAngle, AngleUnit angleUnit = AngleUnit.DEGREES)
        => DrawPie(new Vector2(xCenter, yCenter), radius, startAngle, sweepAngle, angleUnit);

    public void DrawTriangle(float x1, float y1, float x2, float y2, float x3, float y3)
        => DrawTriangle(new Vector2(x1, y1), new Vector2(x2, y2), new Vector2(x3, y3));

    public void DrawPolygon(params float[] vertices)
    {
        if (vertices.Length % 2 != 0)
        {
            throw new ArgumentException("Vertices array must contain an even number of elements.");
        }

        Vector2[] points = new Vector2[vertices.Length / 2];
        for (int i = 0; i < vertices.Length; i += 2)
        {
            points[i / 2] = new Vector2(vertices[i], vertices[i + 1]);
        }
        DrawPolygon(points);
    }

    #endregion

    #region Filled Shapes

    public void FillSquare(float xLeft, float yTop, float sideLength)
        => FillSquare(new Vector2(xLeft, yTop), sideLength);

    public void FillRectangle(float xLeft, float yTop, float width, float height)
        => FillRectangle(new Vector2(xLeft, yTop), new Vector2(width, height));

    public void FillCircle(float xCenter, float yCenter, float radius)
        => FillCircle(new Vector2(xCenter, yCenter), radius);

    public void FillEllipse(float xCenter, float yCenter, float width, float height)
        => FillEllipse(new Vector2(xCenter, yCenter), new Vector2(width, height));

    public void FillPie(float xCenter, float yCenter, float radius, float startAngle, float sweepAngle, AngleUnit angleUnit = AngleUnit.DEGREES)
        => FillPie(new Vector2(xCenter, yCenter), radius, startAngle, sweepAngle, angleUnit);

    public void FillTriangle(float x1, float y1, float x2, float y2, float x3, float y3)
        => FillTriangle(new Vector2(x1, y1), new Vector2(x2, y2), new Vector2(x3, y3));

    public void FillPolygon(params float[] vertices)
    {
        if (vertices.Length % 2 != 0)
        {
            throw new ArgumentException("Vertices array must contain an even number of elements.");
        }

        Vector2[] points = new Vector2[vertices.Length / 2];
        for (int i = 0; i < vertices.Length; i += 2)
        {
            points[i / 2] = new Vector2(vertices[i], vertices[i + 1]);
        }
        FillPolygon(points);
    }

    #endregion

    #region Point, Lines, Curves

    public void DrawPoint(float x, float y)
        => DrawPoint(new Vector2(x, y));

    public void DrawLine(float startX, float startY, float endX, float endY)
        => DrawLine(new Vector2(startX, startY), new Vector2(endX, endY));

    public void DrawCurve(float startX, float startY, float controlX, float controlY, float endX, float endY, bool debug = false)
    {
        DrawCurve(new Vector2(startX, startY), new Vector2(controlX, controlY), new Vector2(endX, endY));
        if (debug)
        {
            DrawLine(new Vector2(startX, startY), new Vector2(controlX, controlY));
            DrawLine(new Vector2(controlX, controlY), new Vector2(endX, endY));
        }
    }

    public void DrawBezier(float startX, float startY, float control1X, float control1Y, float control2X, float control2Y, float endX, float endY, bool debug = false)
    {
        DrawBezier(new Vector2(startX, startY), new Vector2(control1X, control1Y), new Vector2(control2X, control2Y), new Vector2(endX, endY));
        if (debug)
        {
            DrawLine(new Vector2(startX, startY), new Vector2(control1X, control1Y));
            DrawLine(new Vector2(control1X, control1Y), new Vector2(control2X, control2Y));
            DrawLine(new Vector2(control2X, control2Y), new Vector2(endX, endY));
        }
    }

    public void DrawArc(float xCenter, float yCenter, float radius, float startAngle, float sweepAngle, AngleUnit angleUnit = AngleUnit.DEGREES)
        => DrawArc(new Vector2(xCenter, yCenter), radius, startAngle, sweepAngle, angleUnit);

    #endregion

    #region Curves Filled 

    public void FillCurve(float startX, float startY, float controlX, float controlY, float endX, float endY, bool debug = false)
    {
        FillCurve(new Vector2(startX, startY), new Vector2(controlX, controlY), new Vector2(endX, endY));
        if (debug)
        {
            DrawLine(new Vector2(startX, startY), new Vector2(controlX, controlY));
            DrawLine(new Vector2(controlX, controlY), new Vector2(endX, endY));
        }
    }

    public void FillBezier(float startX, float startY, float control1X, float control1Y, float control2X, float control2Y, float endX, float endY, bool debug = false)
    {
        FillBezier(new Vector2(startX, startY), new Vector2(control1X, control1Y), new Vector2(control2X, control2Y), new Vector2(endX, endY));
        if (debug)
        {
            DrawLine(new Vector2(startX, startY), new Vector2(control1X, control1Y));
            DrawLine(new Vector2(control1X, control1Y), new Vector2(control2X, control2Y));
            DrawLine(new Vector2(control2X, control2Y), new Vector2(endX, endY));
        }
    }

    public void FillArc(float xCenter, float yCenter, float radius, float startAngle, float sweepAngle, AngleUnit angleUnit = AngleUnit.DEGREES)
        => FillArc(new Vector2(xCenter, yCenter), radius, startAngle, sweepAngle, angleUnit);

    #endregion

    #region Text

    public void DrawText(float x, float y, string text)
        => DrawText(new Vector2(x, y), text);

    public (float width, float height) MeasureTextTuple(string text)
    {
        var size = MeasureText(text);
        return (size.X, size.Y);
    }

    #endregion

    #region Image

    public void DrawImage(ImageHandle img, float x, float y, bool antialias = true)
        => DrawImage(img, new Vector2(x, y), img.NaturalSize, antialias);

    public void DrawImage(ImageHandle img, float x, float y, float width, float height, bool antialias = true)
        => DrawImage(img, new Vector2(x, y), new Vector2(width, height), antialias);

    public void DrawImage(string filePath, float x, float y, bool antialias = true)
        => DrawImage(filePath, new Vector2(x, y), antialias);

    public void DrawImage(string filePath, float x, float y, float width, float height, bool antialias = true)
        => DrawImage(filePath, new Vector2(x, y), new Vector2(width, height), antialias);

    #endregion
}