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

    public AutopilotRun(SimulationConfig config, GameController controller, double paceSeconds)
    {
        _controller = controller;
        _pilot = new Pilot(config);
        _meter = new SaturationMeter(config);
        _tickDuration = config.TickDurationSeconds;
        _budgetTicks = (int)Math.Round(paceSeconds / config.TickDurationSeconds);

        // Arrancar la sesión no es táctica, así que no pasa por el piloto.
        controller.SubmitInput("START");
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
                LastCommand = text;
                CommandEmitted?.Invoke(snapshot.Tick, text);
                _nextCommandTick = snapshot.Tick + _budgetTicks;
            }
        }

        _meter.Observe(snapshot);

        // Un período exacto ejecuta cero o un tick, nunca dos. Se reintenta una vez por la
        // deriva de punto flotante; el tope evita el cuelgue si esa suposición deja de valer.
        var before = _controller.Snapshot.Tick;
        for (var attempt = 0; attempt < 2 && _controller.Snapshot.Tick == before; attempt++)
            _controller.Advance(_tickDuration);
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
}
