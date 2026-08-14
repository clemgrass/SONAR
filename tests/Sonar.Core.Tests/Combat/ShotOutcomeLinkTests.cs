using System;
using System.Collections.Generic;
using System.Linq;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Combat;

public class ShotOutcomeLinkTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Command[] Fire(int bearing, int band) =>
        new Command[] { new FireCommand(new TargetCoordinate(new Bearing(bearing), new Band(band))) };

    [Fact]
    public void DirectHit_NamesTheTorpedoThatCausedIt()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            new Contact
            {
                Id = new ContactId(1), Kind = ContactKind.Enemy,
                Position = new Vec2(0.40, 0.0), Velocity = Vec2.Zero,
                Alive = true, NextFireTick = int.MaxValue,
            },
        });

        var events = new List<GameEvent>(sim.Tick(Fire(90, 2)));
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        var fired = events.OfType<TorpedoFired>().Single();
        var killed = events.OfType<ContactDestroyed>().Single();

        Assert.Equal(fired.TorpedoId, killed.TorpedoId);
    }

    [Fact]
    public void RunOutDetonation_NamesItsTorpedo()
    {
        var sim = TestWorld.Still();

        var events = new List<GameEvent>(sim.Tick(Fire(90, 2)));
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        var fired = events.OfType<TorpedoFired>().Single();
        var detonated = events.OfType<TorpedoDetonated>().Single();

        Assert.Equal(fired.TorpedoId, detonated.TorpedoId);
    }

    [Fact]
    public void DeathsWithoutATorpedo_ReportMinusOne()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            new Contact
            {
                Id = new ContactId(1), Kind = ContactKind.Enemy,
                Position = new Vec2(0.55, 0.0), Velocity = Vec2.Zero,
                Alive = true, NextFireTick = int.MaxValue,
            },
        });

        var events = new List<GameEvent>(sim.Tick(new Command[]
        {
            new DeployDefenseCommand(new TargetCoordinate(new Bearing(90), new Band(2))),
        }));
        for (var i = 0; i < 10; i++) events.AddRange(sim.Tick(NoCommands));

        var killed = events.OfType<ContactDestroyed>().Single();
        Assert.Equal(DestructionCause.Defense, killed.Cause);
        Assert.Equal(-1, killed.TorpedoId);
    }
}
