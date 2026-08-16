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

/// <summary>
/// `Personality.FiringRadius` se declaraba, se poblaba para las cinco personalidades y se
/// afirmaba en <see cref="Entities.PersonalityCatalogTests"/> — pero nada la leía. El RAM
/// (radio cero, "nunca dispara") disparaba igual que cualquier otro, y el STALKER tiraba
/// desde donde estuviera en vez de cerrar primero a su radio de compromiso. Este archivo
/// prueba que <c>FireEnemyTorpedoes</c> ahora sí respeta ese radio.
/// </summary>
public class FiringRangeTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Contact Enemy(int id, Personality? personality, Vec2 position, int nextFireTick) => new()
    {
        Id = new ContactId(id),
        Kind = ContactKind.Enemy,
        Position = position,
        Velocity = Vec2.Zero,
        Alive = true,
        NextFireTick = nextFireTick,
        LastFixTick = 0,
        Personality = personality,
    };

    [Fact]
    public void TheRamNeverFires()
    {
        // El RAM tiene FiringRadius = 0.0: matar por contacto es toda su amenaza, y no debe
        // soltar un solo torpedo mientras cierra desde 0.99.
        var ram = TestWorld.Still();
        ram.SetContacts(new[] { Enemy(1, PersonalityCatalog.Ram, new Vec2(0.0, -0.99), nextFireTick: 0) });

        var ramEvents = new List<GameEvent>();
        for (var i = 0; i < 3000; i++) ramEvents.AddRange(ram.Tick(NoCommands));

        Assert.DoesNotContain(ramEvents, e => e is EnemyTorpedoFired);

        // Control positivo: el MISMO escenario, con un STALKER en su lugar, sí dispara. Sin
        // esto "no disparó" podría significar igual de bien "los enemigos dejaron de
        // disparar del todo" (por ejemplo, si el gate quedara invertido).
        var control = TestWorld.Still();
        control.SetContacts(new[] { Enemy(1, PersonalityCatalog.Stalker, new Vec2(0.0, -0.99), nextFireTick: 0) });

        var controlEvents = new List<GameEvent>();
        for (var i = 0; i < 3000; i++) controlEvents.AddRange(control.Tick(NoCommands));

        Assert.Contains(controlEvents, e => e is EnemyTorpedoFired);
    }

    [Fact]
    public void AStalkerHoldsFireUntilItHasClosed()
    {
        var stalker = PersonalityCatalog.Stalker;
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { Enemy(1, stalker, new Vec2(0.0, -0.99), nextFireTick: 0) });

        // Su reloj de recarga ya lo deja disparar desde el primerísimo tick (NextFireTick =
        // 0). Si no dispara aquí, la causa no puede ser la recarga: tiene que ser la
        // distancia.
        var firstTickEvents = sim.Tick(NoCommands);
        Assert.DoesNotContain(firstTickEvents, e => e is EnemyTorpedoFired);

        double? distanceAtFirstShot = null;
        for (var i = 0; i < 3000 && distanceAtFirstShot is null; i++)
        {
            var events = sim.Tick(NoCommands);
            if (events.Any(e => e is EnemyTorpedoFired))
                distanceAtFirstShot = sim.State.Contacts[0].Position.Length;
        }

        Assert.NotNull(distanceAtFirstShot);
        Assert.True(distanceAtFirstShot <= stalker.FiringRadius,
            $"el STALKER disparó a distancia {distanceAtFirstShot:F4}, fuera de su radio de compromiso {stalker.FiringRadius:F4}");
    }

    [Fact]
    public void AContactWithoutPersonalityFiresFromAnywhere()
    {
        // Sin personalidad es el comportamiento de siempre: dispara aunque esté a 0.99, más
        // lejos que el radio de compromiso de cualquier personalidad del roster. Esto es lo
        // que mantiene válidos todos los tests de combate anteriores a esta tarea.
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { Enemy(1, personality: null, new Vec2(0.0, -0.99), nextFireTick: 0) });

        var events = sim.Tick(NoCommands);

        Assert.Contains(events, e => e is EnemyTorpedoFired);
    }
}
