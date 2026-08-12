using System;
using System.Collections.Generic;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Combat;

public class DirectHitTests
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

    [Fact]
    public void TorpedoOnBearing_DestroysContactOnTheWay()
    {
        var sim = TestWorld.Still();
        // Marcación 90 es este. Contacto en x = 0.40, dentro de la banda C.
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.40, 0.0)) });

        var events = new List<GameEvent>();
        events.AddRange(sim.Tick(Fire(90, 2)));
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        var destroyed = (ContactDestroyed)Assert.Single(events, e => e is ContactDestroyed);
        Assert.Equal(1, destroyed.ContactId);
        Assert.Equal(DestructionCause.DirectHit, destroyed.Cause);
        Assert.Empty(sim.State.Contacts);
    }

    [Fact]
    public void LongerBand_StillDestroysContactOnTheWay()
    {
        // El torpedo choca con lo primero que se le cruce, aunque su corrida sea más larga.
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.40, 0.0)) });

        var events = new List<GameEvent>();
        events.AddRange(sim.Tick(Fire(90, 4)));   // banda E, corrida larga
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.Contains(events, e => e is ContactDestroyed { Cause: DestructionCause.DirectHit });
    }

    [Fact]
    public void InertContact_StopsTheTorpedo()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            Contact.CreateInert(new ContactId(1), new Vec2(0.30, 0.0)),
            EnemyAt(2, new Vec2(0.60, 0.0)),
        });

        var events = new List<GameEvent>();
        events.AddRange(sim.Tick(Fire(90, 4)));
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        // El pecio muere; el enemigo detrás sobrevive.
        var destroyed = (ContactDestroyed)Assert.Single(events, e => e is ContactDestroyed);
        Assert.Equal(1, destroyed.ContactId);
        Assert.Single(sim.State.Contacts);
        Assert.Equal(2, sim.State.Contacts[0].Id.Value);
    }

    [Fact]
    public void TorpedoInsideDeadZone_CannotReachAContactThere()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        // Contacto dentro de la zona muerta: ningún torpedo nace antes de ese radio.
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.05, 0.0)) });

        var events = new List<GameEvent>();
        events.AddRange(sim.Tick(Fire(90, 0)));
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.DoesNotContain(events, e => e is ContactDestroyed);
        Assert.Single(sim.State.Contacts);
    }

    [Fact]
    public void SimultaneousCandidates_LowestContactIdWins()
    {
        var sim = TestWorld.Still();
        // Dos contactos prácticamente en el mismo punto: gana el de id menor (TDD §3.3).
        sim.SetContacts(new[]
        {
            EnemyAt(7, new Vec2(0.40, 0.0)),
            EnemyAt(3, new Vec2(0.40, 0.001)),
        });

        var events = new List<GameEvent>();
        events.AddRange(sim.Tick(Fire(90, 2)));
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        var destroyed = (ContactDestroyed)Assert.Single(events, e => e is ContactDestroyed);
        Assert.Equal(3, destroyed.ContactId);
    }
}
