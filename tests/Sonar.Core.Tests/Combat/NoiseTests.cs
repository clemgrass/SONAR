using System;
using System.Collections.Generic;
using System.Linq;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Combat;

public class NoiseTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    /// Lejos del radio de escucha pasiva, para que sólo el ruido lo refresque.
    private static Contact FarEnemy(int id) => new()
    {
        Id = new ContactId(id),
        Kind = ContactKind.Enemy,
        Position = new Vec2(0.0, -0.90),
        Velocity = Vec2.Zero,
        Alive = true,
        NextFireTick = int.MaxValue,
        LastFixTick = 0,
        Personality = PersonalityCatalog.Picket,
    };

    [Fact]
    public void SilenceLetsTheFixGoStale()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { FarEnemy(1) });

        for (var i = 0; i < 30; i++) sim.Tick(NoCommands);

        // El sonar arranca corriendo, así que hay que apagarlo para que haya silencio real.
        sim.Tick(new Command[] { new ToggleSonarCommand() });
        var before = sim.State.Contacts[0].LastFixTick;
        for (var i = 0; i < 300; i++) sim.Tick(NoCommands);

        Assert.Equal(before, sim.State.Contacts[0].LastFixTick);
    }

    [Fact]
    public void FiringATorpedoGivesEveryEnemyAFreshFix()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { FarEnemy(1) });
        sim.Tick(new Command[] { new ToggleSonarCommand() });   // apagar el barrido
        for (var i = 0; i < 200; i++) sim.Tick(NoCommands);

        var stale = sim.State.Contacts[0].LastFixTick;

        sim.Tick(new Command[]
        {
            new FireCommand(new TargetCoordinate(new Bearing(90), new Band(3))),
        });
        sim.Tick(NoCommands);

        Assert.True(sim.State.Contacts[0].LastFixTick > stale,
            "disparar tiene que delatar la posición");
    }

    [Fact]
    public void ASweepRevolutionGivesEveryEnemyAFreshFix()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { FarEnemy(1) });

        // Una vuelta completa del haz, más un tick de margen.
        var ticksPerRevolution = (int)Math.Ceiling(config.SweepPeriodSeconds / config.TickDurationSeconds);
        for (var i = 0; i < ticksPerRevolution + 2; i++) sim.Tick(NoCommands);

        Assert.True(sim.State.Contacts[0].LastFixTick > 0,
            "cada vuelta del haz tiene que delatar la posición");
    }

    [Fact]
    public void ProximityRefreshesEvenInTotalSilence()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        // Dentro del radio de escucha pasiva.
        sim.SetContacts(new[]
        {
            FarEnemy(1) with { Position = new Vec2(0.0, -config.PassiveHearingRadius * 0.5) },
        });
        sim.Tick(new Command[] { new ToggleSonarCommand() });

        for (var i = 0; i < 300; i++) sim.Tick(NoCommands);

        Assert.True(sim.State.Contacts[0].LastFixTick >= sim.State.Tick - 2,
            "un enemigo pegado te oye aunque estés callado");
    }
}
