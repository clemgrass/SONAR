using System.Collections.Generic;
using Sonar.Core;
using Sonar.Core.Entities;
using Sonar.Core.Spatial;

namespace Sonar.Controller;

/// <summary>
/// El escenario de apertura de M1. Vive acá y no en el proyecto de Godot para que los tests
/// puedan ejercitar el escenario <b>real</b> en vez de una copia — una copia se desincroniza
/// y deja de probar lo que se juega.
///
/// Estos números son material de M1: la pregunta 3 (cuántos contactos son jugables) se
/// responde tocándolos y volviendo a jugar.
/// </summary>
public static class OpeningScenario
{
    /// <summary>
    /// Avance del submarino, y con él la deriva del mundo. **No es un detalle de sabor.**
    /// La deriva se suma a la velocidad de todos los contactos, así que este número fija
    /// cuánto dura un encuentro.
    /// </summary>
    public const double SubSpeed = 0.012;

    public static readonly Bearing Course = new(0);

    /// <summary>
    /// Ticks de gracia antes del primer disparo enemigo, encima del intervalo normal.
    ///
    /// Sin esto el jugador muere a los diez segundos sin tocar nada: con un intervalo de
    /// 420 ticks (7 s) y torpedos de ~1,7 s de vuelo, los dos primeros impactos llegan
    /// antes de la tercera vuelta del haz. Medido, no supuesto — el submarino se destruía
    /// en el tick 582.
    ///
    /// M1 mide si extrapolar lecturas es satisfactorio, y para eso hace falta alcanzar a
    /// leer, extrapolar, disparar y ver el resultado. La presión viene después.
    /// </summary>
    public const int FirstFireGraceTicks = 1800;

    /// <summary>
    /// Cuatro enemigos y un pecio. Las velocidades propias casi cancelan la deriva: el neto
    /// es del orden de 0,003 por segundo, de modo que un contacto tarda minutos en cruzar
    /// una banda y hay tiempo real para extrapolar.
    ///
    /// Con velocidades más altas el escenario se degenera: todo lo que empieza al norte cae
    /// encima del submarino en pocos segundos y no se alcanza a leer nada. Lo pincha
    /// OpeningScenarioTests.TheEncounterLastsLongEnoughToPlayIt.
    /// </summary>
    public static IEnumerable<Contact> Contacts(SimulationConfig config)
    {
        var spawner = new ContactSpawner(config);

        var enemies = spawner.SpawnEnemies(
            spawnTick: 0, firstId: 100,
            positions: new[]
            {
                new Vec2(0.10, -0.78),   // norte
                new Vec2(0.74, 0.18),    // este
                new Vec2(-0.62, 0.44),   // suroeste
                new Vec2(-0.30, -0.70),  // noroeste
            },
            velocities: new[]
            {
                new Vec2(0.0008, -0.0110),
                new Vec2(-0.0016, -0.0120),
                new Vec2(0.0011, -0.0132),
                new Vec2(0.0004, -0.0114),
            });

        foreach (var enemy in enemies)
            yield return enemy with { NextFireTick = enemy.NextFireTick + FirstFireGraceTicks };

        // Pecio de referencia: no tiene velocidad propia, así que recibe la deriva pura y
        // cruza el scope de norte a sur. Es lo que hace legible la anomalía del §25 — deja
        // una defensa a su lado y míralos separarse (spec §4.9).
        yield return Contact.CreateInert(new ContactId(200), new Vec2(0.22, -0.88));
    }
}
