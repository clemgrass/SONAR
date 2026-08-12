using System;
using System.Collections.Generic;
using Sonar.Controller.Presentation;
using Sonar.Core;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;

namespace Sonar.Controller;

/// <summary>
/// Frontera entre la presentación y la simulación. Acumula tiempo real y avanza Core en
/// pasos fijos; Core nunca ve tiempo de pared.
/// </summary>
public sealed class GameController
{
    private readonly SimulationConfig _config;
    private readonly BandLayout _layout;
    private readonly CommandParser _parser;
    private readonly Simulation _simulation;
    private readonly List<Command> _pending = new();

    private double _accumulator;

    public WorldSnapshot Snapshot { get; private set; }

    /// <summary>Eventos producidos por la última llamada a Advance.</summary>
    public IReadOnlyList<GameEvent> LastEvents { get; private set; } = Array.Empty<GameEvent>();

    public GameController(SimulationConfig config, int seed, Bearing course, double speed)
    {
        config.Validate();
        _config = config;
        _layout = config.BuildBandLayout();
        _parser = new CommandParser(_layout.Count);
        _simulation = new Simulation(config, new SeededRandomSource(seed),
            WorldState.CreateInitial(config, course, speed));
        Snapshot = SnapshotBuilder.Build(_simulation.State, _layout);
    }

    /// <summary>Coloca contactos. En M1 lo usa el arranque; después será el guion de misión.</summary>
    public void SetContacts(IEnumerable<Contact> contacts)
    {
        _simulation.SetContacts(contacts);
        Snapshot = SnapshotBuilder.Build(_simulation.State, _layout);
    }

    /// <summary>
    /// Encola texto crudo. Se parsea aquí y se aplica en la próxima frontera de tick,
    /// nunca a mitad: es lo que mantiene la simulación reproducible.
    /// </summary>
    public void SubmitInput(string rawText)
    {
        var command = _parser.Parse(rawText);
        if (command is not null) _pending.Add(command);
    }

    /// <summary>
    /// Acumula tiempo real y ejecuta los ticks completos que quepan, hasta
    /// MaxCatchUpTicks. El excedente se descarta: sin tope, un congelamiento produce
    /// una avalancha que el jugador percibe como un salto.
    /// </summary>
    public IReadOnlyList<GameEvent> Advance(double deltaSeconds)
    {
        _accumulator += deltaSeconds;

        var events = new List<GameEvent>();
        var executed = 0;

        while (_accumulator >= _config.TickDurationSeconds && executed < _config.MaxCatchUpTicks)
        {
            _accumulator -= _config.TickDurationSeconds;

            var batch = _pending.ToArray();
            _pending.Clear();
            events.AddRange(_simulation.Tick(batch));
            executed++;
        }

        // Descartar el excedente en vez de arrastrarlo al siguiente frame.
        if (_accumulator >= _config.TickDurationSeconds) _accumulator = 0;

        Snapshot = SnapshotBuilder.Build(_simulation.State, _layout);
        LastEvents = events;
        return events;
    }
}
