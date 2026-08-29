using Sonar.Core;

namespace Sonar.Autopilot.Tests.Support;

/// <summary>
/// Config fija para los unitarios. A propósito NO se lee de disco: un unitario que dependa
/// de config/simulation.working.json se pone rojo cada vez que se ajusta el tuning, que es
/// justo el trabajo que M1 pide hacer. Los números son los de agosto de 2026, congelados.
/// </summary>
internal static class TestConfig
{
    public static SimulationConfig Build() => new()
    {
        TickDurationSeconds = 1.0 / 60.0,
        MaxCatchUpTicks = 5,
        DeadZoneRadius = 0.10,
        BandOuterRadii = new[] { 0.28, 0.46, 0.64, 0.82, 1.00 },
        SweepPeriodSeconds = 7.0,
        TorpedoSpeed = 0.043,
        HitRadius = 0.040,
        BlastRadius = 0.100,
        NearMissRadius = 0.160,
        EnemyTorpedoSpeed = 0.03,
        EnemyFireIntervalTicks = 1500,
        EnemyFireStaggerTicks = 300,
        EnemyReloadTicks = 600,
        SubHitRadius = 0.045,
        PassiveHearingRadius = 0.46,
        FriendlyLaneRadius = 0.040,
        DefenseRadius = 0.070,
        DefenseCapacity = 1,
        MagazineSize = 3,
        AmmoReserve = 12,
        ReloadTicks = 180,
    };
}
