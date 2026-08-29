using System;
using Sonar.Controller.Presentation;
using Sonar.Core;
using Sonar.Core.Spatial;

namespace Sonar.Autopilot;

/// <summary>
/// El piloto. Decide un comando por turno leyendo <b>únicamente</b> el snapshot: lecturas
/// envejecidas y con gating, lo mismo que ve el jugador.
///
/// Se llama Pilot y no Autopilot porque `Autopilot` ya es un segmento del namespace
/// `Sonar.Autopilot`: desde `Sonar.Autopilot.Tests` o desde `Sonar.Game`, el nombre a secas
/// resuelve al namespace y no al tipo, y no compila (CS0118).
///
/// Que no reciba el GameController no es estilo: es el enforcement. Con él a mano alcanzaría
/// LiveEnemyCount, que cuenta enemigos que el jugador quizá nunca leyó, y la pared que midiera
/// este bot sería la de otro juego. Con esta firma, hacer trampa no compila — el mismo
/// mecanismo por el que ContactView impide que la presentación dibuje la verdad.
/// </summary>
public sealed class Pilot
{
    /// <summary>
    /// Separación por debajo de la cual dos disparos se consideran el mismo. Tres grados es
    /// más que el redondeo a grado entero y menos que la separación entre dos contactos que
    /// valga la pena atacar por separado.
    /// </summary>
    private const double EngagedSeparationDegrees = 3.0;

    /// <summary>
    /// Barridos de antigüedad tras los cuales una lectura deja de servir para apuntar.
    ///
    /// Hace falta porque el solucionador no puede detectar el problema solo: mide el error
    /// contra la posición PREDICHA, y una predicción hecha desde una lectura rancia es
    /// internamente consistente y falsa. Un contacto sale de `snapshot.Contacts` sólo al
    /// morir —nunca por salirse del alcance— y el barrido no refresca nada más allá del
    /// radio 1.0, así que sin esta compuerta el piloto le dispara con confianza a un
    /// fantasma de hace un minuto.
    ///
    /// Tres barridos: dos son el mínimo para tener velocidad, así que con menos se
    /// rechazarían blancos recién adquiridos.
    /// </summary>
    private const double MaxReadingAgeSweeps = 3.0;

    private readonly SimulationConfig _config;
    private readonly BandLayout _layout;
    private readonly ContactTracker _tracker = new();
    private readonly double _maxReadingAgeTicks;

    public Pilot(SimulationConfig config)
    {
        _config = config;
        _layout = config.BuildBandLayout();
        _maxReadingAgeTicks =
            MaxReadingAgeSweeps * config.SweepPeriodSeconds / config.TickDurationSeconds;
    }

    /// <summary>
    /// Se llama CADA tick. Va aparte de Decide porque los dos tienen ritmos distintos: el
    /// rastreador no puede perderse lecturas por estar el presupuesto agotado.
    /// </summary>
    public void Observe(WorldSnapshot snapshot) => _tracker.Observe(snapshot);

    /// <summary>Comando del turno, o null si no hay nada que valga la pena teclear.</summary>
    public string? Decide(WorldSnapshot snapshot) => DecideFire(snapshot);

    private string? DecideFire(WorldSnapshot snapshot)
    {
        // Comandos que la simulación rechazaría no se emiten. Un rechazo no es una respuesta,
        // y contarlo inflaría el denominador del índice de saturación hasta hacerlo mentir.
        if (snapshot.ReloadInProgress || snapshot.AmmoLoaded <= 0) return null;

        var bestRadius = double.MaxValue;
        var best = default(FiringSolution);
        var found = false;

        // snapshot.Contacts viene ordenada por id ascendente —SonarState garantiza el orden
        // de las lecturas— y la comparación de abajo es estricta. Con eso el desempate queda
        // por id ascendente, como pide el §3 del contrato.
        foreach (var contact in snapshot.Contacts)
        {
            if (contact.AgeTicks > _maxReadingAgeTicks) continue;
            if (!_tracker.TryEstimate(contact.Id, out var estimate)) continue;
            if (!FiringSolver.TrySolve(_config, _layout, estimate, snapshot.Tick, out var solution)) continue;
            if (AlreadyEngaged(snapshot, solution)) continue;

            var radius = ContactTracker.Extrapolate(estimate, snapshot.Tick).Length;
            if (radius >= bestRadius) continue;

            bestRadius = radius;
            best = solution;
            found = true;
        }

        return found ? $"{best.Band.Letter}{best.Bearing.Degrees:D3}" : null;
    }

    /// <summary>¿Ya hay un torpedo propio volando a esta misma solución?</summary>
    private bool AlreadyEngaged(WorldSnapshot snapshot, FiringSolution solution)
    {
        var run = _layout.CenterRadius(solution.Band);
        foreach (var torpedo in snapshot.Torpedoes)
        {
            if (Math.Abs(torpedo.RunToRadius - run) > 1e-9) continue;
            if (Bearing.Separation(torpedo.BearingDegrees, solution.Bearing.Degrees)
                <= EngagedSeparationDegrees) return true;
        }
        return false;
    }
}
