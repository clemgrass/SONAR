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

public class FriendlyFireTests
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
    public void AnEnemyTorpedoDestroysAnEnemySubmarineInItsPath()
    {
        // El accidente real no es disparar a través de un compañero —eso ahora se evita— sino
        // que el compañero se cruce DESPUÉS del lanzamiento. Por eso el tiro sale con el
        // carril limpio y el segundo enemigo aparece recién entonces: es exactamente el caso
        // que el chequeo de carril no puede prever, porque proyecta en línea recta.
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { Enemy(1, new Vec2(0.0, -0.90), nextFireTick: 0) });

        var events = new List<GameEvent>(sim.Tick(NoCommands));
        Assert.Contains(events, e => e is EnemyTorpedoFired);

        sim.SetContacts(new[]
        {
            sim.State.Contacts.Single(c => c.Id.Value == 1),
            Enemy(2, new Vec2(0.0, -0.50), nextFireTick: int.MaxValue),
        });

        for (var i = 0; i < 400; i++) events.AddRange(sim.Tick(NoCommands));

        var killed = Assert.Single(events.OfType<ContactDestroyed>(), e => e.ContactId == 2);
        Assert.Equal(DestructionCause.FriendlyFire, killed.Cause);
        Assert.DoesNotContain(sim.State.Contacts, c => c.Id.Value == 2);
    }

    [Fact]
    public void AnEnemyTorpedoStillPassesThroughWrecks()
    {
        // Los pecios siguen inmunes: la anomalía de defensas del §25 necesita que ese pecio
        // sobreviva como referencia observable.
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            Enemy(1, new Vec2(0.0, -0.90), nextFireTick: 0),
            Contact.CreateInert(new ContactId(2), new Vec2(0.0, -0.50)),
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 400; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.Contains(sim.State.Contacts, c => c.Id.Value == 2);

        // Control positivo: el torpedo recorrió la línea entera, así que cruzó el pecio.
        Assert.Contains(events, e => e is SubHit);
    }

    [Fact]
    public void AnEnemyTorpedoDoesNotDestroyTheContactThatFiredIt()
    {
        // Sin esta guarda, un torpedo detonaría contra su propio lanzador en el tick de salida:
        // nace exactamente encima de él.
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { Enemy(1, new Vec2(0.0, -0.90), nextFireTick: 0) });

        var events = new List<GameEvent>();
        for (var i = 0; i < 10; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.DoesNotContain(events, e => e is ContactDestroyed);
        Assert.Contains(sim.State.Contacts, c => c.Id.Value == 1);
    }

    [Fact]
    public void TwoEnemyTorpedoesDestroyEachOther()
    {
        // El plan original cruzaba estos dos torpedos justo sobre el origen, donde vive el
        // submarino. Por simetría se cruzan a mitad de camino entre sus lanzadores, y a esa
        // distancia mutua de HitRadius (0.040) cada uno está a la mitad de eso del origen
        // (0.020) — dentro de SubHitRadius (0.045). El submarino se comía los dos torpedos
        // por SubHit antes de que llegaran a chocar entre sí. Se desplaza el cruce a
        // y = -0.50 para que ocurra lejos de cualquier radio del submarino.
        var sim = TestWorld.Still();
        sim.SetContacts(Array.Empty<Contact>());
        // Dos torpedos enemigos de frente, de lanzadores distintos, que se cruzan.
        sim.SetTorpedoes(new[]
        {
            Torpedo.CreateEnemyToward(new TorpedoId(901), new Vec2(-0.30, -0.50),
                new Vec2(0.30, -0.50), 0.03, sourceContactId: 1),
            Torpedo.CreateEnemyToward(new TorpedoId(902), new Vec2(0.30, -0.50),
                new Vec2(-0.30, -0.50), 0.03, sourceContactId: 2),
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 700; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.Equal(2, events.OfType<EnemyTorpedoDestroyed>()
                              .Count(e => e.Cause == DestructionCause.TorpedoCollision));
        Assert.Empty(sim.State.Torpedoes);
    }

    [Fact]
    public void ATorpedoDoesNotCollideWithItself()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(Array.Empty<Contact>());
        sim.SetTorpedoes(new[]
        {
            Torpedo.CreateEnemyToward(new TorpedoId(901), new Vec2(-0.30, 0.0),
                new Vec2(0.30, 0.0), 0.03, sourceContactId: 1),
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 60; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.DoesNotContain(events, e => e is EnemyTorpedoDestroyed);
    }
}
