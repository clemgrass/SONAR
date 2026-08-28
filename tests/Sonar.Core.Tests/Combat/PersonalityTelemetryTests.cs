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

public class PersonalityTelemetryTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Contact Enemy(int id, Personality? personality) => new()
    {
        Id = new ContactId(id),
        Kind = ContactKind.Enemy,
        Position = new Vec2(0.30, -0.50),
        Velocity = Vec2.Zero,
        Alive = true,
        NextFireTick = 0,
        LastFixTick = 0,
        Personality = personality,
    };

    [Fact]
    public void EveryEnemyShotCarriesThePersonalityThatFiredIt()
    {
        // Con cinco personalidades y veinte testers, sin esta columna los datos son el
        // promedio de cinco cosas distintas y no responden nada.
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { Enemy(1, PersonalityCatalog.Goad) });

        var events = new List<GameEvent>();
        for (var i = 0; i < 60; i++) events.AddRange(sim.Tick(NoCommands));

        var fired = Assert.Single(events.OfType<EnemyTorpedoFired>());
        Assert.Equal("GOAD", fired.Personality);
    }

    [Fact]
    public void AnEnemyWithoutPersonalityReportsNone()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { Enemy(1, null) });

        var events = new List<GameEvent>();
        for (var i = 0; i < 60; i++) events.AddRange(sim.Tick(NoCommands));

        var fired = Assert.Single(events.OfType<EnemyTorpedoFired>());
        Assert.Null(fired.Personality);
    }

    [Fact]
    public void AKillCarriesThePersonalityOfWhatDied()
    {
        // Para poder preguntar cuál personalidad mata más y cuál muere más rápido, la muerte
        // tiene que decir de quién era el casco.
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { Enemy(1, PersonalityCatalog.Picket) with { NextFireTick = int.MaxValue } });

        var events = new List<GameEvent>();
        events.AddRange(sim.Tick(new Command[]
        {
            new FireCommand(new TargetCoordinate(new Bearing(31), new Band(3))),
        }));
        for (var i = 0; i < 300; i++) events.AddRange(sim.Tick(NoCommands));

        var killed = Assert.Single(events.OfType<ContactDestroyed>());
        Assert.Equal("PICKET", killed.Personality);
    }
}
