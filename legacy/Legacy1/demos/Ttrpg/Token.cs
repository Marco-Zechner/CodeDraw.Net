using MarcoZechner.ColorDotNet;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.Ttrpg;

public class Token
{

    public static float globalScale = 0.1f;

    public Vector2<double> Position;
    public float Size = 1.0f;
    public float Rotation = 0.0f; // in degrees
    public Color Color = Color.PINK;
    public bool VisibleToPlayers = false;
    public string Name = "Token";
    public int FontSize = 24;

    public TokenStatus Status = TokenStatus.Alive;
    public Token(Vector2<double> position)
    {
        Position = position;
    }

    public Color StatusColor => Status switch
    {
        TokenStatus.Unused => Color.GRAY,
        TokenStatus.Alive => Color.GREEN,
        TokenStatus.Dead => Color.RED,
        _ => Color.BLACK,
    };

    public bool MouseHovering => this == TokenFactory.MouseOverToken;

    public bool IsMouseOver(Vector2<double> mousePos)
    {
        float r = Size * globalScale;
        var dist = (Position - mousePos).Length;
        return dist <= r;
    }
}

public enum TokenStatus
{
    Unused,
    Alive,
    Dead,
}