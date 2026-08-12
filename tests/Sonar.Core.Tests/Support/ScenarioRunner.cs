using System;
using System.Collections.Generic;
using Sonar.Core;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;

namespace Sonar.Core.Tests.Support;

/// <summary>
/// Arnés headless: corre un guion (tick → comandos) y devuelve los eventos producidos.
/// Es lo que hace cierta la afirmación de que la campaña se puede jugar sin abrir Godot.
/// </summary>
public sealed class ScenarioRunner
{
    private readonly Simulation _simulation;
    private readonly IReadOnlyDictionary<int, IReadOnlyList<Command>> _script;

    /// <summary>
    /// Tick absoluto desde el arranque. Es un campo y no una variable de bucle a propósito:
    /// los tests de determinismo llaman Run(1) repetidamente, y con un contador local el
    /// guion volvería a aplicar los comandos del tick 0 en cada llamada.
    /// </summary>
    private int _tick;

    public List<GameEvent> Events { get; } = new();

    public ScenarioRunner(
        SimulationConfig config,
        int seed,
        IReadOnlyDictionary<int, IReadOnlyList<Command>> script,
        IEnumerable<Contact>? contacts = null,
        double subSpeed = 0.05)
    {
        _simulation = new Simulation(config, new SeededRandomSource(seed),
            WorldState.CreateInitial(config, new Bearing(0), subSpeed));
        if (contacts is not null) _simulation.SetContacts(contacts);
        _script = script;
    }

    /// <summary>Avanza `ticks` ticks más, continuando desde donde quedó.</summary>
    public WorldState Run(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            var commands = _script.TryGetValue(_tick, out var queued)
                ? queued
                : Array.Empty<Command>();
            Events.AddRange(_simulation.Tick(commands));
            _tick++;
        }
        return _simulation.State;
    }

    public WorldState State => _simulation.State;
}

public static class ScenarioScripts
{
    /// Guion fijo de referencia para los tests de determinismo.
    public static IReadOnlyDictionary<int, IReadOnlyList<Command>> MixedActivity() =>
        new Dictionary<int, IReadOnlyList<Command>>
        {
            [10] = new Command[] { new StatusCommand() },
            [40] = new Command[] { new ToggleSonarCommand() },
            [90] = new Command[] { new ToggleSonarCommand() },
            [150] = new Command[] { new UnrecognizedInput("what is this") },
            [220] = new Command[] { new StatusCommand() },
        };

    /// Contactos de referencia: dos enemigos en movimiento y un pecio inmóvil.
    public static IEnumerable<Contact> ReferenceContacts() => new[]
    {
        new Contact
        {
            Id = new ContactId(101), Kind = ContactKind.Enemy,
            Position = new Vec2(0.60, -0.30), Velocity = new Vec2(-0.08, 0.05),
            Alive = true, NextFireTick = int.MaxValue,
        },
        new Contact
        {
            Id = new ContactId(102), Kind = ContactKind.Enemy,
            Position = new Vec2(-0.45, 0.55), Velocity = new Vec2(0.06, -0.09),
            Alive = true, NextFireTick = int.MaxValue,
        },
        Contact.CreateInert(new ContactId(103), new Vec2(0.10, 0.80)),
    };
}
