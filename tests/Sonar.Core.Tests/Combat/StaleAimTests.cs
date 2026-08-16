using System;
using System.Collections.Generic;
using System.Linq;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Combat;

public class StaleAimTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    // [Defecto de plan corregido] El plan original ponía el contacto en (0.0, -0.90), que
    // cae exactamente sobre el eje del rumbo de Moving() (norte puro, Bearing(0)). Ahí la
    // deriva es puramente vertical y el contacto también, así que cualquier desplazamiento a
    // lo largo de esa misma recta —correcto o no— es colineal con el rayo contacto-origen y
    // por tanto invisible para un chequeo de dirección: el torpedo apunta igual sin importar
    // la antigüedad o el AimLead. Es el mismo defecto de "marcación 000" que ya atrapó este
    // contrato antes (ver AGENT_CONTRACT.md §4). Sacar el contacto del eje es lo que permite
    // que estos tests detecten tanto el envejecimiento como su compensación.
    private static readonly Vec2 StartPosition = new(0.30, -0.85);

    // [Defecto de plan corregido, tarea 12] Con el gate de FiringRadius conectado, un
    // contacto viejo (lastFixTick = -600) mide su alcance contra una estimación que se
    // corrió hacia atrás en el rumbo, y eso puede empujarlo fuera de su radio de
    // compromiso incluso partiendo de StartPosition. Le pasa al PICKET (su radio es 0.95 y
    // la estimación corrida lo deja justo por fuera) y le pasa siempre al HUNTER (compensa
    // del todo, así que su centro es el origen exacto y su radio, 0.60, es el más chico del
    // roster). Estos tests miden dirección de puntería, no alcance, así que la corrección es
    // acercar el contacto — no sacarle la personalidad, que es justo lo que están probando.
    // Se queda fuera del eje por la misma razón que StartPosition.
    private static readonly Vec2 CloseStartPosition = new(0.15, -0.48);

    private static Contact Enemy(Personality? personality, int lastFixTick, Vec2? position = null) => new()
    {
        Id = new ContactId(1),
        Kind = ContactKind.Enemy,
        Position = position ?? StartPosition,
        Velocity = Vec2.Zero,
        Alive = true,
        NextFireTick = 0,
        LastFixTick = lastFixTick,
        Personality = personality,
    };

    /// <summary>
    /// Dirección real hacia el origen, calculada desde la posición del contacto ya con la
    /// deriva de este tick aplicada (la fase 4 mueve los contactos antes de que la fase 5
    /// dispare). Usar la posición cruda de spawn en vez de ésta introduciría un desvío del
    /// orden de 1e-4 apenas el contacto no está sobre el eje del rumbo, y rompería la
    /// comparación en precision:6 por una razón ajena a lo que el test quiere medir.
    /// </summary>
    private static Vec2 TowardOriginFrom(Simulation sim) =>
        (Vec2.Zero - sim.State.Contacts.Single(c => c.Id.Value == 1).Position).Normalized();

    [Fact]
    public void AContactWithoutPersonalityStillAimsStraightAtTheOrigin()
    {
        // El comportamiento de siempre tiene que seguir intacto: es lo que sostiene a los
        // tests anteriores.
        var sim = TestWorld.Moving();
        sim.SetContacts(new[] { Enemy(null, lastFixTick: 0) });
        sim.Tick(NoCommands);

        var torpedo = sim.State.Torpedoes.Single(t => t.Side == TorpedoSide.Enemy);
        var towardOrigin = TowardOriginFrom(sim);
        Assert.Equal(towardOrigin.X, torpedo.Direction.X, precision: 6);
        Assert.Equal(towardOrigin.Y, torpedo.Direction.Y, precision: 6);
    }

    [Fact]
    public void AStaleFixMakesTheTorpedoMissBehindThePlayer()
    {
        // Con el submarino en movimiento, un contacto viejo envejece hacia atrás en el rumbo.
        // lastFixTick negativo = "te escuchó hace 600 ticks", diez segundos de antigüedad.
        var sim = TestWorld.Moving();
        sim.SetContacts(new[]
            { Enemy(PersonalityCatalog.Picket, lastFixTick: -600, position: CloseStartPosition) });
        sim.Tick(NoCommands);

        var torpedo = sim.State.Torpedoes.Single(t => t.Side == TorpedoSide.Enemy);
        var towardOrigin = TowardOriginFrom(sim);

        var dot = torpedo.Direction.X * towardOrigin.X + torpedo.Direction.Y * towardOrigin.Y;
        Assert.True(dot < 0.9999, $"el torpedo apuntó al origen igual (dot={dot:F6})");
    }

    [Fact]
    public void TheHunterCompensatesAndStillAimsAtThePlayer()
    {
        // Misma antigüedad y misma posición cercana que el test anterior, pero AimLead = 1.
        // Al HUNTER la posición cercana le hace falta siempre, no sólo por la antigüedad:
        // compensa la deriva del todo, así que mide su alcance contra el origen exacto, y su
        // radio de compromiso —0.60— es el más chico del roster.
        var sim = TestWorld.Moving();
        sim.SetContacts(new[]
            { Enemy(PersonalityCatalog.Hunter, lastFixTick: -600, position: CloseStartPosition) });
        sim.Tick(NoCommands);

        var torpedo = sim.State.Torpedoes.Single(t => t.Side == TorpedoSide.Enemy);
        var towardOrigin = TowardOriginFrom(sim);
        Assert.Equal(towardOrigin.X, torpedo.Direction.X, precision: 6);
        Assert.Equal(towardOrigin.Y, torpedo.Direction.Y, precision: 6);
    }

    [Fact]
    public void AFreshFixAimsAtThePlayerEvenWithoutCompensating()
    {
        var sim = TestWorld.Moving();
        sim.SetContacts(new[] { Enemy(PersonalityCatalog.Picket, lastFixTick: 0) });
        sim.Tick(NoCommands);

        var torpedo = sim.State.Torpedoes.Single(t => t.Side == TorpedoSide.Enemy);
        var towardOrigin = TowardOriginFrom(sim);
        Assert.Equal(towardOrigin.X, torpedo.Direction.X, precision: 6);
        Assert.Equal(towardOrigin.Y, torpedo.Direction.Y, precision: 6);
    }
}
