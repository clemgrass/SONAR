using System;
using System.Collections.Generic;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Combat;

public class SteeringTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Contact At(double radius, Personality? personality) => new()
    {
        Id = new ContactId(1),
        Kind = ContactKind.Enemy,
        Position = new Vec2(0.0, -radius),
        Velocity = Vec2.Zero,
        Alive = true,
        NextFireTick = int.MaxValue,
        LastFixTick = 0,
        Personality = personality,
    };

    [Fact]
    public void AContactWithoutPersonalityKeepsItsFixedVelocity()
    {
        // Esto es lo que sostiene todos los tests anteriores: sin personalidad, balístico.
        var sim = TestWorld.Still();
        var velocity = new Vec2(0.01, 0.02);
        sim.SetContacts(new[] { At(0.9, personality: null) with { Velocity = velocity } });

        for (var i = 0; i < 60; i++) sim.Tick(NoCommands);

        Assert.Equal(velocity.X, sim.State.Contacts[0].Velocity.X, precision: 9);
        Assert.Equal(velocity.Y, sim.State.Contacts[0].Velocity.Y, precision: 9);
    }

    [Fact]
    public void ThePicketHoldsTheOuterBand()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { At(0.99, PersonalityCatalog.Picket) });

        for (var i = 0; i < 1800; i++) sim.Tick(NoCommands);

        var radius = sim.State.Contacts[0].Position.Length;
        Assert.InRange(radius, 0.84, 0.96);   // se quedó en la banda E
    }

    [Fact]
    public void TheRamClosesAllTheWayIn()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { At(0.99, PersonalityCatalog.Ram) });

        var before = sim.State.Contacts[0].Position.Length;
        for (var i = 0; i < 600; i++)
        {
            sim.Tick(NoCommands);
            if (sim.State.Contacts.Count == 0) break;   // llegó y mató
        }

        if (sim.State.Contacts.Count > 0)
            Assert.True(sim.State.Contacts[0].Position.Length < before - 0.05,
                "el RAM tiene que estar cerrando distancia");
        else
            Assert.True(sim.State.Sub.Destroyed, "si desapareció fue porque llegó");
    }

    [Fact]
    public void TheStalkerWithdrawsWhenItHasNoTorpedoLeft()
    {
        // El ciclo de acecho: cargado se acerca, vacío se abre. Se simula "vacío" poniéndole
        // un torpedo suyo en el agua, que es lo que la regla mira.
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { At(0.55, PersonalityCatalog.Stalker) });
        // El torpedo va hacia AFUERA a propósito: uno dirigido al origen impactaría sobre
        // el submarino dentro de la ventana del test, desaparecería, y el STALKER volvería a
        // contarse como cargado a mitad de la medición. Lo único que la regla mira es que
        // exista un torpedo suyo en el agua, no dónde esté.
        sim.SetTorpedoes(new[]
        {
            Torpedo.CreateEnemyToward(new TorpedoId(900), new Vec2(0.50, 0.0),
                new Vec2(1.00, 0.0), 0.03, sourceContactId: 1),
        });

        var before = sim.State.Contacts[0].Position.Length;
        for (var i = 0; i < 600; i++) sim.Tick(NoCommands);

        Assert.True(sim.State.Contacts[0].Position.Length > before,
            "con su torpedo en el agua el STALKER tiene que estar abriéndose");
    }

    [Fact]
    public void NoSteeredContactEverOutrunsHalfATorpedo()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { At(0.99, PersonalityCatalog.Ram) });

        for (var i = 0; i < 300 && sim.State.Contacts.Count > 0; i++)
        {
            sim.Tick(NoCommands);
            if (sim.State.Contacts.Count == 0) break;
            Assert.True(sim.State.Contacts[0].Velocity.Length <= config.EnemyTorpedoSpeed / 2.0 + 1e-9,
                "el techo de velocidad se rompió");
        }
    }
}
