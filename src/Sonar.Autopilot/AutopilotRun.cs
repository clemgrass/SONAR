using System;
using Sonar.Controller;
using Sonar.Core;

namespace Sonar.Autopilot;

/// <summary>
/// El bucle de una corrida pilotada. Existe una sola vez y la usan los dos consumidores —el
/// test y la escena de Godot— porque es lo que garantiza que lo que se mira sea exactamente
/// lo que el test afirma.
///
/// El piloto decide por número de tick, no por frame: un frame perdido en Godot hace la
/// simulación más lenta, nunca distinta.
/// </summary>
public sealed class AutopilotRun
{
    /// <summary>Tick y texto de cada comando. Godot lo engancha para escribirlo en el registro.</summary>
    public event Action<int, string>? CommandEmitted;

    private readonly GameController _controller;
    private readonly Pilot _pilot;
    private readonly SaturationMeter _meter;
    private readonly double _tickDuration;
    private readonly int _budgetTicks;

    private int _nextCommandTick;

    private readonly int _seed;
    private readonly double _paceSeconds;
    private readonly System.Collections.Generic.List<WaveRow> _waves = new();
    private readonly System.Collections.Generic.List<double> _saturationPerSecond = new();

    private int _wave;
    private int _waveStartTick;
    private int _previousLiveEnemies;
    private int _waveShots;
    private int _waveKills;
    private int _waveThreats;
    private int _waveCommands;
    private double _wavePeak;
    private string _deathCause = "Unknown";

    public AutopilotRun(SimulationConfig config, GameController controller, double paceSeconds)
    {
        _controller = controller;
        _pilot = new Pilot(config);
        _meter = new SaturationMeter(config);
        _tickDuration = config.TickDurationSeconds;
        _budgetTicks = (int)Math.Round(paceSeconds / config.TickDurationSeconds);

        // Arrancar la sesión no es táctica, así que no pasa por el piloto.
        controller.SubmitInput("START");

        _seed = controller.Seed;
        _paceSeconds = paceSeconds;
    }

    public bool Finished => _controller.Snapshot.Destroyed;
    public int Tick => _controller.Snapshot.Tick;
    public double Saturation => _meter.IndexAt(_controller.Snapshot.Tick);
    public string? LastCommand { get; private set; }

    public void StepOneTick()
    {
        // Con el submarino destruido Simulation.Tick devuelve temprano y NO actualiza el
        // estado: el tick queda congelado. Sin esta guarda, el bucle de abajo no termina.
        if (Finished) return;

        var snapshot = _controller.Snapshot;
        _pilot.Observe(snapshot);

        if (snapshot.Tick >= _nextCommandTick)
        {
            var text = _pilot.Decide(snapshot);
            if (text is not null)
            {
                _controller.SubmitInput(text);
                _meter.NoteCommand(snapshot.Tick);
                _waveCommands++;
                LastCommand = text;
                CommandEmitted?.Invoke(snapshot.Tick, text);
                _nextCommandTick = snapshot.Tick + _budgetTicks;
            }
        }

        _meter.Observe(snapshot);

        if (snapshot.Tick % 60 == 0) _saturationPerSecond.Add(_meter.IndexAt(snapshot.Tick));

        var current = _meter.IndexAt(snapshot.Tick);
        if (current > _wavePeak) _wavePeak = current;

        // El director de oleadas no emite eventos —es guion, no simulación—, así que el
        // cambio se detecta comparando, igual que hace Main.cs.
        if (_controller.Wave != _wave)
        {
            // Con el conteo del tick ANTERIOR: en el tick en que entra una oleada nueva, el
            // conteo actual ya incluye a los que acaban de aparecer, y la oleada que se está
            // cerrando quedaría reportada con el tamaño de la que la reemplaza.
            CloseWave(snapshot.Tick, _previousLiveEnemies);
            _wave = _controller.Wave;
            _waveStartTick = snapshot.Tick;
            _waveShots = _waveKills = _waveThreats = _waveCommands = 0;
            _wavePeak = 0.0;
        }

        _previousLiveEnemies = _controller.LiveEnemyCount;

        // Un período exacto ejecuta cero o un tick, nunca dos. Se reintenta una vez por la
        // deriva de punto flotante; el tope evita el cuelgue si esa suposición deja de valer.
        var before = _controller.Snapshot.Tick;
        for (var attempt = 0; attempt < 2 && _controller.Snapshot.Tick == before; attempt++)
            Record(_controller.Advance(_tickDuration));
    }

    /// <summary>
    /// Avanza hasta morir. El tope no es decoración: si el piloto sobrevive para siempre, el
    /// test tiene que fallar con un mensaje, no colgarse.
    /// </summary>
    public void RunToDeath(int maxTicks)
    {
        var steps = 0;
        while (!Finished && steps++ < maxTicks) StepOneTick();
    }

    /// <summary>
    /// Cuenta desde el flujo de eventos, que es verdad del mundo. El arnés SÍ puede mirarla;
    /// el que no puede es el piloto. Por eso las amenazas del reporte se cuentan por evento y
    /// las del índice por lo que apareció en el snapshot: el índice mide lo que el jugador
    /// percibe, el reporte lo que de verdad pasó.
    /// </summary>
    private void Record(System.Collections.Generic.IReadOnlyList<Sonar.Core.Events.GameEvent> events)
    {
        foreach (var evt in events)
        {
            switch (evt)
            {
                case Sonar.Core.Events.TorpedoFired: _waveShots++; break;
                case Sonar.Core.Events.ContactDestroyed: _waveKills++; break;
                case Sonar.Core.Events.EnemyTorpedoFired: _waveThreats++; break;
                case Sonar.Core.Events.SubDestroyed e: _deathCause = e.Cause.ToString(); break;
            }
        }
    }

    /// <summary>
    /// En toda oleada que se cierra por dar paso a la siguiente, `enemiesLeft` es cero por
    /// construcción: el director no deja entrar la próxima hasta que se limpie la anterior.
    /// Un número distinto de cero en una fila que no sea la última significa que esa compuerta
    /// se rompió. Donde la columna sí dice algo es en la última fila: cuántos quedaban vivos
    /// en el momento de morir.
    /// </summary>
    private void CloseWave(int tick, int enemiesLeft)
    {
        if (_wave == 0) return;
        _waves.Add(new WaveRow(
            _wave, _waveStartTick, tick - _waveStartTick, enemiesLeft,
            _waveShots, _waveKills, _waveThreats, _waveCommands,
            _wavePeak, _meter.IndexAt(tick)));
    }

    /// <summary>Cierra la oleada en curso y devuelve la corrida en números.</summary>
    public RunReport BuildReport()
    {
        var tick = _controller.Snapshot.Tick;
        var waves = new System.Collections.Generic.List<WaveRow>(_waves);
        if (_wave != 0)
        {
            waves.Add(new WaveRow(
                _wave, _waveStartTick, tick - _waveStartTick, _controller.LiveEnemyCount,
                _waveShots, _waveKills, _waveThreats, _waveCommands,
                _wavePeak, _meter.IndexAt(tick)));
        }

        return new RunReport(_seed, _paceSeconds, tick, _deathCause, _wave,
            _meter.IndexAt(tick), waves, _saturationPerSecond);
    }
}
