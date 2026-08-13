using System;
using System.Collections.Generic;
using System.Linq;
using Sonar.Core;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Combat;

public class ShotAssessmentTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Contact EnemyAt(int id, Vec2 position) => new()
    {
        Id = new ContactId(id),
        Kind = ContactKind.Enemy,
        Position = position,
        Velocity = Vec2.Zero,
        Alive = true,
        NextFireTick = int.MaxValue,
    };

    private static Command[] Fire(int bearing, int band) =>
        new Command[] { new FireCommand(new TargetCoordinate(new Bearing(bearing), new Band(band))) };

    private static int TicksPerSweep(SimulationConfig config) =>
        (int)Math.Round(config.SweepPeriodSeconds / config.TickDurationSeconds);

    [Fact]
    public void MeasuresAngularErrorAgainstTheNearestContact()
    {
        var sim = TestWorld.Still();
        // Contacto exactamente en marcación 090, banda C.
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.55, 0.0)) });

        var events = new List<GameEvent>(sim.Tick(Fire(100, 2)));

        var shot = (ShotAssessed)Assert.Single(events, e => e is ShotAssessed);
        Assert.Equal(1, shot.NearestContactId);
        Assert.Equal(10.0, shot.ErrorDegrees, precision: 6);
        Assert.Equal(0.0, shot.ErrorRadius, precision: 6);
    }

    [Fact]
    public void MeasuresRadialErrorAgainstTheBandCentre()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        // Contacto en marcación 090 a radio 0.70. El centro de la banda C está en 0.55.
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.70, 0.0)) });

        var events = new List<GameEvent>(sim.Tick(Fire(90, 2)));

        var shot = (ShotAssessed)Assert.Single(events, e => e is ShotAssessed);
        Assert.Equal(0.0, shot.ErrorDegrees, precision: 6);
        Assert.Equal(0.15, shot.ErrorRadius, precision: 6);
    }

    [Fact]
    public void PicksTheContactClosestInBearing()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            EnemyAt(1, new Vec2(0.0, -0.55)),   // marcación 000
            EnemyAt(2, new Vec2(0.55, 0.0)),    // marcación 090
        });

        var events = new List<GameEvent>(sim.Tick(Fire(80, 2)));

        var shot = (ShotAssessed)Assert.Single(events, e => e is ShotAssessed);
        Assert.Equal(2, shot.NearestContactId);
        Assert.Equal(10.0, shot.ErrorDegrees, precision: 6);
    }

    [Fact]
    public void CarriesTheAgeOfTheReadingTheShooterCouldSee()
    {
        // La edad de la lectura es la otra mitad de la curva de extrapolación: sin ella no
        // se puede preguntar si la precisión se degrada conforme la marca envejece.
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.55, 0.0)) });

        // Deja pasar una vuelta larga para que la lectura tenga edad medible.
        for (var i = 0; i < TicksPerSweep(config) + 60; i++) sim.Tick(NoCommands);
        Assert.Single(sim.State.Sonar.Readings);

        var events = new List<GameEvent>(sim.Tick(Fire(90, 2)));
        var shot = (ShotAssessed)Assert.Single(events, e => e is ShotAssessed);

        Assert.True(shot.ReadingAgeTicks > 0,
            $"La edad de lectura no se registró: {shot.ReadingAgeTicks}");
        Assert.True(shot.ReadingAgeTicks < TicksPerSweep(config),
            "La edad no puede superar una vuelta del haz.");
    }

    [Fact]
    public void NeverReadContact_ReportsAgeMinusOne()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.55, 0.0)) });

        // Tick 0: el haz todavía no barrió nada.
        var events = new List<GameEvent>(sim.Tick(Fire(90, 2)));

        var shot = (ShotAssessed)Assert.Single(events, e => e is ShotAssessed);
        Assert.Equal(-1, shot.ReadingAgeTicks);
    }

    [Fact]
    public void NoContacts_NoAssessment()
    {
        // Disparar al vacío no tiene contra qué medirse. Se omite en vez de inventar un
        // error sentinela que después contamine los promedios.
        var sim = TestWorld.Still();

        var events = new List<GameEvent>(sim.Tick(Fire(90, 2)));

        Assert.Contains(events, e => e is TorpedoFired);
        Assert.DoesNotContain(events, e => e is ShotAssessed);
    }

    [Fact]
    public void RejectedShot_IsNotAssessed()
    {
        var config = TestWorld.Config() with { MagazineSize = 1 };
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.55, 0.0)) });

        sim.Tick(Fire(90, 2));
        var events = new List<GameEvent>(sim.Tick(Fire(90, 2)));

        Assert.Contains(events, e => e is FireRejected);
        Assert.DoesNotContain(events, e => e is ShotAssessed);
    }
}
