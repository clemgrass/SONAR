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

namespace Sonar.Core.Tests.Sensing;

public class SweepRevolutionTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static int TicksPerSweep(SimulationConfig config) =>
        (int)Math.Round(config.SweepPeriodSeconds / config.TickDurationSeconds);

    [Fact]
    public void OneEventPerRevolution()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);

        var events = new List<GameEvent>();
        for (var i = 0; i < TicksPerSweep(config) * 3; i++) events.AddRange(sim.Tick(NoCommands));

        var laps = events.OfType<SweepCompleted>().ToArray();
        Assert.Equal(3, laps.Length);
        Assert.Equal(new[] { 1, 2, 3 }, laps.Select(l => l.Revolution).ToArray());
    }

    [Fact]
    public void CarriesHowManyContactsAreOnTheBoard()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[]
        {
            Contact.CreateInert(new ContactId(1), new Vec2(0.5, 0.0)),
            Contact.CreateInert(new ContactId(2), new Vec2(0.0, 0.5)),
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < TicksPerSweep(config) * 2; i++) events.AddRange(sim.Tick(NoCommands));

        // A la segunda vuelta ya barrió a los dos.
        Assert.Equal(2, events.OfType<SweepCompleted>().Last().ReadingCount);
    }

    [Fact]
    public void StoppedSweep_CompletesNoRevolutions()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.Tick(new Command[] { new ToggleSonarCommand() });

        var events = new List<GameEvent>();
        for (var i = 0; i < TicksPerSweep(config) * 3; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.Empty(events.OfType<SweepCompleted>());

        // Control positivo: con el haz andando los mismos ticks sí dan vueltas. Sin esto el
        // test pasaría igual si el evento no se emitiera nunca.
        var running = TestWorld.Still(config);
        var runningEvents = new List<GameEvent>();
        for (var i = 0; i < TicksPerSweep(config) * 3; i++)
            runningEvents.AddRange(running.Tick(NoCommands));

        Assert.NotEmpty(runningEvents.OfType<SweepCompleted>());
    }
}
