using System;
using Sonar.Core;
using Xunit;

namespace Sonar.Core.Tests;

public class SimulationConfigTests
{
    /// Configuración de trabajo válida. Los valores definitivos salen de M1.
    private static SimulationConfig Valid() => new()
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

    [Fact]
    public void Validate_AcceptsWorkingConfiguration()
    {
        Valid().Validate();   // no debe lanzar
    }

    [Fact]
    public void Validate_RejectsHitRadiusSmallerThanPerTickTravel()
    {
        // 0.70 * (1/60) ≈ 0.01167 — un HitRadius menor permite atravesar contactos.
        var config = Valid() with { HitRadius = 0.005 };
        var ex = Assert.Throws<ArgumentException>(config.Validate);
        Assert.Contains("HitRadius", ex.Message);
    }

    [Fact]
    public void Validate_RejectsSubHitRadiusSmallerThanEnemyPerTickTravel()
    {
        var config = Valid() with { SubHitRadius = 0.003 };
        var ex = Assert.Throws<ArgumentException>(config.Validate);
        Assert.Contains("SubHitRadius", ex.Message);
    }

    [Fact]
    public void Validate_RejectsDefenseRadiusSmallerThanEnemyPerTickTravel()
    {
        var config = Valid() with { DefenseRadius = 0.004 };
        var ex = Assert.Throws<ArgumentException>(config.Validate);
        Assert.Contains("DefenseRadius", ex.Message);
    }

    [Fact]
    public void Validate_RejectsBlastRadiusSmallerThanHitRadius()
    {
        var config = Valid() with { BlastRadius = 0.020 };
        Assert.Throws<ArgumentException>(config.Validate);
    }

    [Fact]
    public void Validate_RejectsNearMissRadiusNotGreaterThanBlastRadius()
    {
        var config = Valid() with { NearMissRadius = 0.100 };
        Assert.Throws<ArgumentException>(config.Validate);
    }

    [Fact]
    public void Validate_RejectsSubHitRadiusOutsideDeadZone()
    {
        // El submarino debe ser alcanzable dentro de la zona donde sus propios
        // torpedos no pueden estar armados.
        var config = Valid() with { SubHitRadius = 0.20 };
        Assert.Throws<ArgumentException>(config.Validate);
    }

    [Fact]
    public void Validate_RejectsPassiveHearingRadiusAtOrBelowDeadZone()
    {
        // Todo lo que está lo bastante cerca como para matar por embestida tiene que
        // estar lo bastante cerca como para oír; si no, un enemigo encima del casco
        // podría seguir sordo.
        var below = Valid() with { PassiveHearingRadius = 0.05 };
        Assert.Throws<ArgumentException>(below.Validate);

        // Caso límite: igual al radio de zona muerta también debe fallar, el chequeo es <=.
        var equal = Valid() with { PassiveHearingRadius = Valid().DeadZoneRadius };
        Assert.Throws<ArgumentException>(equal.Validate);
    }

    [Fact]
    public void Validate_RejectsNonIncreasingBandRadii()
    {
        var config = Valid() with { BandOuterRadii = new[] { 0.28, 0.20, 0.64 } };
        Assert.Throws<ArgumentException>(config.Validate);
    }

    [Fact]
    public void Validate_RejectsNonPositiveFireInterval()
    {
        var config = Valid() with { EnemyFireIntervalTicks = 0 };
        Assert.Throws<ArgumentException>(config.Validate);
    }

    [Fact]
    public void Validate_RejectsNonPositiveFireStagger()
    {
        // Un stagger de cero recrea en silencio la andanada simultánea: todo el
        // escalonamiento de NextFireTick colapsa al mismo tick.
        var config = Valid() with { EnemyFireStaggerTicks = 0 };
        Assert.Throws<ArgumentException>(config.Validate);
    }

    [Fact]
    public void Validate_RejectsNonPositiveEnemyReload()
    {
        var config = Valid() with { EnemyReloadTicks = 0 };
        Assert.Throws<ArgumentException>(config.Validate);
    }

    [Fact]
    public void Validate_RejectsAnEnemyThatCyclesFasterThanThePlayer()
    {
        // El stunlock que motivó el nerfeo vuelve solo si alguien sube la recarga del
        // jugador y se olvida de la enemiga. Que reviente al arrancar, no en la partida.
        var config = Valid() with { ReloadTicks = 300, EnemyReloadTicks = 299 };
        Assert.Throws<ArgumentException>(config.Validate);

        // Igualarlos ya es legal: el piso es "nunca más rápido", no "siempre más lento".
        (Valid() with { ReloadTicks = 300, EnemyReloadTicks = 300 }).Validate();
    }

    [Fact]
    public void BandLayout_IsBuiltFromConfiguredRadii()
    {
        var layout = Valid().BuildBandLayout();
        Assert.Equal(5, layout.Count);
        Assert.Equal(0.10, layout.DeadZoneRadius, precision: 10);
    }
}
