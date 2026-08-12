using System;
using System.Collections.Generic;
using System.Linq;
using Sonar.Core.Commands;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Combat;

public class FiringTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Command[] Fire(int bearing, int band) =>
        new Command[] { new FireCommand(new TargetCoordinate(new Bearing(bearing), new Band(band))) };

    [Fact]
    public void Fire_CreatesTorpedoAndSpendsOneRound()
    {
        var sim = TestWorld.Still();
        var events = sim.Tick(Fire(47, 2));

        Assert.Single(sim.State.Torpedoes);
        Assert.Equal(3, sim.State.Sub.AmmoLoaded);
        var fired = (TorpedoFired)Assert.Single(events, e => e is TorpedoFired);
        Assert.Equal(47, fired.BearingDegrees);
        Assert.Equal('C', fired.BandLetter);
    }

    [Fact]
    public void Fire_TorpedoStartsAtDeadZoneEdge()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.Tick(Fire(90, 2));

        // Tras un tick ya avanzó un poco; comprobamos que arrancó fuera de la zona muerta.
        Assert.True(sim.State.Torpedoes[0].Position.Length >= config.DeadZoneRadius);
    }

    [Fact]
    public void Fire_WithEmptyMagazine_IsRejected()
    {
        var sim = TestWorld.Still();
        for (var i = 0; i < 4; i++) sim.Tick(Fire(i * 10, 4));   // gasta el cargador
        Assert.Equal(0, sim.State.Sub.AmmoLoaded);

        var events = sim.Tick(Fire(200, 4));

        var rejected = (FireRejected)Assert.Single(events, e => e is FireRejected);
        Assert.Equal(RejectionReason.NoAmmo, rejected.Reason);
        Assert.DoesNotContain(events, e => e is TorpedoFired);
    }

    [Fact]
    public void Fire_WhileReloading_IsRejected()
    {
        var sim = TestWorld.Still();
        sim.Tick(new Command[] { new ReloadCommand() });

        var events = sim.Tick(Fire(47, 2));

        var rejected = (FireRejected)Assert.Single(events, e => e is FireRejected);
        Assert.Equal(RejectionReason.Reloading, rejected.Reason);
    }

    [Fact]
    public void Fire_AssignsUniqueIncrementingIds()
    {
        var sim = TestWorld.Still();
        sim.Tick(Fire(10, 4));
        sim.Tick(Fire(20, 4));

        var ids = sim.State.Torpedoes.Select(t => t.Id.Value).ToArray();
        Assert.Equal(2, ids.Distinct().Count());
        Assert.True(ids[1] > ids[0]);
    }
}
