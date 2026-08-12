using System;
using System.Collections.Generic;
using System.Linq;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;

namespace Sonar.Core;

public interface ISimulation
{
    WorldState State { get; }

    /// <summary>Avanza exactamente un tick, consumiendo los comandos dados.</summary>
    IReadOnlyList<GameEvent> Tick(IReadOnlyList<Command> commands);
}

/// <summary>
/// Bucle de simulación. Nunca ve tiempo de pared: avanza en pasos fijos de
/// config.TickDurationSeconds. Acumular tiempo real es trabajo del Controller.
/// </summary>
public sealed class Simulation : ISimulation
{
    private readonly SimulationConfig _config;
    private readonly BandLayout _layout;
    private readonly IRandomSource _random;

    public WorldState State { get; private set; }

    public Simulation(SimulationConfig config, IRandomSource random, WorldState initial)
    {
        config.Validate();
        _config = config;
        _layout = config.BuildBandLayout();
        _random = random;
        State = initial;
    }

    /// <summary>Solo para tests y para el guion de misión: coloca contactos de golpe.</summary>
    public void SetContacts(IEnumerable<Contact> contacts) => State = State.WithContacts(contacts);

    public void SetTorpedoes(IEnumerable<Torpedo> torpedoes) => State = State.WithTorpedoes(torpedoes);

    public void SetDefenses(IEnumerable<Defense> defenses) => State = State.WithDefenses(defenses);

    /// <summary>
    /// Orden de actualización fijo, según TDD §3.2. Cambiarlo cambia el resultado de la
    /// simulación y rompe el determinismo respecto a corridas anteriores.
    /// </summary>
    public IReadOnlyList<GameEvent> Tick(IReadOnlyList<Command> commands)
    {
        if (State.Sub.Destroyed) return Array.Empty<GameEvent>();

        var events = new List<GameEvent>();
        var state = State;
        var dt = _config.TickDurationSeconds;

        // 1 · Aplicar comandos en el orden en que llegaron
        foreach (var command in commands) state = ApplyCommand(state, command, events);

        // 2 · Rumbo y velocidad del submarino: los fija el guion de misión, no este bucle

        // 3 · Deriva del mundo
        var worldDrift = state.Sub.Course.ToUnitVector() * -state.Sub.Speed;

        // 4 · Mover contactos — reciben la deriva
        state = state.WithContacts(state.Contacts.Select(c =>
            c.Alive ? c with { Position = c.Position + (c.Velocity + worldDrift) * dt } : c));

        // 5 · Disparo enemigo — Plan 3

        // 6 · Mover torpedos
        state = state.WithTorpedoes(state.Torpedoes.Select(t =>
            t with { Position = t.Position + t.Direction * t.Speed * dt }));

        // 7 · Colisiones — Plan 3
        // 8 · Corridas agotadas y detonaciones por radio — Plan 3
        // 9 · Impactos sobre el submarino — Plan 3

        // 10 · Barrido. El registro de lecturas con gating llega en el Plan 4.
        state = state with { Sonar = AdvanceSweep(state, dt) };

        // 11 · Recarga — Plan 3

        // 12 · Retirar entidades fuera de rango
        state = state
            .WithContacts(state.Contacts.Where(c => c.Alive))
            .WithTorpedoes(state.Torpedoes.Where(t => t.Position.Length <= 1.0));

        // 13 · Avanzar el contador
        state = state with { Tick = state.Tick + 1 };

        State = state;
        return events;
    }

    private WorldState ApplyCommand(WorldState state, Command command, List<GameEvent> events)
    {
        switch (command)
        {
            case ToggleSonarCommand:
            {
                var running = !state.Sonar.SweepRunning;
                events.Add(new SonarToggled(state.Tick, running));
                return state with { Sonar = state.Sonar with { SweepRunning = running } };
            }

            case StatusCommand:
                events.Add(new StatusReported(state.Tick,
                    state.Sub.AmmoLoaded, state.Sub.AmmoReserve, state.Sub.DefensesAvailable));
                return state;

            case UnrecognizedInput unrecognized:
                events.Add(new InputNotRecognized(state.Tick, unrecognized.RawText));
                return state;

            // FireCommand, DeployDefenseCommand y ReloadCommand llegan en el Plan 3.
            default:
                return state;
        }
    }

    private Sensing.SonarState AdvanceSweep(WorldState state, double dt)
    {
        if (!state.Sonar.SweepRunning) return state.Sonar;
        var degreesPerSecond = 360.0 / _config.SweepPeriodSeconds;
        var advanced = (state.Sonar.SweepDegrees + degreesPerSecond * dt) % 360.0;
        return state.Sonar with { SweepDegrees = advanced };
    }
}
