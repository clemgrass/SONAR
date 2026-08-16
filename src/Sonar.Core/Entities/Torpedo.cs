using Sonar.Core.Spatial;

namespace Sonar.Core.Entities;

public enum TorpedoSide { Own, Enemy }

/// <summary>
/// Se dice torpedo, no misil, para ambos bandos: los misiles lanzados desde submarino
/// no existen hasta los años cincuenta y el §4 del GDD pide que la época se infiera de
/// la terminología.
/// </summary>
public sealed record Torpedo
{
    public required TorpedoId Id { get; init; }
    public required TorpedoSide Side { get; init; }
    public required Vec2 Position { get; init; }

    /// <summary>Dirección unitaria de viaje.</summary>
    public required Vec2 Direction { get; init; }

    public required double Speed { get; init; }

    // --- Solo Own. Para Enemy valen cero y no se consultan. ---
    public required Bearing Bearing { get; init; }
    public required double ArmedFromRadius { get; init; }
    public required double RunToRadius { get; init; }

    /// <summary>
    /// Contacto que lo lanzó, o -1 si es propio. Existe para poder responder "¿este enemigo
    /// ya tiene un torpedo en el agua?", que es lo que le impide disparar otro.
    /// </summary>
    public int SourceContactId { get; init; } = -1;

    public static Torpedo CreateOwn(TorpedoId id, TargetCoordinate target, BandLayout layout, double speed)
    {
        var direction = target.Bearing.ToUnitVector();
        return new Torpedo
        {
            Id = id,
            Side = TorpedoSide.Own,
            Position = direction * layout.DeadZoneRadius,
            Direction = direction,
            Speed = speed,
            Bearing = target.Bearing,
            ArmedFromRadius = layout.DeadZoneRadius,
            RunToRadius = layout.CenterRadius(target.Band),
        };
    }

    /// <summary>
    /// Torpedo enemigo: viaja recto hacia el origen desde donde nació. El submarino está
    /// fijo en el origen, así que no existe la distinción entre dónde está y dónde estará.
    /// </summary>
    public static Torpedo CreateEnemy(
        TorpedoId id, Vec2 origin, double speed, int sourceContactId = -1) => new()
    {
        Id = id,
        Side = TorpedoSide.Enemy,
        Position = origin,
        Direction = (Vec2.Zero - origin).Normalized(),
        Speed = speed,
        Bearing = new Bearing(0),
        ArmedFromRadius = 0,
        RunToRadius = 0,
        SourceContactId = sourceContactId,
    };

    /// <summary>
    /// Torpedo enemigo dirigido a un punto que no es necesariamente el origen: la
    /// estimación envejecida que el enemigo tiene del jugador. Si el punto coincide con el
    /// origen, es idéntico a CreateEnemy.
    /// </summary>
    public static Torpedo CreateEnemyToward(
        TorpedoId id, Vec2 origin, Vec2 target, double speed, int sourceContactId = -1) => new()
    {
        Id = id,
        Side = TorpedoSide.Enemy,
        Position = origin,
        Direction = (target - origin).Normalized(),
        Speed = speed,
        Bearing = new Bearing(0),
        ArmedFromRadius = 0,
        RunToRadius = 0,
        SourceContactId = sourceContactId,
    };
}
