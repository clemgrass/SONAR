using System;
using System.Collections.Generic;
using Sonar.Core;
using Sonar.Core.Entities;
using Sonar.Core.Spatial;

namespace Sonar.Controller;

/// <summary>
/// Guion del gauntlet de M1: oleadas que doblan hasta que el jugador muere. Vive en el
/// Controller y no en Core porque es guion de misión, no regla — la simulación recibe
/// contactos y no se entera de que existen oleadas.
///
/// En qué oleada muere el jugador **es** la respuesta a la pregunta 3 de M1. Estos números
/// son material de medición: se ajustan con datos, no con opinión.
/// </summary>
public sealed class WaveDirector
{
    /// <summary>Silencio inicial. Diez segundos para orientarse antes de que entre nada.</summary>
    public const int OpeningQuietTicks = 600;

    /// <summary>Respiro entre que se limpia una oleada y entra la siguiente.</summary>
    public const int BetweenWavesTicks = 300;

    /// <summary>
    /// Radio de entrada. Justo por dentro del borde: a 1.0 exacto el gating del sonar y la
    /// IA enemiga descartan el contacto por estar fuera de alcance.
    /// </summary>
    public const double SpawnRadius = 0.99;

    /// <summary>Primer id de contacto. Deja libre el rango bajo para escenarios de test.</summary>
    private const int FirstContactId = 100;

    /// <summary>
    /// Tope de cordura para el tamaño de una oleada. El diseño real es "duplica hasta que el
    /// jugador muere", y ninguna partida real llega ni remotamente cerca de esto: el jugador
    /// muere mucho antes de una oleada de cientos de contactos. Pero sin un techo, duplicar
    /// sin límite es una bomba de memoria en cualquier corrida que agote muchos ticks sin
    /// que el jugador muera — <c>RunClearingInstantly</c> en los tests hace exactamente eso
    /// a propósito, para poder inspeccionar muchas oleadas rápido. Se confirmó empíricamente:
    /// sin este tope, `dotnet test` agotaba varios GB de RAM antes de la oleada 30.
    /// </summary>
    private const int MaxWaveSize = 512;

    /// <summary>
    /// Techo del reparto de un escalonamiento completo. El escalonamiento existe para
    /// garantizar que una oleada no dispare toda en el mismo tick — no para marcar el ritmo.
    /// Sin techo, el incremento por contacto escala con el tamaño de la oleada y el último
    /// de una oleada grande queda armado decenas de minutos después de entrar, o sea nunca.
    ///
    /// El valor está elegido para que las oleadas 1 a 4 queden idénticas a como estaban:
    /// la 4 son ocho contactos y su reparto completo son 7 x 300 = 2100 ticks, justo el
    /// techo. Sólo recorta de la oleada 5 en adelante, que es territorio sin medir.
    /// </summary>
    private const int MaxStaggerSpanTicks = 2100;

    // Velocidad de cierre NETA deseada, ya descontada la deriva del mundo. La oleada 1 casi
    // no se mueve para que un jugador lento igual alcance a disparar.
    private const double ClosingBase = 0.006;
    private const double ClosingStep = 0.007;

    /// <summary>
    /// Techo de la velocidad de cierre, como fracción de la velocidad del torpedo enemigo.
    /// Un casco nunca puede acercarse a más de la mitad de lo que vuela un torpedo, así el
    /// torpedo se lee como lo rápido de la escena y el submarino como lo lento.
    ///
    /// Sin techo, la escalada por oleada cruzaba la velocidad del propio torpedo alrededor
    /// de la oleada 5: los cascos alcanzaban a sus municiones y el contraste desaparecía.
    /// El techo recorta arriba y deja intacto el arranque suave — la oleada 1 sigue casi
    /// quieta, que es la que enseña a disparar.
    /// </summary>
    private const double ClosingCeilingOfTorpedoSpeed = 0.5;

    /// <summary>
    /// La composición de una oleada. La dificultad deja de subir duplicando la cantidad
    /// —que es lo que convertía la oleada 4 en una pared— y pasa a subir cambiando la mezcla,
    /// que es lo que gobierna cuántos torpedos pueden estar en el agua a la vez.
    ///
    /// El contacto `i` de una oleada de `count` recibe la personalidad `i % receta.Count`.
    /// </summary>
    private static IReadOnlyList<Personality> RecipeFor(int wave) => wave switch
    {
        1 => new[] { PersonalityCatalog.Stalker },
        2 => new[] { PersonalityCatalog.Stalker, PersonalityCatalog.Picket },
        3 => new[]
        {
            PersonalityCatalog.Stalker, PersonalityCatalog.Picket,
            PersonalityCatalog.Stalker, PersonalityCatalog.Goad,
        },
        4 => new[]
        {
            PersonalityCatalog.Stalker, PersonalityCatalog.Picket,
            PersonalityCatalog.Goad, PersonalityCatalog.Ram,
        },
        _ => new[]
        {
            PersonalityCatalog.Stalker, PersonalityCatalog.Picket,
            PersonalityCatalog.Goad, PersonalityCatalog.Ram,
            PersonalityCatalog.Hunter,
        },
    };

    private readonly SimulationConfig _config;
    private readonly IRandomSource _random;
    private readonly Vec2 _worldDrift;

    private int _nextContactId = FirstContactId;
    private int _nextSpawnTick = OpeningQuietTicks;
    private bool _waitingForClear;

    public int Wave { get; private set; }

    public WaveDirector(SimulationConfig config, IRandomSource random, Bearing course, double subSpeed)
    {
        _config = config;
        _random = random;
        _worldDrift = course.ToUnitVector() * -subSpeed;
    }

    /// <summary>
    /// Devuelve los contactos que entran en este tick, o vacío. El llamador le informa
    /// cuántos enemigos siguen vivos; el director no consulta el estado del mundo.
    /// </summary>
    public IReadOnlyList<Contact> Advance(int tick, int liveEnemies)
    {
        if (_waitingForClear)
        {
            if (liveEnemies > 0) return Array.Empty<Contact>();
            _waitingForClear = false;
            _nextSpawnTick = tick + BetweenWavesTicks;
            return Array.Empty<Contact>();
        }

        if (tick < _nextSpawnTick) return Array.Empty<Contact>();

        Wave++;
        _waitingForClear = true;

        // Exponente acotado antes de desplazar: sin esto, en corridas muy largas
        // (Wave - 1 >= 31) el corrimiento de un int se envuelve y produce un tamaño de
        // oleada erróneo en vez de simplemente grande. Con el exponente ya acotado a 30,
        // el corrimiento nunca se envuelve y MaxWaveSize hace el resto del trabajo.
        var exponent = Math.Min(Wave - 1, 30);
        var count = Math.Min(1 << exponent, MaxWaveSize);
        return Spawn(count, tick);
    }

    private IReadOnlyList<Contact> Spawn(int count, int spawnTick)
    {
        var ceiling = _config.EnemyTorpedoSpeed * ClosingCeilingOfTorpedoSpeed;
        var closing = Math.Min(ClosingBase + (Wave - 1) * ClosingStep, ceiling);
        var spawned = new List<Contact>(count);
        var recipe = RecipeFor(Wave);

        // Un sector por contacto, con temblor acotado a la mitad central del sector. Eso
        // garantiza una separación mínima de medio sector sin dejar las entradas repartidas
        // de forma perfectamente regular, que se leería como artificial.
        var sector = 360.0 / count;
        var offset = _random.NextDouble() * 360.0;

        // Reparto acotado: separación garantizada, costo acotado.
        var step = count > 1
            ? Math.Max(1, Math.Min((count - 1) * _config.EnemyFireStaggerTicks, MaxStaggerSpanTicks) / (count - 1))
            : 0;

        for (var i = 0; i < count; i++)
        {
            var jitter = (_random.NextDouble() - 0.5) * sector * 0.5;
            var degrees = (offset + i * sector + jitter + 360.0) % 360.0;

            var radians = degrees * Math.PI / 180.0;
            var outward = new Vec2(Math.Sin(radians), -Math.Cos(radians));

            spawned.Add(new Contact
            {
                Id = new ContactId(_nextContactId++),
                Kind = ContactKind.Enemy,
                Position = outward * SpawnRadius,
                // La deriva se suma a la velocidad de todos los contactos, así que se resta
                // acá para que el cierre neto sea el pedido. Sin esto el escenario degenera:
                // todo lo que entra por un lado cae encima del submarino en segundos.
                Velocity = outward * -closing - _worldDrift,
                Alive = true,
                // Relativo a la aparición, no absoluto. Con el valor absoluto que había
                // antes, el plazo se consumía oleada a oleada y de la cuarta en adelante
                // los contactos nacían armados y disparaban en el tick en que entraban.
                //
                // El escalonamiento es determinista y no toca el generador de azar: sin él
                // toda la oleada dispara en el mismo tick y la presión llega sincronizada
                // en vez de sostenida.
                NextFireTick = spawnTick
                               + _config.EnemyFireIntervalTicks
                               + i * step,
                Personality = recipe[i % recipe.Count],
                LastFixTick = spawnTick,
            });
        }

        return spawned;
    }
}
