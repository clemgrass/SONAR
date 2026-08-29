using System.Collections.Generic;
using Sonar.Controller.Presentation;
using Sonar.Core.Spatial;

namespace Sonar.Autopilot;

/// <summary>Lo que el piloto sabe de un contacto: dónde estaba, cuándo, y hacia dónde va.</summary>
public readonly record struct ReadingEstimate(Vec2 Position, Vec2 VelocityPerTick, int ObservedAtTick);

/// <summary>
/// Recuerda las dos últimas lecturas distintas de cada contacto. Es lo único que el piloto
/// arrastra entre ticks, y es exactamente lo que arrastra un jugador en la cabeza: dónde vi
/// esto la vez pasada, y dónde lo veo ahora.
///
/// Lista ordenada por id y búsqueda lineal, nunca Dictionary: el orden de enumeración de un
/// Dictionary no está garantizado y el piloto tiene que ser tan determinista como la
/// simulación. Con un puñado de contactos el costo es irrelevante.
/// </summary>
public sealed class ContactTracker
{
    private sealed class Tracked
    {
        public int Id;
        public Vec2 Previous;
        public int PreviousTick;
        public Vec2 Latest;
        public int LatestTick;
        public bool HasPrevious;
    }

    private readonly List<Tracked> _tracked = new();

    /// <summary>
    /// Se llama cada tick. Una lectura es nueva cuando cambia su tick de observación, que
    /// sale de `snapshot.Tick - contact.AgeTicks` — SnapshotBuilder calcula AgeTicks como
    /// esa resta, así que la cuenta es exacta y no una heurística.
    /// </summary>
    public void Observe(WorldSnapshot snapshot)
    {
        foreach (var contact in snapshot.Contacts)
        {
            var observedAt = snapshot.Tick - contact.AgeTicks;
            var tracked = Find(contact.Id);

            if (tracked is null)
            {
                Insert(new Tracked
                {
                    Id = contact.Id,
                    Latest = contact.Position,
                    LatestTick = observedAt,
                    HasPrevious = false,
                });
                continue;
            }

            if (observedAt == tracked.LatestTick) continue;

            tracked.Previous = tracked.Latest;
            tracked.PreviousTick = tracked.LatestTick;
            tracked.Latest = contact.Position;
            tracked.LatestTick = observedAt;
            tracked.HasPrevious = true;
        }
    }

    /// <summary>Falso mientras haya una sola lectura: sin dos no hay velocidad que estimar.</summary>
    public bool TryEstimate(int contactId, out ReadingEstimate estimate)
    {
        var tracked = Find(contactId);
        if (tracked is null || !tracked.HasPrevious)
        {
            estimate = default;
            return false;
        }

        var span = tracked.LatestTick - tracked.PreviousTick;
        var delta = tracked.Latest - tracked.Previous;
        estimate = new ReadingEstimate(tracked.Latest, delta * (1.0 / span), tracked.LatestTick);
        return true;
    }

    public static Vec2 Extrapolate(ReadingEstimate estimate, double tick) =>
        estimate.Position + estimate.VelocityPerTick * (tick - estimate.ObservedAtTick);

    private Tracked? Find(int id)
    {
        foreach (var tracked in _tracked)
            if (tracked.Id == id) return tracked;
        return null;
    }

    /// <summary>Inserta manteniendo el orden por id ascendente, que es el orden de desempate.</summary>
    private void Insert(Tracked tracked)
    {
        for (var i = 0; i < _tracked.Count; i++)
        {
            if (_tracked[i].Id <= tracked.Id) continue;
            _tracked.Insert(i, tracked);
            return;
        }
        _tracked.Add(tracked);
    }
}
