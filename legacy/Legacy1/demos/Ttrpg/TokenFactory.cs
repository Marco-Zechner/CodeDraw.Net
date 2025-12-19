using MarcoZechner.ColorDotNet;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.Ttrpg;
public static class TokenFactory
{
    public static readonly List<Token> Tokens = [];
    public static Token? MouseOverToken = null;


    public static Token CreateToken(Vector2<double> position, Color color, float size = 1.0f, bool visibleToPlayers = false, TokenStatus status = TokenStatus.ALIVE, string? name = null)
    {
        var token = new Token(position)
        {
            Color = color,
            Size = size,
            VisibleToPlayers = visibleToPlayers,
            Status = status,
            Name = name ?? $"T{Tokens.Count + 1}"
        };
        Tokens.Add(token);
        return token;
    }

    public static void DeleteToken(Token token)
    {
        Tokens.Remove(token);
    }

    public static void TriggerTokenEvents(Vector2<double> mousePosOnMap)
    {
        for (int i = Tokens.Count - 1; i >= 0; i--)
        {
            Token? token = Tokens[i];
            if (token.IsMouseOver(mousePosOnMap))
            {
                MouseOverToken = token;
                return;
            }
        }
        MouseOverToken = null;
    }
}