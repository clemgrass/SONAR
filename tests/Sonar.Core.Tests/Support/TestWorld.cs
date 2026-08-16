using Sonar.Core;
using Sonar.Core.Spatial;

namespace Sonar.Core.Tests.Support;

public static class TestWorld
{
    /// Configuración de trabajo válida. Los valores definitivos salen de M1.
    public static SimulationConfig Config() => new()
    {
        TickDurationSeconds = 1.0 / 60.0,
        MaxCatchUpTicks = 5,
        DeadZoneRadius = 0.10,
        BandOuterRadii = new[] { 0.28, 0.46, 0.64, 0.82, 1.00 },
        SweepPeriodSeconds = 3.5,
        TorpedoSpeed = 0.70,
        HitRadius = 0.040,
        BlastRadius = 0.100,
        NearMissRadius = 0.160,
        EnemyTorpedoSpeed = 0.45,
        EnemyFireIntervalTicks = 420,
        EnemyFireStaggerTicks = 70,
        SubHitRadius = 0.045,
        PassiveHearingRadius = 0.46,
        DefenseRadius = 0.070,
        DefenseCapacity = 3,
        MagazineSize = 4,
        AmmoReserve = 12,
        ReloadTicks = 300,
        EnemyReloadTicks = 600,
    };

    /// Submarino inmóvil: sin deriva del mundo. Útil para aislar movimiento propio.
    public static Simulation Still(SimulationConfig? config = null)
    {
        var c = config ?? Config();
        return new Simulation(c, new SeededRandomSource(1),
            WorldState.CreateInitial(c, new Bearing(0), speed: 0.0));
    }

    /// Submarino navegando al norte. Produce deriva del mundo hacia el sur.
    public static Simulation SteamingNorth(double speed = 0.10, SimulationConfig? config = null)
    {
        var c = config ?? Config();
        return new Simulation(c, new SeededRandomSource(1),
            WorldState.CreateInitial(c, new Bearing(0), speed));
    }

    /// <summary>
    /// Como Still() pero con el submarino avanzando, que es lo que hace que la deriva del
    /// mundo sea distinta de cero. Sin deriva no hay envejecimiento que medir.
    /// </summary>
    public static Simulation Moving(SimulationConfig? config = null)
    {
        var c = config ?? Config();
        return new Simulation(c, new SeededRandomSource(1),
            WorldState.CreateInitial(c, new Bearing(0), speed: 0.012));
    }
}
