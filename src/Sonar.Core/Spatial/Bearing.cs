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

    /// <summary>
    /// Marcación exacta del vector, sin redondear a grados enteros. Existe para medir error
    /// de puntería: FromVector redondea, y redondear el error a un grado borra buena parte
    /// de lo que se quiere observar.
    /// </summary>
    public static double DegreesOf(Vec2 v)
    {
        if (v.X == 0 && v.Y == 0) return 0.0;
        var degrees = System.Math.Atan2(v.X, -v.Y) * 180.0 / System.Math.PI;
        return (degrees + 360.0) % 360.0;
    }

    /// <summary>Diferencia angular por el camino corto, en [0, 180].</summary>
    public static double Separation(double a, double b)
    {
        var d = System.Math.Abs(a - b) % 360.0;
        return d > 180.0 ? 360.0 - d : d;
    }

    public override string ToString() => Degrees.ToString("D3");
}
