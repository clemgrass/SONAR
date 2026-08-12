using System;
using System.Collections.Generic;
using Sonar.Core.Entities;
using Sonar.Core.Spatial;

namespace Sonar.Core;

/// <summary>
/// Crea grupos de contactos con sus tiempos de disparo escalonados. El escalonado es
/// determinista y no usa el generador de azar: sin él todos los enemigos de un grupo
/// dispararían en el mismo tick y la presión llegaría en oleadas sincronizadas
/// en vez de sostenida (TDD §4.6).
/// </summary>
public sealed class ContactSpawner
{
    private readonly SimulationConfig _config;

    public ContactSpawner(SimulationConfig config) => _config = config;

    public IEnumerable<Contact> SpawnEnemies(
        int spawnTick, int firstId, IReadOnlyList<Vec2> positions, IReadOnlyList<Vec2> velocities)
    {
        if (positions.Count != velocities.Count)
            throw new ArgumentException(
                "positions y velocities deben tener la misma longitud.", nameof(velocities));

        for (var i = 0; i < positions.Count; i++)
        {
            yield return new Contact
            {
                Id = new ContactId(firstId + i),
                Kind = ContactKind.Enemy,
                Position = positions[i],
                Velocity = velocities[i],
                Alive = true,
                NextFireTick = spawnTick
                               + _config.EnemyFireIntervalTicks
                               + i * _config.EnemyFireStaggerTicks,
            };
        }
    }
}
