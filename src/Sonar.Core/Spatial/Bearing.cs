namespace Sonar.Core.Spatial;

/// <summary>
/// Marcación en grados enteros, siempre normalizada a [0, 360).
/// Cero es norte (arriba) y crece en sentido horario. En espacio de simulación
/// el norte es (0, -1) — ver TDD §1.
/// </summary>
public readonly record struct Bearing
{
    public int Degrees { get; }

    public Bearing(int degrees) => Degrees = ((degrees % 360) + 360) % 360;

    public Vec2 ToUnitVector()
    {
        var radians = Degrees * System.Math.PI / 180.0;
        return new Vec2(System.Math.Sin(radians), -System.Math.Cos(radians));
    }

    /// <summary>Marcación del vector dado. Un vector cero devuelve marcación 0.</summary>
    public static Bearing FromVector(Vec2 v)
    {
        if (v.X == 0 && v.Y == 0) return new Bearing(0);
        var degrees = System.Math.Atan2(v.X, -v.Y) * 180.0 / System.Math.PI;
        return new Bearing((int)System.Math.Round(degrees));
    }

    public override string ToString() => Degrees.ToString("D3");
}
