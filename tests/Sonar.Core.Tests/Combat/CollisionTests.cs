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

public class CollisionTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Contact ClosingEnemy(int id) => new()
    {
        Id = new ContactId(id),
        Kind = ContactKind.Enemy,
        Position = new Vec2(0.30, 0.0),
        Velocity = new Vec2(-0.30, 0.0),
        Alive = true,
        NextFireTick = int.MaxValue,
    };

    [Fact]
    public void EnemyReachingTheSub_KillsItOutright()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { ClosingEnemy(1) });

        var events = new List<GameEvent>();
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        var dead = (SubDestroyed)Assert.Single(events, e => e is SubDestroyed);
        Assert.Equal(SubDeathCause.Collision, dead.Cause);
        Assert.True(sim.State.Sub.Destroyed);

        // De una: no hacen falta dos impactos.
        Assert.Empty(events.OfType<SubHit>());
    }

    [Fact]
    public void InertContact_PassesThroughWithoutKilling()
    {
        // Un pecio ya está hundido. Misma lógica que la exención en las defensas.
        var sim = TestWorld.SteamingNorth(speed: 0.30);
        sim.SetContacts(new[] { Contact.CreateInert(new ContactId(1), new Vec2(0.0, -0.30)) });

        var events = new List<GameEvent>();
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.DoesNotContain(events, e => e is SubDestroyed);
        Assert.False(sim.State.Sub.Destroyed);

        // Control positivo: pasó de verdad por encima del submarino.
        Assert.True(sim.State.Contacts[0].Position.Y > 0.0,
            "El pecio no llegó a cruzar el origen; el test no probó nada.");
    }

    [Fact]
    public void TorpedoDeath_StillReportsItsOwnCause()
    {
        var config = TestWorld.Config() with { ReloadTicks = 30, EnemyReloadTicks = 30 };
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[]
        {
            new Contact
            {
                Id = new ContactId(1), Kind = ContactKind.Enemy,
                Position = new Vec2(0.0, -0.30), Velocity = Vec2.Zero,
                Alive = true, NextFireTick = 0,
            },
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 400; i++) events.AddRange(sim.Tick(NoCommands));

        var dead = (SubDestroyed)Assert.Single(events, e => e is SubDestroyed);
        Assert.Equal(SubDeathCause.Torpedoes, dead.Cause);
    }
}
