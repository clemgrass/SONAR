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

public class RammingTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Contact Rammer(int id, double radius, Personality? personality = null) => new()
    {
        Id = new ContactId(id),
        Kind = ContactKind.Enemy,
        Position = new Vec2(0.0, -radius),
        Velocity = Vec2.Zero,
        Alive = true,
        NextFireTick = int.MaxValue,
        LastFixTick = 0,
        Personality = personality ?? PersonalityCatalog.Ram,
    };

    [Fact]
    public void ARammingRunIsAnnouncedBeforeItArrives()
    {
        // Un kamikaze silencioso mata con algo que no se pudo anticipar. El aviso es lo que
        // convierte la embestida en algo que el jugador pudo leer.
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { Rammer(1, config.PassiveHearingRadius * 0.8) });

        var events = new List<GameEvent>();
        for (var i = 0; i < 600 && !sim.State.Sub.Destroyed; i++)
            events.AddRange(sim.Tick(NoCommands));

        var warnings = events.OfType<RammingRunDetected>().ToList();
        Assert.NotEmpty(warnings);

        var death = events.OfType<SubDestroyed>().FirstOrDefault();
        Assert.NotNull(death);
        Assert.True(warnings[0].Tick < death!.Tick,
            $"el aviso llegó en el tick {warnings[0].Tick} y la muerte en el {death.Tick}");
    }

    [Fact]
    public void OnlyRammersAnnounceThemselves()
    {
        // Control negativo: la misma distancia, otra personalidad, ningún aviso.
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[]
        {
            Rammer(1, config.PassiveHearingRadius * 0.8, PersonalityCatalog.Stalker),
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 600; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.DoesNotContain(events, e => e is RammingRunDetected);
    }

    [Fact]
    public void ARammerFarAwayIsNotYetAnnounced()
    {
        // El aviso es de proximidad, no de existencia: si sonara desde el borde, dejaría de
        // significar "esto viene por ti ahora".
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { Rammer(1, 0.95) });

        var events = new List<GameEvent>();
        for (var i = 0; i < 30; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.DoesNotContain(events, e => e is RammingRunDetected);
    }

    [Fact]
    public void TheWarningRepeatsWhileItKeepsClosing()
    {
        // Es un redoble, no un aviso único: una sola línea se pierde en el registro.
        var config = TestWorld.Config() with { EnemyTorpedoSpeed = 0.02 };   // embiste lento
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { Rammer(1, config.PassiveHearingRadius * 0.95) });

        var events = new List<GameEvent>();
        for (var i = 0; i < 900 && !sim.State.Sub.Destroyed; i++)
            events.AddRange(sim.Tick(NoCommands));

        Assert.True(events.OfType<RammingRunDetected>().Count() > 1,
            "el aviso tiene que repetirse mientras siga cerca");
    }
}
