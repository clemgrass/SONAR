using System;
using System.Collections.Generic;
using Sonar.Controller.Presentation;
using Sonar.Core;

namespace Sonar.Autopilot;

/// <summary>
/// Mide si el jugador puede seguirle el ritmo a lo que le llega:
///
///     S = amenazas nuevas en la ventana / comandos emitidos en la ventana
///
/// Dos conteos y ningún modelo. El denominador ya contiene las dos esperas que importan —el
/// presupuesto de composición y el tiempo que el barrido hace esperar por una lectura— así
/// que no hace falta modelarlas: quedan medidas.
///
/// Está sesgado A LA BAJA a propósito: un RELOAD cuenta como comando aunque no atienda a
/// ninguna amenaza, y eso infla el denominador. Si aun así S cruza 1, cruzó de verdad.
/// </summary>
public sealed class SaturationMeter
{
    /// <summary>
    /// Unos cuatro barridos y más de un ciclo de fuego enemigo. Más corta salta con cada
    /// torpedo; más larga borra el momento en que la cosa se rompe.
    /// </summary>
    public const double WindowSeconds = 30.0;

    private readonly int _windowTicks;
    private readonly List<int> _threatTicks = new();
    private readonly List<int> _commandTicks = new();

    /// <summary>
    /// Los ids de entidad ascienden siempre (WorldState.TakeEntityId), así que un torpedo
    /// con id mayor al mayor visto es necesariamente nuevo. Un entero en vez de una lista de
    /// ids que crecería durante toda la corrida.
    /// </summary>
    private int _highestTorpedoId = -1;

    public SaturationMeter(SimulationConfig config) =>
        _windowTicks = (int)Math.Round(WindowSeconds / config.TickDurationSeconds);

    public void Observe(WorldSnapshot snapshot)
    {
        foreach (var torpedo in snapshot.EnemyTorpedoes)
        {
            if (torpedo.Id <= _highestTorpedoId) continue;
            _highestTorpedoId = torpedo.Id;
            _threatTicks.Add(snapshot.Tick);
        }

        Prune(_threatTicks, snapshot.Tick);
        Prune(_commandTicks, snapshot.Tick);
    }

    public void NoteCommand(int tick) => _commandTicks.Add(tick);

    public double IndexAt(int tick)
    {
        var threats = CountIn(_threatTicks, tick);
        if (threats == 0) return 0.0;

        // Max(1, …) en vez de infinito: "tres amenazas y ningún comando" se reporta como 3,
        // que es un número comparable y dice lo mismo.
        return threats / (double)Math.Max(1, CountIn(_commandTicks, tick));
    }

    private int CountIn(List<int> ticks, int now)
    {
        var floor = now - _windowTicks;
        var count = 0;
        foreach (var tick in ticks)
            if (tick > floor && tick <= now) count++;
        return count;
    }

    /// <summary>Las listas se llenan en orden ascendente, así que se poda por el frente.</summary>
    private void Prune(List<int> ticks, int now)
    {
        var floor = now - _windowTicks;
        var drop = 0;
        while (drop < ticks.Count && ticks[drop] <= floor) drop++;
        if (drop > 0) ticks.RemoveRange(0, drop);
    }
}
