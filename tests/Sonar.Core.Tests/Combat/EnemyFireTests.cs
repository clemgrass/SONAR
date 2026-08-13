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

public class EnemyFireTests
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
    public void Enemy_FiresWhenItsTickArrives()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { EnemyReadyToFire(1, new Vec2(0.0, -0.80), nextFireTick: 3) });

        var events = new List<GameEvent>();
        for (var i = 0; i < 5; i++) events.AddRange(sim.Tick(NoCommands));

        var fired = (EnemyTorpedoFired)Assert.Single(events, e => e is EnemyTorpedoFired);
        Assert.Equal(1, fired.SourceContactId);
        Assert.Equal(3, fired.Tick);   // ni antes ni después del tick pedido
        Assert.Single(sim.State.Torpedoes, t => t.Side == TorpedoSide.Enemy);
    }

    [Fact]
    public void EnemyTorpedo_TravelsTowardTheOrigin()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { EnemyReadyToFire(1, new Vec2(0.0, -0.80), nextFireTick: 0) });

        for (var i = 0; i < 30; i++) sim.Tick(NoCommands);

        var torpedo = sim.State.Torpedoes.Single(t => t.Side == TorpedoSide.Enemy);
        // Nació en y = -0.80 y viene bajando hacia el origen.
        Assert.True(torpedo.Position.Y > -0.80);
        Assert.True(torpedo.Position.Length < 0.80);
    }

    [Fact]
    public void Enemy_RearmsAfterTheReloadTime()
    {
        // Los enemigos recargan con el mismo reloj que el jugador: ReloadTicks.
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { EnemyReadyToFire(1, new Vec2(0.0, -0.95), nextFireTick: 0) });

        var events = new List<GameEvent>();
        for (var i = 0; i < config.ReloadTicks + 5; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.Equal(2, events.Count(e => e is EnemyTorpedoFired));
    }

    [Fact]
    public void Enemy_KeepsOnlyOneTorpedoInTheWater()
    {
        // Aunque la recarga ya haya terminado, mientras su torpedo siga viajando el enemigo
        // no dispara otro. Con torpedos lentos éste es el límite que manda, no el reloj.
        var config = TestWorld.Config() with
        {
            ReloadTicks = 30,             // recarga casi instantánea
            EnemyTorpedoSpeed = 0.05,     // pero el torpedo tarda muchísimo en llegar
        };
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { EnemyReadyToFire(1, new Vec2(0.0, -0.95), nextFireTick: 0) });

        var events = new List<GameEvent>();
        for (var i = 0; i < 600; i++) events.AddRange(sim.Tick(NoCommands));

        // 600 ticks son veinte recargas, y sin embargo hay un solo disparo.
        Assert.Single(events, e => e is EnemyTorpedoFired);
        Assert.Single(sim.State.Torpedoes, t => t.Side == TorpedoSide.Enemy);

        // Control positivo: con el torpedo fuera del agua sí vuelve a disparar. Sin esto el
        // test pasaría igual si los enemigos hubieran dejado de disparar del todo.
        sim.SetTorpedoes(System.Array.Empty<Torpedo>());
        var after = new List<GameEvent>();
        for (var i = 0; i < 60; i++) after.AddRange(sim.Tick(NoCommands));

        Assert.Contains(after, e => e is EnemyTorpedoFired);
    }

    [Fact]
    public void InertContact_NeverFires()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { Contact.CreateInert(new ContactId(1), new Vec2(0.0, -0.80)) });

        var events = new List<GameEvent>();
        for (var i = 0; i < 600; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.DoesNotContain(events, e => e is EnemyTorpedoFired);
    }

    [Fact]
    public void DeadContact_StopsFiring()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { EnemyReadyToFire(1, new Vec2(0.90, 0.0), nextFireTick: 0) });
        // Lo destruye una defensa colocada encima, en el mismo tick del despliegue.
        sim.Tick(new Command[]
        {
            new DeployDefenseCommand(new TargetCoordinate(new Bearing(90), new Band(4))),
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 600; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.Empty(sim.State.Contacts);
        Assert.DoesNotContain(events, e => e is EnemyTorpedoFired);

        // Control positivo: el mismo enemigo, vivo, sí vuelve a disparar dentro de esos
        // 600 ticks. Sin esto el test pasaría igual si los enemigos no dispararan nunca.
        var control = TestWorld.Still();
        control.SetContacts(new[] { EnemyReadyToFire(1, new Vec2(0.90, 0.0), nextFireTick: 0) });
        control.Tick(NoCommands);

        var controlEvents = new List<GameEvent>();
        for (var i = 0; i < 600; i++) controlEvents.AddRange(control.Tick(NoCommands));

        Assert.Contains(controlEvents, e => e is EnemyTorpedoFired);
    }
}
