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

public class SurvivalTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Contact EnemyReadyToFire(int id, Vec2 position, int nextFireTick) => new()
    {
        Id = new ContactId(id),
        Kind = ContactKind.Enemy,
        Position = position,
        Velocity = Vec2.Zero,
        Alive = true,
        NextFireTick = nextFireTick,
    };

    [Fact]
    public void FirstEnemyTorpedo_HitsButDoesNotKill()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { EnemyReadyToFire(1, new Vec2(0.0, -0.50), nextFireTick: 0) });

        var events = new List<GameEvent>();
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        var hit = (SubHit)Assert.Single(events, e => e is SubHit);
        Assert.Equal(1, hit.HitsTaken);
        Assert.False(sim.State.Sub.Destroyed);
        Assert.DoesNotContain(events, e => e is SubDestroyed);
    }

    [Fact]
    public void SecondHit_DestroysTheSub()
    {
        // La cadencia enemiga la gobierna EnemyReloadTicks; se acorta para no gastar mil
        // ticks de test esperando el segundo disparo. El reloj del jugador baja con él
        // porque la config exige que el enemigo nunca cicle más rápido.
        var config = TestWorld.Config() with { ReloadTicks = 30, EnemyReloadTicks = 30 };
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { EnemyReadyToFire(1, new Vec2(0.0, -0.30), nextFireTick: 0) });

        var events = new List<GameEvent>();
        for (var i = 0; i < 400; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.Equal(2, events.Count(e => e is SubHit));
        Assert.Single(events, e => e is SubDestroyed);
        Assert.True(sim.State.Sub.Destroyed);
    }

    [Fact]
    public void AfterDestruction_TickDoesNothing()
    {
        var config = TestWorld.Config() with { ReloadTicks = 30, EnemyReloadTicks = 30 };
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { EnemyReadyToFire(1, new Vec2(0.0, -0.30), nextFireTick: 0) });

        for (var i = 0; i < 400; i++) sim.Tick(NoCommands);
        Assert.True(sim.State.Sub.Destroyed);

        var frozenTick = sim.State.Tick;
        var events = sim.Tick(NoCommands);

        Assert.Empty(events);
        Assert.Equal(frozenTick, sim.State.Tick);
    }

    [Fact]
    public void OwnAndEnemyTorpedo_DestroyEachOther()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { EnemyReadyToFire(1, new Vec2(0.0, -0.95), nextFireTick: 0) });

        var events = new List<GameEvent>();
        // Dispara al norte, de frente al torpedo entrante.
        events.AddRange(sim.Tick(new Command[]
        {
            new FireCommand(new TargetCoordinate(new Bearing(0), new Band(4))),
        }));
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.Contains(events, e => e is EnemyTorpedoDestroyed
        {
            Cause: DestructionCause.TorpedoCollision,
        });
        Assert.DoesNotContain(events, e => e is SubHit);
    }

    [Fact]
    public void EnemyTorpedo_PassesThroughInertContacts()
    {
        // El fuego amigo entre enemigos ya existe (spec 2026-08-15 §7), pero los pecios
        // siguen siendo inmunes: la anomalía de defensas del §25 los necesita como referencia.
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            EnemyReadyToFire(1, new Vec2(0.0, -0.90), nextFireTick: 0),
            Contact.CreateInert(new ContactId(2), new Vec2(0.0, -0.50)),
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 200; i++) events.AddRange(sim.Tick(NoCommands));

        // El pecio sigue vivo y el submarino recibió el impacto.
        Assert.Contains(sim.State.Contacts, c => c.Id.Value == 2);
        Assert.Contains(events, e => e is SubHit);
    }

    [Fact]
    public void EnemyTorpedo_StopsAtTheFirstEnemyItCrosses()
    {
        // Antes este test afirmaba que lo atravesaba. El spec del 2026-08-15 §7 revierte esa
        // decisión del TDD §4.5: ahora el compañero muere y el torpedo no llega al submarino.
        //
        // El compañero entra DESPUÉS del lanzamiento a propósito: con él ya en la línea, el
        // de atrás aguantaría el tiro y no habría torpedo que probar.
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { EnemyReadyToFire(1, new Vec2(0.0, -0.90), nextFireTick: 0) });

        var events = new List<GameEvent>(sim.Tick(NoCommands));
        sim.SetContacts(new[]
        {
            sim.State.Contacts.Single(c => c.Id.Value == 1),
            EnemyReadyToFire(2, new Vec2(0.0, -0.50), nextFireTick: int.MaxValue),
        });

        for (var i = 0; i < 200; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.DoesNotContain(sim.State.Contacts, c => c.Id.Value == 2);
        Assert.DoesNotContain(events, e => e is SubHit);
    }

    [Fact]
    public void Interception_ConfirmsTheOwnTorpedoDetonated()
    {
        // Sin este evento la intercepción es invisible para el jugador: el spec §4.6 pide
        // confirmación de impacto que no dependa de dónde esté el barrido.
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { EnemyReadyToFire(1, new Vec2(0.0, -0.95), nextFireTick: 0) });

        var events = new List<GameEvent>(sim.Tick(new Command[]
        {
            new FireCommand(new TargetCoordinate(new Bearing(0), new Band(4))),
        }));
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.Contains(events, e => e is TorpedoDetonated { HitSomething: true });
        // El torpedo propio no llegó a agotar su corrida en la banda E: murió antes.
        Assert.Empty(sim.State.Torpedoes);
    }
}
