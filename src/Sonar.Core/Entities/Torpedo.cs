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
    public static Torpedo CreateEnemy(TorpedoId id, Vec2 origin, double speed) => new()
    {
        Id = id,
        Side = TorpedoSide.Enemy,
        Position = origin,
        Direction = (Vec2.Zero - origin).Normalized(),
        Speed = speed,
        Bearing = new Bearing(0),
        ArmedFromRadius = 0,
        RunToRadius = 0,
    };
}
