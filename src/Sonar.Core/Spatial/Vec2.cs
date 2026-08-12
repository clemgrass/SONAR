namespace Sonar.Core.Spatial;

/// <summary>
/// Vector 2D de doble precisión. Existe para que Sonar.Core no dependa de Godot.
/// Nunca sustituir por Godot.Vector2: Core no referencia Godot (ver TDD §5.1 del spec).
/// </summary>
public readonly record struct Vec2(double X, double Y)
{
    public static readonly Vec2 Zero = new(0, 0);

    public double Length => System.Math.Sqrt(X * X + Y * Y);

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double scalar) => new(a.X * scalar, a.Y * scalar);

    public static double Distance(Vec2 a, Vec2 b) => (a - b).Length;

    /// <summary>Devuelve Zero si el vector es de longitud cero, en lugar de NaN.</summary>
    public Vec2 Normalized()
    {
        var length = Length;
        return length == 0 ? Zero : new Vec2(X / length, Y / length);
    }
}
