using System;
using System.Collections.Generic;
using Sonar.Core.Commands;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Combat;

public class ReloadTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Command[] Fire(int bearing, int band) =>
        new Command[] { new FireCommand(new TargetCoordinate(new Bearing(bearing), new Band(band))) };

    [Fact]
    public void Reload_LoadsOneTorpedoPerCycle()
    {
        // No llena el cargador: carga un tubo. Quedarse en cero cuesta una recarga por
        // torpedo, así que gastar el último es una decisión y no un trámite.
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        for (var i = 0; i < config.MagazineSize; i++) sim.Tick(Fire(i * 20, 4));
        Assert.Equal(0, sim.State.Sub.AmmoLoaded);

        var events = new List<GameEvent>(sim.Tick(new Command[] { new ReloadCommand() }));
        Assert.Contains(events, e => e is ReloadStarted);

        for (var i = 0; i < config.ReloadTicks; i++) events.AddRange(sim.Tick(NoCommands));

        var completed = (ReloadCompleted)Assert.Single(events, e => e is ReloadCompleted);
        Assert.Equal(1, completed.AmmoLoaded);
        Assert.Equal(1, sim.State.Sub.AmmoLoaded);
        Assert.Equal(config.AmmoReserve - 1, sim.State.Sub.AmmoReserve);
        Assert.False(sim.State.Sub.ReloadInProgress);
    }

    [Fact]
    public void Reload_RepeatedFillsTheMagazineOneAtATime()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        for (var i = 0; i < config.MagazineSize; i++) sim.Tick(Fire(i * 20, 4));

        for (var round = 1; round <= config.MagazineSize; round++)
        {
            sim.Tick(new Command[] { new ReloadCommand() });
            for (var i = 0; i < config.ReloadTicks; i++) sim.Tick(NoCommands);
            Assert.Equal(round, sim.State.Sub.AmmoLoaded);
        }

        // Con el cargador lleno, recargar de nuevo no consume reserva.
        var reserve = sim.State.Sub.AmmoReserve;
        sim.Tick(new Command[] { new ReloadCommand() });
        for (var i = 0; i < config.ReloadTicks; i++) sim.Tick(NoCommands);

        Assert.Equal(config.MagazineSize, sim.State.Sub.AmmoLoaded);
        Assert.Equal(reserve, sim.State.Sub.AmmoReserve);
    }

    [Fact]
    public void Reload_TakesTheFullConfiguredTime()
    {
        // Un tick antes del plazo la recarga sigue en curso. Sin esto, una implementación
        // que completara de inmediato pasaría el test de arriba igual.
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);

        var events = new List<GameEvent>(sim.Tick(new Command[] { new ReloadCommand() }));
        for (var i = 0; i < config.ReloadTicks - 1; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.DoesNotContain(events, e => e is ReloadCompleted);
        Assert.True(sim.State.Sub.ReloadInProgress);
    }

    [Fact]
    public void Reload_DoesNotPauseTheWorld()
    {
        // El §23 del GDD: la recarga ocurre en tiempo real. El barrido sigue girando.
        var sim = TestWorld.Still();
        sim.Tick(new Command[] { new ReloadCommand() });
        var before = sim.State.Sonar.SweepDegrees;

        for (var i = 0; i < 30; i++) sim.Tick(NoCommands);

        Assert.NotEqual(before, sim.State.Sonar.SweepDegrees);
    }

    [Fact]
    public void Reload_WithEmptyReserve_DoesNothing()
    {
        var config = TestWorld.Config() with { AmmoReserve = 0 };
        var sim = TestWorld.Still(config);

        var events = sim.Tick(new Command[] { new ReloadCommand() });

        Assert.DoesNotContain(events, e => e is ReloadStarted);
        Assert.False(sim.State.Sub.ReloadInProgress);
    }

    [Fact]
    public void Reload_TopsUpPartialMagazine()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.Tick(Fire(10, 4));   // quedan 3 de 4

        var events = new List<GameEvent>(sim.Tick(new Command[] { new ReloadCommand() }));
        for (var i = 0; i < config.ReloadTicks; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.Equal(4, sim.State.Sub.AmmoLoaded);
        Assert.Equal(11, sim.State.Sub.AmmoReserve);   // solo consumió uno
    }

    [Fact]
    public void Reload_CannotBeStartedTwice()
    {
        var sim = TestWorld.Still();
        sim.Tick(new Command[] { new ReloadCommand() });
        var events = sim.Tick(new Command[] { new ReloadCommand() });

        Assert.DoesNotContain(events, e => e is ReloadStarted);
    }
}
