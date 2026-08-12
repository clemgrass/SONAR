using System;
using System.Collections.Generic;
using System.Linq;
using Sonar.Core.Entities;
using Sonar.Core.Sensing;
using Sonar.Core.Spatial;

namespace Sonar.Core;

/// <summary>
/// Estado completo de la simulación en un tick. Inmutable: cada tick produce uno nuevo.
///
/// Todas las colecciones son listas ordenadas por id ascendente. Nunca Dictionary ni
/// HashSet para nada que se itere durante la simulación: su orden de enumeración no está
/// garantizado por contrato y eso rompería el determinismo (TDD §2.5).
/// </summary>
public sealed record WorldState
{
    public required int Tick { get; init; }
    public required PlayerSub Sub { get; init; }
    public required IReadOnlyList<Contact> Contacts { get; init; }
    public required IReadOnlyList<Torpedo> Torpedoes { get; init; }
    public required IReadOnlyList<Defense> Defenses { get; init; }
    public required SonarState Sonar { get; init; }
    public required int NextEntityId { get; init; }

    public static WorldState CreateInitial(SimulationConfig config, Bearing course, double speed) => new()
    {
        Tick = 0,
        Sub = PlayerSub.Create(config, course, speed),
        Contacts = Array.Empty<Contact>(),
        Torpedoes = Array.Empty<Torpedo>(),
        Defenses = Array.Empty<Defense>(),
        Sonar = SonarState.CreateInitial(),
        NextEntityId = 1,
    };

    /// <summary>Reserva el siguiente id y devuelve el estado avanzado.</summary>
    public (int Id, WorldState State) TakeEntityId() =>
        (NextEntityId, this with { NextEntityId = NextEntityId + 1 });

    public WorldState WithContacts(IEnumerable<Contact> contacts) =>
        this with { Contacts = contacts.OrderBy(c => c.Id.Value).ToArray() };

    public WorldState WithTorpedoes(IEnumerable<Torpedo> torpedoes) =>
        this with { Torpedoes = torpedoes.OrderBy(t => t.Id.Value).ToArray() };

    public WorldState WithDefenses(IEnumerable<Defense> defenses) =>
        this with { Defenses = defenses.OrderBy(d => d.Id.Value).ToArray() };
}
