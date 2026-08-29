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

            // El reloj retrocedió: es una corrida nueva. RETRY reconstruye la Simulation
            // —el tick vuelve a cero— y también el WaveDirector, así que los ids de contacto
            // vuelven a empezar en 100. Sin olvidar acá, la entrada del contacto 100 del
            // intento anterior recibiría esta lectura como si fuera la continuación de la
            // suya, con un span negativo y una velocidad inventada.
            if (observedAt < tracked.LatestTick)
            {
                tracked.Latest = contact.Position;
                tracked.LatestTick = observedAt;
                tracked.HasPrevious = false;
                continue;
            }

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

        // Estrictamente positivo por construcción: Observe descarta las lecturas repetidas
        // por igualdad y reinicia el rastreo cuando el reloj retrocede, así que acá nunca
        // llega un span cero o negativo. Lo sostiene ARetryStartsTheTrackingOver, no un
        // chequeo defensivo que nadie podría ejercitar.
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

    /// <summary>
    /// Se agrega al final y no en orden de id, porque el orden de esta lista no lo lee nadie:
    /// Find es su único consumidor y busca por id. La regla de determinismo del §3 del
    /// contrato se cumple por no usar Dictionary ni HashSet, no por mantener un orden que
    /// ningún recorrido observa. Si algún día algo la enumera, ahí habrá que ordenarla — y
    /// testear ese orden, que es lo que faltaba cuando esto insertaba ordenado.
    /// </summary>
    private void Insert(Tracked tracked) => _tracked.Add(tracked);
}
