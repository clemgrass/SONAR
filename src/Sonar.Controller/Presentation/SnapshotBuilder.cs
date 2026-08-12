using System.Collections.Generic;
using System.Linq;
using Sonar.Core;
using Sonar.Core.Entities;
using Sonar.Core.Spatial;

namespace Sonar.Controller.Presentation;

public static class SnapshotBuilder
{
    /// <summary>
    /// Letra que se muestra cuando la lectura cae fuera de toda banda — dentro de la zona
    /// muerta o más allá del scope. Se marca en vez de inventar una banda, porque la UI
    /// necesita poder distinguir ese caso.
    /// </summary>
    private const char NoBand = '-';

    public static WorldSnapshot Build(WorldState state, BandLayout layout)
    {
        var contacts = new List<ContactView>();
        foreach (var reading in state.Sonar.Readings)
        {
            var bearing = Bearing.FromVector(reading.Position);
            var band = layout.TryBandAt(reading.Position.Length, out var b) ? b.Letter : NoBand;
            contacts.Add(new ContactView(
                reading.ContactId.Value,
                reading.Position,
                bearing.Degrees,
                band,
                state.Tick - reading.ObservedAtTick));
        }

        var own = state.Torpedoes
            .Where(t => t.Side == TorpedoSide.Own)
            .Select(t => new TorpedoView(t.Id.Value, t.Position, t.Bearing.Degrees, t.RunToRadius))
            .ToArray();

        var enemy = state.Torpedoes
            .Where(t => t.Side == TorpedoSide.Enemy)
            .Select(t => new EnemyTorpedoView(t.Id.Value, t.Position))
            .ToArray();

        var defenses = state.Defenses
            .Where(d => d.Alive)
            .Select(d => new DefenseView(d.Id.Value, d.Position))
            .ToArray();

        return new WorldSnapshot(
            state.Tick,
            state.Sonar.SweepDegrees,
            state.Sonar.SweepRunning,
            contacts,
            own,
            enemy,
            defenses,
            state.Sub.AmmoLoaded,
            state.Sub.AmmoReserve,
            state.Sub.DefensesAvailable,
            state.Sub.ReloadInProgress,
            state.Sub.HitsTaken,
            state.Sub.Destroyed);
    }
}
