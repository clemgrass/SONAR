using Sonar.Core.Spatial;

namespace Sonar.Core.Entities;

/// <summary>
/// Defensa desplegada en una coordenada. Su posición NO recibe la deriva del mundo:
/// esa omisión deliberada es toda la implementación de la anomalía del §25 del GDD.
/// No agregarle deriva creyendo que es un bug — ver TDD §4.4.
/// </summary>
public sealed record Defense
{
    public required DefenseId Id { get; init; }
    public required Vec2 Position { get; init; }
    public required bool Alive { get; init; }

    public static Defense Create(DefenseId id, Vec2 position) => new()
    {
        Id = id,
        Position = position,
        Alive = true,
    };
}
