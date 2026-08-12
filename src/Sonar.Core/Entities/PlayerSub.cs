using Sonar.Core.Spatial;

namespace Sonar.Core.Entities;

/// <summary>
/// El submarino del jugador está siempre en el origen (0,0) y nunca se mueve en espacio
/// de simulación. Course y Speed solo sirven para calcular la deriva del mundo.
/// </summary>
public sealed record PlayerSub
{
    public required int AmmoLoaded { get; init; }
    public required int AmmoReserve { get; init; }
    public required int DefensesAvailable { get; init; }
    public required bool ReloadInProgress { get; init; }
    public required int ReloadCompletesAtTick { get; init; }
    public required Bearing Course { get; init; }
    public required double Speed { get; init; }

    /// <summary>Impactos recibidos. Al llegar a 2 termina la corrida (§20 del GDD).</summary>
    public required int HitsTaken { get; init; }

    public required bool Destroyed { get; init; }

    public static PlayerSub Create(SimulationConfig config, Bearing course, double speed) => new()
    {
        AmmoLoaded = config.MagazineSize,
        AmmoReserve = config.AmmoReserve,
        DefensesAvailable = config.DefenseCapacity,
        ReloadInProgress = false,
        ReloadCompletesAtTick = 0,
        Course = course,
        Speed = speed,
        HitsTaken = 0,
        Destroyed = false,
    };
}
