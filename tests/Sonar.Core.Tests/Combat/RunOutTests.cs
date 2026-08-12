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

public class RunOutTests
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

    private static List<GameEvent> RunFor(Simulation sim, Command[] first, int ticks)
    {
        var events = new List<GameEvent>(sim.Tick(first));
        for (var i = 0; i < ticks; i++) events.AddRange(sim.Tick(NoCommands));
        return events;
    }

    [Fact]
    public void BandTooShort_DetonatesInEmptyWaterAndContactSurvives()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.80, 0.0)) });

        // Banda A: corrida a 0.19. El contacto está en 0.80.
        var events = RunFor(sim, Fire(90, 0), 120);

        var detonated = (TorpedoDetonated)Assert.Single(events, e => e is TorpedoDetonated);
        Assert.False(detonated.HitSomething);
        Assert.Single(sim.State.Contacts);
    }

    [Fact]
    public void ContactSlightlyOffBearing_IsKilledByTheBlast()
    {
        var sim = TestWorld.Still();
        // Centro de la banda C está a 0.55 en marcación 90 -> (0.55, 0).
        // Un contacto a 0.06 de ahí cae dentro del radio de explosión (0.10) pero fuera
        // del radio de impacto directo (0.04): sólo puede morir por la detonación.
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.55, 0.06)) });

        var events = RunFor(sim, Fire(90, 2), 120);

        var destroyed = (ContactDestroyed)Assert.Single(events, e => e is ContactDestroyed);
        Assert.Equal(DestructionCause.Blast, destroyed.Cause);
        var detonated = (TorpedoDetonated)Assert.Single(events, e => e is TorpedoDetonated);
        Assert.True(detonated.HitSomething);
    }

    [Fact]
    public void TwoContactsInBlastRadius_BothDie()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            EnemyAt(1, new Vec2(0.55, 0.05)),
            EnemyAt(2, new Vec2(0.55, -0.05)),
        });

        var events = RunFor(sim, Fire(90, 2), 120);

        Assert.Equal(2, events.Count(e => e is ContactDestroyed));
        Assert.Empty(sim.State.Contacts);
    }

    [Fact]
    public void ContactJustOutsideBlast_SurvivesAndEmitsNearMiss()
    {
        var sim = TestWorld.Still();
        // A 0.13 del punto de detonación: fuera del radio de explosión (0.10),
        // dentro de la ventana de casi-impacto (0.16).
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.55, 0.13)) });

        var events = RunFor(sim, Fire(90, 2), 120);

        Assert.DoesNotContain(events, e => e is ContactDestroyed);
        var nearMiss = (NearMiss)Assert.Single(events, e => e is NearMiss);
        Assert.Equal(1, nearMiss.ContactId);
        Assert.InRange(nearMiss.Distance, 0.10, 0.16);
    }

    [Fact]
    public void ContactFarAway_EmitsNoNearMiss()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.55, 0.40)) });

        var events = RunFor(sim, Fire(90, 2), 120);

        // Control positivo: la detonación sí ocurrió. Sin esto, afirmar la ausencia de
        // NearMiss pasaría igual con el fin de corrida entero sin implementar.
        Assert.Contains(events, e => e is TorpedoDetonated);
        Assert.DoesNotContain(events, e => e is NearMiss);
    }

    [Fact]
    public void TorpedoIsGoneAfterDetonating()
    {
        var sim = TestWorld.Still();

        // 45 ticks: el torpedo cruza su corrida de banda C (0.55) alrededor del tick 39,
        // y recién pasado el tick 78 saldría del arena. La ventana importa: con 120 ticks
        // este test pasaría por la limpieza de fuera de rango aunque nada detonara nunca.
        var events = RunFor(sim, Fire(90, 2), 45);

        Assert.Contains(events, e => e is TorpedoDetonated);
        Assert.Empty(sim.State.Torpedoes);
    }
}
