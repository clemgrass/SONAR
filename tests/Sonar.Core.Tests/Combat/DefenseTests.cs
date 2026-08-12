using System;
using System.Collections.Generic;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Combat;

public class DefenseTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Command[] Deploy(int bearing, int band) =>
        new Command[] { new DeployDefenseCommand(new TargetCoordinate(new Bearing(bearing), new Band(band))) };

    private static Command[] Fire(int bearing, int band) =>
        new Command[] { new FireCommand(new TargetCoordinate(new Bearing(bearing), new Band(band))) };

    [Fact]
    public void Deploy_PlacesDefenseAndSpendsOne()
    {
        var sim = TestWorld.Still();
        var events = sim.Tick(Deploy(90, 2));

        Assert.Single(sim.State.Defenses);
        Assert.Equal(2, sim.State.Sub.DefensesAvailable);
        Assert.Contains(events, e => e is DefenseDeployed);
    }

    [Fact]
    public void Deploy_AtBandCenter()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.Tick(Deploy(90, 2));

        Assert.Equal(0.55, sim.State.Defenses[0].Position.X, precision: 10);
        Assert.Equal(0.00, sim.State.Defenses[0].Position.Y, precision: 10);
    }

    [Fact]
    public void Deploy_BeyondCapacity_IsRejected()
    {
        var sim = TestWorld.Still();
        for (var i = 0; i < 3; i++) sim.Tick(Deploy(i * 30, 2));

        var events = sim.Tick(Deploy(200, 2));

        var rejected = (DefenseRejected)Assert.Single(events, e => e is DefenseRejected);
        Assert.Equal(RejectionReason.NoneAvailable, rejected.Reason);
        Assert.Equal(3, sim.State.Defenses.Count);
    }

    [Fact]
    public void Defense_DestroysAContactThatDriftsIntoIt()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            new Contact
            {
                Id = new ContactId(1), Kind = ContactKind.Enemy,
                Position = new Vec2(0.90, 0.0), Velocity = new Vec2(-0.5, 0.0),
                Alive = true, NextFireTick = int.MaxValue,
            },
        });

        var events = new List<GameEvent>(sim.Tick(Deploy(90, 2)));
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        var destroyed = (ContactDestroyed)Assert.Single(events, e => e is ContactDestroyed);
        Assert.Equal(DestructionCause.Defense, destroyed.Cause);

        // La defensa se consume al detonar y hay que anunciarlo: el jugador tiene que
        // saber que quedó descubierto.
        var spent = (DefenseDetonated)Assert.Single(events, e => e is DefenseDetonated);
        Assert.Equal(1, spent.TargetsDestroyed);
        Assert.Empty(sim.State.Defenses);
    }

    [Fact]
    public void SpentDefense_FreesItsSlot()
    {
        // El límite es de concurrencia, no de inventario: con capacidad uno no puedes tener
        // dos a la vez, pero sí volver a poner una apenas la anterior se gaste.
        var config = TestWorld.Config() with { DefenseCapacity = 1 };
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[]
        {
            new Contact
            {
                Id = new ContactId(1), Kind = ContactKind.Enemy,
                Position = new Vec2(0.90, 0.0), Velocity = new Vec2(-0.5, 0.0),
                Alive = true, NextFireTick = int.MaxValue,
            },
        });

        sim.Tick(Deploy(90, 2));
        Assert.Equal(0, sim.State.Sub.DefensesAvailable);

        // Con una ya desplegada, la segunda se rechaza.
        Assert.Single(sim.Tick(Deploy(270, 2)), e => e is DefenseRejected);
        Assert.Single(sim.State.Defenses);

        // El contacto llega, la defensa detona y el hueco vuelve a quedar libre.
        var events = new List<GameEvent>();
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.Contains(events, e => e is DefenseDetonated);
        Assert.Empty(sim.State.Defenses);
        Assert.Equal(1, sim.State.Sub.DefensesAvailable);

        // Y ahora sí acepta otra.
        Assert.Contains(sim.Tick(Deploy(270, 2)), e => e is DefenseDeployed);
        Assert.Single(sim.State.Defenses);
    }

    [Fact]
    public void UntouchedDefense_IsNotSpent()
    {
        // Se gasta al detonar, no con el tiempo. Sin esto el test de arriba pasaría
        // aunque las defensas se evaporaran solas.
        var config = TestWorld.Config() with { DefenseCapacity = 1 };
        var sim = TestWorld.Still(config);

        var events = new List<GameEvent>(sim.Tick(Deploy(90, 2)));
        for (var i = 0; i < 600; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.DoesNotContain(events, e => e is DefenseDetonated);
        Assert.Single(sim.State.Defenses);
        Assert.Equal(0, sim.State.Sub.DefensesAvailable);
    }

    [Fact]
    public void Defense_LeavesInertContactsAlone()
    {
        // Un pecio puede quedar dentro de una defensa y no le pasa nada. Es la condición
        // para que la anomalía del §25 sea observable: el jugador tiene que poder dejar
        // una defensa junto a una referencia inerte y verlas separarse. Ver la nota en
        // ResolveDefenseCollisions y DefenseAnomalyTests.
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            Contact.CreateInert(new ContactId(1), new Vec2(0.55, 0.0)),
            new Contact
            {
                Id = new ContactId(2), Kind = ContactKind.Enemy,
                Position = new Vec2(0.55, 0.01), Velocity = Vec2.Zero,
                Alive = true, NextFireTick = int.MaxValue,
            },
        });

        var events = new List<GameEvent>(sim.Tick(Deploy(90, 2)));
        for (var i = 0; i < 10; i++) events.AddRange(sim.Tick(NoCommands));

        // Mismo sitio, misma defensa: el enemigo muere, el pecio sigue ahí.
        var destroyed = (ContactDestroyed)Assert.Single(events, e => e is ContactDestroyed);
        Assert.Equal(2, destroyed.ContactId);
        Assert.Equal(DestructionCause.Defense, destroyed.Cause);
        Assert.Equal(1, Assert.Single(sim.State.Contacts).Id.Value);
    }

    [Fact]
    public void Defense_DestroysOwnTorpedoesToo()
    {
        // El §24 del GDD dice que la defensa interactúa con proyectiles propios.
        var sim = TestWorld.Still();
        sim.Tick(Deploy(90, 1));   // defensa a 0.37 en marcación 90

        var events = new List<GameEvent>(sim.Tick(Fire(90, 4)));
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        // El torpedo muere en la defensa a 0.37; nunca llega a detonar en la banda E.
        Assert.Empty(sim.State.Torpedoes);
        Assert.DoesNotContain(events, e => e is TorpedoDetonated);

        // Control positivo: el mismo disparo sin defensa por delante sí detona. Sin esto,
        // el test pasaría igual si el fin de corrida estuviera roto y nadie detonara nunca.
        var control = TestWorld.Still();
        var controlEvents = new List<GameEvent>(control.Tick(Fire(90, 4)));
        for (var i = 0; i < 120; i++) controlEvents.AddRange(control.Tick(NoCommands));

        Assert.Contains(controlEvents, e => e is TorpedoDetonated);
    }
}
