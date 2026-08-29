using Sonar.Core;
using Sonar.Core.Spatial;

namespace Sonar.Autopilot;

/// <summary>Un disparo tecleable: la banda y la marcación en grados enteros.</summary>
public readonly record struct FiringSolution(Bearing Bearing, Band Band, double ErrorRadius);

/// <summary>
/// Resuelve el encuentro entre el torpedo y el blanco. El torpedo no persigue: sale del borde
/// de la zona muerta, vuela recto por una marcación y detona en el CENTRO de la banda pedida.
/// Así que apuntar no es "hacia dónde está" sino "qué par (marcación, banda) hace que el punto
/// de detonación y el blanco coincidan en el mismo instante".
/// </summary>
public static class FiringSolver
{
    public static bool TrySolve(
        SimulationConfig config, BandLayout layout,
        ReadingEstimate estimate, int nowTick, out FiringSolution solution)
    {
        var perTick = config.TorpedoSpeed * config.TickDurationSeconds;
        var best = default(FiringSolution);
        var bestError = double.MaxValue;

        for (var i = 0; i < layout.Count; i++)
        {
            var band = new Band(i);
            var center = layout.CenterRadius(band);

            // El torpedo nace en el borde de la zona muerta, no en el origen: por eso se resta.
            var flightTicks = (center - layout.DeadZoneRadius) / perTick;
            var predicted = ContactTracker.Extrapolate(estimate, nowTick + flightTicks);

            // La marcación se redondea a grado entero porque eso es lo que se puede teclear.
            var bearing = Bearing.FromVector(predicted);
            var detonation = bearing.ToUnitVector() * center;
            var error = Vec2.Distance(predicted, detonation);

            if (error >= bestError) continue;
            bestError = error;
            best = new FiringSolution(bearing, band, error);
        }

        solution = best;

        // Fuera del radio de explosión el disparo es una esperanza, no una solución. El
        // cargador tiene tres tubos y recargar cuesta un comando: no se gasta en esperanzas.
        return bestError < config.BlastRadius;
    }
}
