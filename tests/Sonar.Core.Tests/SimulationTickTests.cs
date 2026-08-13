using System;
using System.Collections.Generic;
using Sonar.Core;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests;

public class SimulationTickTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    [Fact]
    public void Tick_AdvancesTheTickCounter()
    {
        var sim = TestWorld.Still();
        sim.Tick(NoCommands);
        sim.Tick(NoCommands);
        Assert.Equal(2, sim.State.Tick);
    }

    [Fact]
    public void Tick_AdvancesTheSweep()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);

        // Un período completo debe devolver el haz cerca de cero.
        var ticksPerSweep = (int)Math.Round(config.SweepPeriodSeconds / config.TickDurationSeconds);
        for (var i = 0; i < ticksPerSweep; i++) sim.Tick(NoCommands);

        Assert.InRange(sim.State.Sonar.SweepDegrees, 0.0, 1.0);
    }

    [Fact]
    public void Tick_DoesNotAdvanceSweepWhenStopped()
    {
        var sim = TestWorld.Still();
        sim.Tick(new Command[] { new ToggleSonarCommand() });
        var frozen = sim.State.Sonar.SweepDegrees;

        for (var i = 0; i < 100; i++) sim.Tick(NoCommands);

        Assert.False(sim.State.Sonar.SweepRunning);
        Assert.Equal(frozen, sim.State.Sonar.SweepDegrees, precision: 10);
    }

    [Fact]
    public void ToggleSonar_EmitsSonarToggledEvent()
    {
        var sim = TestWorld.Still();
        var events = sim.Tick(new Command[] { new ToggleSonarCommand() });

        var toggled = Assert.Single(events, e => e is SonarToggled);
        Assert.False(((SonarToggled)toggled).Running);
    }

    [Fact]
    public void UnrecognizedInput_EmitsEventAndChangesNothingElse()
    {
        var sim = TestWorld.Still();
        var before = sim.State.Sub;

        var events = sim.Tick(new Command[] { new UnrecognizedInput("hello") });

        var evt = Assert.Single(events, e => e is InputNotRecognized);
        Assert.Equal("hello", ((InputNotRecognized)evt).RawText);
        Assert.Equal(before, sim.State.Sub);
    }

    [Fact]
    public void StatusCommand_ReportsCurrentResources()
    {
        var sim = TestWorld.Still();
        var events = sim.Tick(new Command[] { new StatusCommand() });

        var report = (StatusReported)Assert.Single(events, e => e is StatusReported);
        Assert.Equal(4, report.AmmoLoaded);
        Assert.Equal(12, report.AmmoReserve);
        Assert.Equal(3, report.Defenses);
    }

    [Fact]
    public void EnemyContact_MovesByItsOwnVelocity()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[]
        {
            new Contact
            {
                Id = new ContactId(1),
                Kind = ContactKind.Enemy,
                // Y=0.1 en vez de 0: a Y=0 la trayectoria cruza el radio de colisión del
                // submarino (0.045) y la muerte por embestida congela la simulación antes
                // de los 60 ticks. Con este desvío el contacto pasa de largo y el test mide
                // lo que dice medir: movimiento por velocidad propia, no combate.
                Position = new Vec2(0.5, 0.1),
                Velocity = new Vec2(-0.6, 0.0),   // acercándose al oeste
                Alive = true,
                NextFireTick = int.MaxValue,
            },
        });

        for (var i = 0; i < 60; i++) sim.Tick(NoCommands);

        // 60 ticks = 1 segundo a -0.6 por segundo.
        Assert.Equal(0.5 - 0.6, sim.State.Contacts[0].Position.X, precision: 6);
    }

    [Fact]
    public void OwnTorpedo_MovesAlongItsBearing()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        var layout = config.BuildBandLayout();
        sim.SetTorpedoes(new[]
        {
            Torpedo.CreateOwn(new TorpedoId(1),
                new TargetCoordinate(new Bearing(90), new Band(4)), layout, config.TorpedoSpeed),
        });

        for (var i = 0; i < 60; i++) sim.Tick(NoCommands);

        // Nace en x = 0.10 y avanza al este a 0.70 por segundo.
        Assert.Equal(0.10 + 0.70, sim.State.Torpedoes[0].Position.X, precision: 6);
        Assert.Equal(0.0, sim.State.Torpedoes[0].Position.Y, precision: 6);
    }
}
