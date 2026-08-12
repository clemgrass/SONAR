using System;
using System.Collections.Generic;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests;

/// <summary>
/// La anomalía del §25 del GDD no se implementa: se produce por una omisión deliberada.
/// Los contactos reciben la deriva del mundo; las defensas no. Como el submarino está fijo
/// en el origen, una defensa quieta en espacio de simulación es una defensa que acompaña
/// al submarino en la ficción, mientras un pecio se aleja.
///
/// Si alguno de estos tests falla porque alguien le agregó deriva a las defensas, la
/// anomalía dejó de existir. No es un bug: es la mecánica.
/// </summary>
public class DefenseAnomalyTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    [Fact]
    public void InertContact_DriftsAsTheSubSteamsForward()
    {
        var sim = TestWorld.SteamingNorth(speed: 0.10);
        sim.SetContacts(new[] { Contact.CreateInert(new ContactId(1), new Vec2(0.0, -0.50)) });

        for (var i = 0; i < 60; i++) sim.Tick(NoCommands);

        // El submarino avanza al norte a 0.10/s, así que el pecio deriva al sur 0.10 en un segundo.
        Assert.Equal(-0.40, sim.State.Contacts[0].Position.Y, precision: 6);
    }

    [Fact]
    public void Defense_DoesNotDrift()
    {
        var sim = TestWorld.SteamingNorth(speed: 0.10);
        sim.SetDefenses(new[] { Defense.Create(new DefenseId(1), new Vec2(0.0, -0.50)) });

        for (var i = 0; i < 60; i++) sim.Tick(NoCommands);

        Assert.Equal(-0.50, sim.State.Defenses[0].Position.Y, precision: 10);
    }

    [Fact]
    public void WreckAndDefense_StartTogetherAndSeparate()
    {
        // Esta es la observación que el jugador puede hacer: dos cosas en el mismo sitio,
        // y una se queda contigo.
        var sim = TestWorld.SteamingNorth(speed: 0.10);
        var start = new Vec2(0.0, -0.50);
        sim.SetContacts(new[] { Contact.CreateInert(new ContactId(1), start) });
        sim.SetDefenses(new[] { Defense.Create(new DefenseId(2), start) });

        for (var i = 0; i < 120; i++) sim.Tick(NoCommands);

        var separation = Vec2.Distance(sim.State.Contacts[0].Position, sim.State.Defenses[0].Position);
        Assert.Equal(0.20, separation, precision: 6);
    }

    [Fact]
    public void WithSubStopped_NothingDrifts()
    {
        // Control: sin avance no hay deriva, y por tanto no hay anomalía observable.
        var sim = TestWorld.Still();
        var start = new Vec2(0.0, -0.50);
        sim.SetContacts(new[] { Contact.CreateInert(new ContactId(1), start) });
        sim.SetDefenses(new[] { Defense.Create(new DefenseId(2), start) });

        for (var i = 0; i < 120; i++) sim.Tick(NoCommands);

        Assert.Equal(start.Y, sim.State.Contacts[0].Position.Y, precision: 10);
        Assert.Equal(start.Y, sim.State.Defenses[0].Position.Y, precision: 10);
    }
}
