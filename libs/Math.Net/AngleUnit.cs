namespace MarcoZechner.MathDotNet;

public enum AngleUnit {
    Degrees,
    Radians,
}

public static class AngleUnitExtensions
{
    public static float ToAngleUnit(this float angle, AngleUnit unit) => unit switch
    {
        AngleUnit.Degrees => angle * MathF.PI / 180f,
        AngleUnit.Radians => angle * 180f / MathF.PI,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
    };
    
    public static float DegreesConversion(this AngleUnit unit) => 180f / MathF.PI;
    public static float RadiansConversion(this AngleUnit unit) => MathF.PI / 180f;
}