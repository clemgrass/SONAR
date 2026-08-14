using System;
using System.Collections.Generic;
using System.Linq;
using Sonar.Controller.Presentation;
using Sonar.Core;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;

namespace Sonar.Controller;

/// <summary>
/// Frontera entre la presentación y la simulación. Acumula tiempo real y avanza Core en
/// pasos fijos; Core nunca ve tiempo de pared. También hospeda el director de oleadas y el
/// reinicio, porque ninguno de los dos es una regla del mundo.
/// </summary>
public sealed class GameController
{
    private readonly SimulationConfig _config;
    private readonly BandLayout _layout;
    private readonly CommandParser _parser;
    private readonly Bearing _course;
    private readonly double _speed;
    private readonly int _baseSeed;
    private readonly List<Command> _pending = new();
    private readonly List<int> _spawnedBearings = new();

    private Simulation _simulation = null!;
    private WaveDirector _waves = null!;
    private double _accumulator;

    public WorldSnapshot Snapshot { get; private set; } = null!;

    /// <summary>Eventos producidos por la última llamada a Advance.</summary>
    public IReadOnlyList<GameEvent> LastEvents { get; private set; } = Array.Empty<GameEvent>();

    /// <summary>Intento actual, base 1. RETRY lo incrementa.</summary>
    public int Attempt { get; private set; }

    /// <summary>Semilla del intento actual. Anotarla permite reproducirlo entero.</summary>
    public int Seed { get; private set; }

    /// <summary>
    /// Falso hasta que llegue `START`. Con el mundo detenido el jugador nuevo puede leer las
    /// instrucciones sin que lo maten mientras lee: es lo que permite que la demo se explique
    /// sola. Una vez arrancada no vuelve atrás — `RETRY` reinicia la corrida, no el instructivo.
    /// </summary>
    public bool Started { get; private set; }

    public int Wave => _waves.Wave;

    public int LiveEnemyCount =>
        _simulation.State.Contacts.Count(c => c.Alive && c.Kind == ContactKind.Enemy);

    public GameController(SimulationConfig config, int baseSeed, Bearing course, double speed)
    {
        config.Validate();
        _config = config;
        _layout = config.BuildBandLayout();
        _parser = new CommandParser(_layout.Count);
        _course = course;
        _speed = speed;
        _baseSeed = baseSeed;

        StartRun();
    }

    /// <summary>
    /// Arranca un intento. La semilla se deriva de la base y del número de intento, así que
    /// los spawns cambian entre intentos y la sesión completa sigue siendo reproducible con
    /// sólo anotar la semilla base.
    ///
    /// No toca `Started` a propósito: RETRY reinicia la corrida, no devuelve al instructivo.
    /// </summary>
    private void StartRun()
    {
        Attempt++;
        Seed = _baseSeed + Attempt * 7919;   // primo, para separar bien las secuencias

        _simulation = new Simulation(_config, new SeededRandomSource(Seed),
            WorldState.CreateInitial(_config, _course, _speed));
        _waves = new WaveDirector(_config, new SeededRandomSource(Seed ^ 0x5EED),
            _course, _speed);

        _accumulator = 0;
        _pending.Clear();
        _spawnedBearings.Clear();
        LastEvents = Array.Empty<GameEvent>();
        Snapshot = SnapshotBuilder.Build(_simulation.State, _layout);
    }

    /// <summary>
    /// Encola texto crudo. Se parsea aquí y se aplica en la próxima frontera de tick, nunca
    /// a mitad: es lo que mantiene la simulación reproducible.
    ///
    /// RETRY es la excepción y se atiende de inmediato: con el submarino destruido la
    /// simulación se congela y un comando encolado no se consumiría nunca.
    /// </summary>
    /// <returns>
    /// `true` si el texto correspondía a un comando. Lo devuelve porque la telemetría
    /// necesita saberlo y la escena no puede deducirlo: el parser vive acá dentro.
    /// </returns>
    public bool SubmitInput(string rawText)
    {
        var command = _parser.Parse(rawText);
        if (command is null) return false;

        switch (command)
        {
            case StartCommand:
                Started = true;
                return true;

            case RetryCommand:
                if (Snapshot.Destroyed) StartRun();
                return true;
        }

        // Antes de arrancar sólo START hace algo. Un comando encolado durante el instructivo
        // se dispararía en el instante en que el jugador arranca, que es justo cuando
        // todavía no sabe lo que hace.
        if (!Started) return command is not UnrecognizedInput;

        _pending.Add(command);
        return command is not UnrecognizedInput;
    }

    /// <summary>
    /// Acumula tiempo real y ejecuta los ticks completos que quepan, hasta MaxCatchUpTicks.
    /// El excedente se descarta: sin tope, un congelamiento produce una avalancha que el
    /// jugador percibe como un salto.
    /// </summary>
    public IReadOnlyList<GameEvent> Advance(double deltaSeconds)
    {
        // El mundo no corre hasta START. Tampoco se acumula tiempo: si se acumulara, el
        // jugador que se toma dos minutos leyendo recibiría una avalancha al arrancar.
        if (!Started) return Array.Empty<GameEvent>();

        _accumulator += deltaSeconds;

        var events = new List<GameEvent>();
        var executed = 0;

        while (_accumulator >= _config.TickDurationSeconds && executed < _config.MaxCatchUpTicks)
        {
            _accumulator -= _config.TickDurationSeconds;

            var spawned = _waves.Advance(_simulation.State.Tick, LiveEnemyCount);
            if (spawned.Count > 0)
            {
                _simulation.AddContacts(spawned);
                foreach (var contact in spawned)
                    _spawnedBearings.Add((int)Math.Round(Bearing.DegreesOf(contact.Position)));
            }

            var batch = _pending.ToArray();
            _pending.Clear();
            events.AddRange(_simulation.Tick(batch));
            executed++;
        }

        if (_accumulator >= _config.TickDurationSeconds) _accumulator = 0;

        Snapshot = SnapshotBuilder.Build(_simulation.State, _layout);
        LastEvents = events;
        return events;
    }

    // --- Sólo para tests. No los use la presentación. ---

    /// <summary>Marca el submarino como destruido, para poder ejercitar RETRY sin morir.</summary>
    public void ForceDestroyForTests() =>
        Snapshot = Snapshot with { Destroyed = true };

    public int[] SpawnedBearingsForTests() => _spawnedBearings.ToArray();
}
