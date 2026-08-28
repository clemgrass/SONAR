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

public class HeldFireTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Contact Enemy(int id, Vec2 position, int nextFireTick) => new()
    {
        Id = new ContactId(id),
        Kind = ContactKind.Enemy,
        Position = position,
        Velocity = Vec2.Zero,
        Alive = true,
        NextFireTick = nextFireTick,
    };

    [Fact]
    public void AnEnemyHoldsFireWhenAFriendIsInTheLane()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            Enemy(1, new Vec2(0.0, -0.90), nextFireTick: 0),
            Enemy(2, new Vec2(0.0, -0.50), nextFireTick: int.MaxValue),   // justo en medio
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.DoesNotContain(events, e => e is EnemyTorpedoFired);
        Assert.Contains(events, e => e is FireHeldForFriendly);
    }

    [Fact]
    public void AnEnemyWithAClearLaneFires()
    {
        // Control positivo: el mismo escenario con el compañero fuera de la línea. Sin esto,
        // el test de arriba pasaría igual si los enemigos hubieran dejado de disparar.
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            Enemy(1, new Vec2(0.0, -0.90), nextFireTick: 0),
            Enemy(2, new Vec2(0.50, 0.0), nextFireTick: int.MaxValue),
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.Contains(events, e => e is EnemyTorpedoFired);
        Assert.DoesNotContain(events, e => e is FireHeldForFriendly);
    }

    [Fact]
    public void AFriendlyTorpedoAlsoBlocksTheLane()
    {
        // No sólo los cascos tapan el carril: un torpedo de otro enemigo también.
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { Enemy(1, new Vec2(0.0, -0.90), nextFireTick: 0) });
        sim.SetTorpedoes(new[]
        {
            Torpedo.CreateEnemyToward(new TorpedoId(901), new Vec2(0.0, -0.50),
                new Vec2(0.0, -0.49), 0.0, sourceContactId: 2),
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 60; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.DoesNotContain(events, e => e is EnemyTorpedoFired);
    }

    [Fact]
    public void TheEnemyFiresOnceTheFriendMovesOutOfTheWay()
    {
        // Aguantar no es renunciar: vuelve a mirar cada tick.
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            Enemy(1, new Vec2(0.0, -0.90), nextFireTick: 0),
            Enemy(2, new Vec2(0.0, -0.50), nextFireTick: int.MaxValue),
        });
        for (var i = 0; i < 60; i++) sim.Tick(NoCommands);

        // Se aparta el compañero.
        sim.SetContacts(new[]
        {
            sim.State.Contacts.Single(c => c.Id.Value == 1),
            Enemy(2, new Vec2(0.70, 0.0), nextFireTick: int.MaxValue),
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 60; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.Contains(events, e => e is EnemyTorpedoFired);
    }
}
