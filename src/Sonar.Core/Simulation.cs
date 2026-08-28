using System;
using System.Collections.Generic;
using System.Linq;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;

namespace Sonar.Core;

public interface ISimulation
{
    WorldState State { get; }

    /// <summary>Avanza exactamente un tick, consumiendo los comandos dados.</summary>
    IReadOnlyList<GameEvent> Tick(IReadOnlyList<Command> commands);
}

/// <summary>
/// Bucle de simulación. Nunca ve tiempo de pared: avanza en pasos fijos de
/// config.TickDurationSeconds. Acumular tiempo real es trabajo del Controller.
/// </summary>
public sealed class Simulation : ISimulation
{
    private readonly SimulationConfig _config;
    private readonly BandLayout _layout;
    private readonly IRandomSource _random;

    public WorldState State { get; private set; }

    public Simulation(SimulationConfig config, IRandomSource random, WorldState initial)
    {
        config.Validate();
        _config = config;
        _layout = config.BuildBandLayout();
        _random = random;
        State = initial;
    }

    /// <summary>Solo para tests y para el guion de misión: coloca contactos de golpe.</summary>
    public void SetContacts(IEnumerable<Contact> contacts) => State = State.WithContacts(contacts);

    /// <summary>Suma contactos a los que ya están. Lo usa el director de oleadas.</summary>
    public void AddContacts(IEnumerable<Contact> contacts) =>
        State = State.WithContacts(State.Contacts.Concat(contacts));

    public void SetTorpedoes(IEnumerable<Torpedo> torpedoes) => State = State.WithTorpedoes(torpedoes);

    public void SetDefenses(IEnumerable<Defense> defenses) => State = State.WithDefenses(defenses);

    /// <summary>
    /// Orden de actualización fijo, según TDD §3.2. Cambiarlo cambia el resultado de la
    /// simulación y rompe el determinismo respecto a corridas anteriores.
    /// </summary>
    public IReadOnlyList<GameEvent> Tick(IReadOnlyList<Command> commands)
    {
        if (State.Sub.Destroyed) return Array.Empty<GameEvent>();

        var events = new List<GameEvent>();
        var state = State;
        var dt = _config.TickDurationSeconds;

        // 1 · Aplicar comandos en el orden en que llegaron
        foreach (var command in commands) state = ApplyCommand(state, command, events);

        // 2 · Rumbo y velocidad: los fija el guion de misión, no este bucle

        // 3 · Deriva del mundo
        var worldDrift = state.Sub.Course.ToUnitVector() * -state.Sub.Speed;

        // Listas de trabajo. Se reconstruye el estado inmutable al final del tick.
        var contacts = state.Contacts.ToList();
        var torpedoes = state.Torpedoes.ToList();
        var defenses = state.Defenses.ToList();
        var sub = state.Sub;

        // 4 · Gobernar y mover contactos — reciben la deriva. Las defensas NO: esa omisión
        //     deliberada es la anomalía del §25 del GDD. Ver TDD §4.4. El gobierno va
        //     primero para que la velocidad que se integra sea la de este tick y no la
        //     del anterior.
        SteerContacts(contacts, torpedoes, state.Tick, worldDrift, dt);

        for (var i = 0; i < contacts.Count; i++)
        {
            var c = contacts[i];
            if (!c.Alive) continue;
            contacts[i] = c with { Position = c.Position + (c.Velocity + worldDrift) * dt };
        }

        // 5 · Disparo enemigo
        state = FireEnemyTorpedoes(state, contacts, torpedoes, events, worldDrift, dt);

        // 6 · Mover torpedos
        for (var i = 0; i < torpedoes.Count; i++)
        {
            var t = torpedoes[i];
            torpedoes[i] = t with { Position = t.Position + t.Direction * t.Speed * dt };
        }

        // 7 · Colisiones
        ResolveDirectHits(contacts, torpedoes, events, state.Tick);
        ResolveTorpedoCollisions(torpedoes, events, state.Tick);
        ResolveDefenseCollisions(contacts, torpedoes, defenses, events, state.Tick);

        // 8 · Corridas agotadas y detonaciones por radio
        ResolveRunOuts(contacts, torpedoes, events, state.Tick);

        // 9 · Impactos sobre el submarino
        sub = ResolveSubHits(sub, torpedoes, events, state.Tick);

        // 9b · Embestidas. Va después de los torpedos para que, si en el mismo tick te
        //      llegan las dos cosas, la causa reportada sea la que ya te había matado.
        sub = ResolveContactCollisions(sub, contacts, events, state.Tick);

        // 10 · Barrido y registro de lecturas
        var sonar = AdvanceSweepAndRecord(state.Sonar, contacts, state.Tick, dt, events);

        // 10b · Qué delata al jugador
        RefreshEnemyFixes(contacts, events, state.Tick);

        // 11 · Recarga
        sub = AdvanceReload(sub, state.Tick, events);

        // 12 · Retirar entidades muertas y fuera de rango
        contacts.RemoveAll(c => !c.Alive);
        torpedoes.RemoveAll(t => t.Position.Length > 1.0);
        defenses.RemoveAll(d => !d.Alive);

        // 13 · Reconstruir el estado inmutable y avanzar el contador.
        //      DefensesAvailable se deriva de las defensas vivas, no se lleva como contador
        //      aparte: así una defensa que se gastó devuelve el hueco sin que nadie tenga
        //      que acordarse de sumarlo.
        sub = sub with { DefensesAvailable = _config.DefenseCapacity - defenses.Count };
        state = state with { Sub = sub, Sonar = sonar, Tick = state.Tick + 1 };
        State = state.WithContacts(contacts).WithTorpedoes(torpedoes).WithDefenses(defenses);
        return events;
    }

    // --- Fases. Se llenan en las tareas siguientes de este plan. ---

    /// <summary>
    /// IA mínima de M1: cada enemigo dispara recto hacia el origen. No evade, no persigue y
    /// no elige blanco — el §63.11 completo va a un milestone posterior. Lo que M1 mide es
    /// al jugador bajo presión, no la inteligencia del enemigo.
    ///
    /// Juega con la misma <i>forma</i> de regla que el jugador pero peor cargador: <b>un solo
    /// torpedo, que se agota al dispararlo</b>. No tiene ráfaga posible — cada disparo lo
    /// deja vacío y obliga a recargar, y esa recarga (`config.EnemyReloadTicks`) es más
    /// lenta que la del jugador. Encima sigue el límite de uno en el agua: mientras el suyo
    /// viaje, no vuelve a disparar. Con torpedos lentos ése suele ser el que manda.
    ///
    /// La asimetría es deliberada. Con ambos lados al mismo reloj el jugador quedaba
    /// encadenado a esquivar torpedos sin ventana para calcular un disparo propio.
    ///
    /// `EnemyFireIntervalTicks` no gobierna la cadencia: sólo lo usa el WaveDirector para
    /// decidir cuánto tarda en armarse un contacto recién aparecido.
    /// </summary>
    private WorldState FireEnemyTorpedoes(
        WorldState state, List<Contact> contacts, List<Torpedo> torpedoes,
        List<GameEvent> events, Vec2 worldDrift, double dt)
    {
        for (var i = 0; i < contacts.Count; i++)
        {
            var contact = contacts[i];
            if (!contact.Alive) continue;
            if (contact.Kind != ContactKind.Enemy) continue;
            if (state.Tick < contact.NextFireTick) continue;
            if (contact.Position.Length > 1.0) continue;

            // Fuera de su rango de compromiso no dispara. Para el RAM ese rango es cero, que
            // es como se expresa "no dispara nunca": su amenaza se mide en distancia, no en
            // torpedos. Se compara contra su estimación y no contra el origen, por coherencia
            // con el gobierno del movimiento — un enemigo decide con lo que cree, no con lo
            // que es.
            if (contact.Personality is not null)
            {
                var range = (contact.Position - EstimatedCentre(contact, state.Tick, worldDrift, dt)).Length;
                if (range > contact.Personality.FiringRadius) continue;
            }

            // Uno a la vez: hasta que el suyo se resuelva —impacte, lo intercepten o salga
            // del arena— este enemigo no vuelve a disparar.
            if (torpedoes.Any(t => t.Side == TorpedoSide.Enemy
                                   && t.SourceContactId == contact.Id.Value)) continue;

            var (id, advanced) = state.TakeEntityId();
            state = advanced;

            var target = EstimatedSubPosition(contact, state.Tick, worldDrift, dt);
            torpedoes.Add(Torpedo.CreateEnemyToward(new TorpedoId(id), contact.Position,
                target, _config.EnemyTorpedoSpeed, contact.Id.Value));
            events.Add(new EnemyTorpedoFired(state.Tick, id, contact.Id.Value));

            contacts[i] = contact with
            {
                NextFireTick = state.Tick + _config.EnemyReloadTicks + Hesitation(contact),
            };
        }

        return state;
    }

    /// <summary>
    /// Cuánto tarda este enemigo en decidirse, sorteado uniforme dentro de su ventana.
    ///
    /// El argumento a favor no es el realismo: el escalonamiento del WaveDirector arregla la
    /// primera andanada, pero con recargas idénticas los enemigos <b>se vuelven a sincronizar
    /// solos</b> a las pocas vueltas y la pared reaparece. Esto es lo único que lo impide para
    /// siempre.
    /// </summary>
    private int Hesitation(Contact contact)
    {
        if (contact.Personality is null) return 0;

        var min = contact.Personality.HesitationMinTicks;
        var max = contact.Personality.HesitationMaxTicks;
        if (max <= min) return min;

        return min + (int)(_random.NextDouble() * (max - min + 1));
    }

    /// <summary>
    /// Fase 4b · Gobierno del movimiento. Los contactos con personalidad dejan de ser
    /// balísticos: cada tick se les recalcula la velocidad para llevarlos al radio que
    /// prefieren, con una componente tangencial que los hace rodear.
    ///
    /// <b>El centro es su estimación, no el origen.</b> Con eso la convergencia sobre el agua
    /// vacía que el jugador dejó atrás no necesita ninguna regla aparte: es el mismo cálculo
    /// orbitando un punto equivocado. Un enemigo se mueve siempre respecto de dónde cree que
    /// estás.
    ///
    /// Los contactos sin personalidad no se tocan: conservan la velocidad con la que nacieron.
    /// </summary>
    private void SteerContacts(
        List<Contact> contacts, List<Torpedo> torpedoes, int tick, Vec2 worldDrift, double dt)
    {
        var ceiling = _config.EnemyTorpedoSpeed / 2.0;

        for (var i = 0; i < contacts.Count; i++)
        {
            var contact = contacts[i];
            if (contact.Personality is null || !contact.Alive) continue;
            if (contact.Kind != ContactKind.Enemy) continue;

            var loaded = !torpedoes.Any(t => t.Side == TorpedoSide.Enemy
                                             && t.SourceContactId == contact.Id.Value);
            var preferred = loaded
                ? contact.Personality.RadiusLoaded
                : contact.Personality.RadiusEmpty;

            var centre = EstimatedCentre(contact, tick, worldDrift, dt);
            var offset = contact.Position - centre;
            var distance = offset.Length;

            // Encima del centro no hay dirección definida; se deja quieto un tick.
            if (distance < 1e-9) continue;

            var outward = offset * (1.0 / distance);
            var radial = outward * (preferred - distance);
            var tangent = new Vec2(-outward.Y, outward.X) * contact.Personality.OrbitRate;

            var desired = radial + tangent;
            if (desired.Length > ceiling) desired = desired.Normalized() * ceiling;

            // Se resta la deriva para que el movimiento NETO sea el pedido: la fase 4 se la
            // vuelve a sumar a todos los contactos.
            contacts[i] = contact with { Velocity = desired - worldDrift };
        }
    }

    /// <summary>
    /// La estimación limpia: dónde cree este enemigo que está el jugador, sin el error de
    /// puntería. La usa el gobierno del movimiento (tarea 8) y es la base de la otra.
    /// </summary>
    private Vec2 EstimatedCentre(Contact contact, int tick, Vec2 worldDrift, double dt)
    {
        if (contact.Personality is null) return Vec2.Zero;
        var age = Math.Max(0, tick - contact.LastFixTick);
        return worldDrift * (age * dt) * (1.0 - contact.Personality.AimLead);
    }

    /// <summary>
    /// Dónde cree este enemigo que está el jugador. El submarino está clavado en el origen y
    /// el mundo deriva alrededor, así que un contacto viejo envejece solo hacia atrás en el
    /// rumbo: el punto de agua donde el jugador estaba hace N ticks es hoy `deriva × N × dt`.
    ///
    /// Sin personalidad devuelve el origen exacto — el comportamiento de siempre.
    /// </summary>
    private Vec2 EstimatedSubPosition(Contact contact, int tick, Vec2 worldDrift, double dt)
    {
        if (contact.Personality is null) return Vec2.Zero;

        var stale = EstimatedCentre(contact, tick, worldDrift, dt);
        if (contact.Personality.AimJitter <= 0.0) return stale;

        // Ruido puro, sólo donde el carácter lo pide. Se consume del generador sembrado en
        // orden de id, como todo lo demás, para no romper el determinismo.
        var angle = _random.NextDouble() * Math.PI * 2.0;
        var radius = _random.NextDouble() * contact.Personality.AimJitter;
        return stale + new Vec2(Math.Cos(angle) * radius, Math.Sin(angle) * radius);
    }

    /// <summary>
    /// Torpedos en orden de id ascendente; para cada uno, contactos en orden de id
    /// ascendente, y detona con el primero que cumpla. Elimina toda ambigüedad de
    /// simultaneidad sin desempates aleatorios (TDD §3.3).
    /// </summary>
    private void ResolveDirectHits(
        List<Contact> contacts, List<Torpedo> torpedoes, List<GameEvent> events, int tick)
    {
        for (var t = torpedoes.Count - 1; t >= 0; t--)
        {
            var torpedo = torpedoes[t];
            if (torpedo.Position.Length < torpedo.ArmedFromRadius) continue;

            for (var c = 0; c < contacts.Count; c++)
            {
                var contact = contacts[c];
                if (!contact.Alive) continue;

                if (torpedo.Side == TorpedoSide.Enemy)
                {
                    // Los pecios siguen inmunes al fuego enemigo: la anomalía del §25 necesita
                    // que ese pecio sobreviva como referencia.
                    if (contact.Kind != ContactKind.Enemy) continue;
                    // Y nadie se mata a sí mismo: el torpedo nace encima de su lanzador.
                    if (contact.Id.Value == torpedo.SourceContactId) continue;
                }

                if (Vec2.Distance(torpedo.Position, contact.Position) >= _config.HitRadius) continue;

                contacts[c] = contact with { Alive = false };
                events.Add(new ContactDestroyed(tick, contact.Id.Value, torpedo.Position,
                    torpedo.Side == TorpedoSide.Enemy
                        ? DestructionCause.FriendlyFire
                        : DestructionCause.DirectHit,
                    torpedo.Id.Value));
                torpedoes.RemoveAt(t);
                break;
            }
        }
    }

    /// <summary>
    /// Torpedo propio contra torpedo enemigo: se destruyen los dos. Es el §24 del GDD.
    /// Un torpedo enemigo no daña a los contactos — ni fuego amigo entre enemigos ni
    /// choque con pecios (TDD §4.5).
    /// </summary>
    private void ResolveTorpedoCollisions(List<Torpedo> torpedoes, List<GameEvent> events, int tick)
    {
        // Se marcan los ids a eliminar y se retiran al final. Manipular índices mientras
        // se recorre en dos bucles anidados es donde más fácil se cuela un error.
        //
        // Lista y no HashSet a propósito: la regla de "ninguna colección de orden no
        // garantizado dentro de Core" se verifica con grep y debe poder seguir siendo
        // absoluta. Con un puñado de torpedos, Contains lineal no cuesta nada.
        var destroyed = new List<int>();

        foreach (var own in torpedoes.Where(t => t.Side == TorpedoSide.Own)
                                     .OrderBy(t => t.Id.Value))
        {
            if (destroyed.Contains(own.Id.Value)) continue;

            foreach (var enemy in torpedoes.Where(t => t.Side == TorpedoSide.Enemy)
                                           .OrderBy(t => t.Id.Value))
            {
                if (destroyed.Contains(enemy.Id.Value)) continue;
                if (Vec2.Distance(own.Position, enemy.Position) >= _config.HitRadius) continue;

                events.Add(new EnemyTorpedoDestroyed(tick, enemy.Id.Value, enemy.Position,
                    DestructionCause.TorpedoCollision));

                // Sin este evento la intercepción no produce ninguna confirmación para el
                // jugador: el torpedo propio desaparecería en silencio y sólo se anunciaría
                // la muerte del enemigo. El spec §4.6 exige confirmación de impacto propia
                // e independiente del barrido, así que la detonación se emite también aquí.
                events.Add(new TorpedoDetonated(tick, own.Position, HitSomething: true,
                    TorpedoId: own.Id.Value));

                destroyed.Add(own.Id.Value);
                destroyed.Add(enemy.Id.Value);
                break;
            }
        }

        // Enemigo contra enemigo. Va en un bucle aparte y no mezclado con el de arriba para
        // que el orden de resolución siga siendo el de ids ascendentes dentro de cada clase,
        // que es lo que hace reproducible el desempate.
        //
        // Comparte la lista `destroyed` con el bucle de arriba, y eso decide los cruces
        // triples: si en el mismo tick un torpedo enemigo iba a chocar con uno propio Y con
        // otro enemigo, **gana la intercepción del jugador**, porque su bucle corrió primero
        // y ya lo marcó. Es lo correcto además de lo determinista — el mérito es del jugador,
        // y la telemetría lo cuenta como intercepción y no como accidente enemigo.
        var enemies = torpedoes.Where(t => t.Side == TorpedoSide.Enemy)
                               .OrderBy(t => t.Id.Value).ToList();

        for (var a = 0; a < enemies.Count; a++)
        for (var b = a + 1; b < enemies.Count; b++)
        {
            var first = enemies[a];
            var second = enemies[b];
            if (destroyed.Contains(first.Id.Value) || destroyed.Contains(second.Id.Value)) continue;
            if (Vec2.Distance(first.Position, second.Position) >= _config.HitRadius) continue;

            events.Add(new EnemyTorpedoDestroyed(tick, first.Id.Value, first.Position,
                DestructionCause.TorpedoCollision));
            events.Add(new EnemyTorpedoDestroyed(tick, second.Id.Value, second.Position,
                DestructionCause.TorpedoCollision));

            destroyed.Add(first.Id.Value);
            destroyed.Add(second.Id.Value);
        }

        if (destroyed.Count > 0) torpedoes.RemoveAll(t => destroyed.Contains(t.Id.Value));
    }

    /// <summary>
    /// Una defensa destruye cualquier torpedo — propio o ajeno — y cualquier contacto
    /// enemigo que la toque, y <b>se consume al hacerlo</b>. Detona una vez, llevándose todo
    /// lo que tenga dentro del radio en ese instante, y desaparece.
    ///
    /// Que se gaste es lo que le da peso a la decisión: con `config.DefenseCapacity` puesto
    /// en uno, desplegarla es quedarse sin red hasta que explote, y elegir dónde ponerla es
    /// una apuesta sobre por dónde viene la amenaza.
    ///
    /// Los contactos inertes la atraviesan sin sufrir nada, y eso no es una excepción
    /// menor: la anomalía del §25 sólo es observable si el jugador puede dejar una defensa
    /// junto a un pecio y mirarlos separarse. Si la defensa se comiera el pecio, el
    /// experimento que revela la anomalía la borraría (ver spec §4.9 y DefenseAnomalyTests).
    /// Un pecio además ya está hundido: no hay nada que destruirle.
    /// </summary>
    private void ResolveDefenseCollisions(
        List<Contact> contacts, List<Torpedo> torpedoes, List<Defense> defenses,
        List<GameEvent> events, int tick)
    {
        for (var d = 0; d < defenses.Count; d++)
        {
            var defense = defenses[d];
            if (!defense.Alive) continue;

            var destroyed = 0;

            for (var c = 0; c < contacts.Count; c++)
            {
                var contact = contacts[c];
                if (!contact.Alive || contact.Kind != ContactKind.Enemy) continue;
                if (Vec2.Distance(defense.Position, contact.Position) >= _config.DefenseRadius) continue;

                contacts[c] = contact with { Alive = false };
                events.Add(new ContactDestroyed(tick, contact.Id.Value, contact.Position,
                    DestructionCause.Defense));
                destroyed++;
            }

            for (var t = torpedoes.Count - 1; t >= 0; t--)
            {
                var torpedo = torpedoes[t];
                if (Vec2.Distance(defense.Position, torpedo.Position) >= _config.DefenseRadius) continue;

                if (torpedo.Side == TorpedoSide.Enemy)
                    events.Add(new EnemyTorpedoDestroyed(tick, torpedo.Id.Value, torpedo.Position,
                        DestructionCause.Defense));

                torpedoes.RemoveAt(t);
                destroyed++;
            }

            if (destroyed == 0) continue;

            defenses[d] = defense with { Alive = false };
            events.Add(new DefenseDetonated(tick, defense.Id.Value, defense.Position, destroyed));
        }
    }

    /// <summary>
    /// Torpedo propio que agotó su corrida: detona con radio de explosión en el punto
    /// nominal de la banda. Puede destruir más de un contacto — limitarlo a uno exigiría
    /// una regla de desempate arbitraria (TDD §4.3).
    /// </summary>
    private void ResolveRunOuts(
        List<Contact> contacts, List<Torpedo> torpedoes, List<GameEvent> events, int tick)
    {
        for (var t = torpedoes.Count - 1; t >= 0; t--)
        {
            var torpedo = torpedoes[t];
            if (torpedo.Side != TorpedoSide.Own) continue;
            if (torpedo.Position.Length < torpedo.RunToRadius) continue;

            var point = torpedo.Bearing.ToUnitVector() * torpedo.RunToRadius;
            var killed = false;
            var nearestSurvivor = double.MaxValue;
            var nearestSurvivorId = -1;

            for (var c = 0; c < contacts.Count; c++)
            {
                var contact = contacts[c];
                if (!contact.Alive) continue;

                var distance = Vec2.Distance(point, contact.Position);
                if (distance < _config.BlastRadius)
                {
                    contacts[c] = contact with { Alive = false };
                    events.Add(new ContactDestroyed(tick, contact.Id.Value, contact.Position,
                        DestructionCause.Blast, torpedo.Id.Value));
                    killed = true;
                }
                else if (distance < nearestSurvivor)
                {
                    nearestSurvivor = distance;
                    nearestSurvivorId = contact.Id.Value;
                }
            }

            events.Add(new TorpedoDetonated(tick, point, killed, torpedo.Id.Value));

            // Casi-impacto: cerca como para ser intencional, lejos como para no destruir.
            // Base mecánica de la pista 2 (spec §4.10). Sin efecto en M1.
            if (!killed && nearestSurvivorId >= 0 && nearestSurvivor < _config.NearMissRadius)
                events.Add(new NearMiss(tick, nearestSurvivorId, nearestSurvivor));

            torpedoes.RemoveAt(t);
        }
    }

    /// <summary>
    /// Dos impactos y se acaba la corrida (§20 del GDD). Sin desactivación de sistemas en
    /// M1: los estados de daño del §21 van a un milestone posterior.
    ///
    /// El impacto sobre el propio submarino se confirma siempre, sin depender del barrido:
    /// es corolario de la regla de confirmación del spec §4.6.
    /// </summary>
    private PlayerSub ResolveSubHits(
        PlayerSub sub, List<Torpedo> torpedoes, List<GameEvent> events, int tick)
    {
        for (var t = torpedoes.Count - 1; t >= 0; t--)
        {
            var torpedo = torpedoes[t];
            if (torpedo.Side != TorpedoSide.Enemy) continue;
            if (torpedo.Position.Length >= _config.SubHitRadius) continue;

            torpedoes.RemoveAt(t);
            sub = sub with { HitsTaken = sub.HitsTaken + 1 };
            events.Add(new SubHit(tick, sub.HitsTaken));

            if (sub.HitsTaken >= 2 && !sub.Destroyed)
            {
                sub = sub with { Destroyed = true };
                events.Add(new SubDestroyed(tick, SubDeathCause.Torpedoes));
            }
        }

        return sub;
    }

    /// <summary>
    /// Un contacto enemigo que llegue encima del submarino lo destruye de una, sin importar
    /// los impactos previos. Es lo que convierte "no disparar" en una consecuencia.
    ///
    /// Los contactos inertes no chocan: un pecio ya está hundido, y morir contra terreno es
    /// una muerte barata. Misma lógica que su exención frente a las defensas (§4.4).
    /// </summary>
    private PlayerSub ResolveContactCollisions(
        PlayerSub sub, List<Contact> contacts, List<GameEvent> events, int tick)
    {
        if (sub.Destroyed) return sub;

        foreach (var contact in contacts)
        {
            if (!contact.Alive || contact.Kind != ContactKind.Enemy) continue;
            if (contact.Position.Length >= _config.SubHitRadius) continue;

            events.Add(new SubDestroyed(tick, SubDeathCause.Collision));
            return sub with { Destroyed = true };
        }

        return sub;
    }

    /// <summary>
    /// La recarga ocurre en tiempo real y no pausa nada (§23 del GDD). No se puede
    /// interrumpir ni cancelar en M1: interrupción y fallo dependen del sistema de daño,
    /// que va a un milestone posterior.
    ///
    /// `[DECIDED]` Carga <b>un torpedo por recarga</b>, no llena el cargador. Con tres tubos
    /// y siete segundos por tubo, quedarse en cero cuesta veintiún segundos de trabajo
    /// repetido en vez de una sola espera: disparar el tercero es una decisión, no un
    /// trámite. Es la misma regla que respetan los enemigos (ver FireEnemyTorpedoes).
    /// </summary>
    private PlayerSub AdvanceReload(PlayerSub sub, int tick, List<GameEvent> events)
    {
        if (!sub.ReloadInProgress || tick < sub.ReloadCompletesAtTick) return sub;

        var room = Math.Min(1, _config.MagazineSize - sub.AmmoLoaded);
        var moved = Math.Min(room, sub.AmmoReserve);

        var reloaded = sub with
        {
            AmmoLoaded = sub.AmmoLoaded + moved,
            AmmoReserve = sub.AmmoReserve - moved,
            ReloadInProgress = false,
            ReloadCompletesAtTick = 0,
        };

        events.Add(new ReloadCompleted(tick, reloaded.AmmoLoaded));
        return reloaded;
    }

    /// <summary>
    /// Mide el disparo contra la verdad. Vive en Core y no en la presentación porque
    /// necesita la posición real del contacto, que el snapshot esconde a propósito: subir
    /// ese dato rompería la defensa central del diseño.
    /// </summary>
    private void AssessShot(WorldState state, TargetCoordinate target, int torpedoId,
        List<GameEvent> events)
    {
        var firedBearing = (double)target.Bearing.Degrees;

        Contact? nearest = null;
        var bestSeparation = double.MaxValue;

        foreach (var contact in state.Contacts)
        {
            if (!contact.Alive) continue;
            var separation = Bearing.Separation(firedBearing, Bearing.DegreesOf(contact.Position));
            if (separation >= bestSeparation) continue;
            bestSeparation = separation;
            nearest = contact;
        }

        // Disparar al vacío no tiene contra qué medirse. Se omite en vez de inventar un
        // valor sentinela que después contamine los promedios.
        if (nearest is null) return;

        var age = -1;
        foreach (var reading in state.Sonar.Readings)
        {
            if (reading.ContactId != nearest.Id) continue;
            age = state.Tick - reading.ObservedAtTick;
            break;
        }

        events.Add(new ShotAssessed(state.Tick, torpedoId, nearest.Id.Value,
            bestSeparation,
            System.Math.Abs(_layout.CenterRadius(target.Band) - nearest.Position.Length),
            age));
    }

    private WorldState ApplyCommand(WorldState state, Command command, List<GameEvent> events)
    {
        switch (command)
        {
            case ToggleSonarCommand:
            {
                var running = !state.Sonar.SweepRunning;
                events.Add(new SonarToggled(state.Tick, running));
                return state with { Sonar = state.Sonar with { SweepRunning = running } };
            }

            case StatusCommand:
                events.Add(new StatusReported(state.Tick,
                    state.Sub.AmmoLoaded, state.Sub.AmmoReserve, state.Sub.DefensesAvailable));
                return state;

            case UnrecognizedInput unrecognized:
                events.Add(new InputNotRecognized(state.Tick, unrecognized.RawText));
                return state;

            case FireCommand fire:
            {
                if (state.Sub.ReloadInProgress)
                {
                    events.Add(new FireRejected(state.Tick, RejectionReason.Reloading));
                    return state;
                }
                if (state.Sub.AmmoLoaded <= 0)
                {
                    events.Add(new FireRejected(state.Tick, RejectionReason.NoAmmo));
                    return state;
                }

                var (id, advanced) = state.TakeEntityId();
                var torpedo = Torpedo.CreateOwn(
                    new TorpedoId(id), fire.Target, _layout, _config.TorpedoSpeed);

                events.Add(new TorpedoFired(state.Tick, id,
                    fire.Target.Bearing.Degrees, fire.Target.Band.Letter));

                AssessShot(state, fire.Target, id, events);

                return advanced
                    .WithTorpedoes(advanced.Torpedoes.Append(torpedo))
                    with { Sub = advanced.Sub with { AmmoLoaded = advanced.Sub.AmmoLoaded - 1 } };
            }

            case ReloadCommand:
            {
                if (state.Sub.ReloadInProgress || state.Sub.AmmoReserve <= 0) return state;
                events.Add(new ReloadStarted(state.Tick));
                return state with
                {
                    Sub = state.Sub with
                    {
                        ReloadInProgress = true,
                        ReloadCompletesAtTick = state.Tick + _config.ReloadTicks,
                    },
                };
            }

            case DeployDefenseCommand deploy:
            {
                // El límite es de concurrencia, no de inventario: puedes volver a poner una
                // apenas la anterior explote, pero nunca tener más de DefenseCapacity a la
                // vez. Con la capacidad en uno, desplegar es quedarse sin red hasta que se
                // gaste, y elegir dónde ponerla es apostar por dónde viene la amenaza.
                var deployed = state.Defenses.Count(d => d.Alive);
                if (deployed >= _config.DefenseCapacity)
                {
                    events.Add(new DefenseRejected(state.Tick, RejectionReason.NoneAvailable));
                    return state;
                }

                var (id, advanced) = state.TakeEntityId();
                var position = deploy.Target.ToPosition(_layout);
                events.Add(new DefenseDeployed(state.Tick, id, position));

                return advanced
                    .WithDefenses(advanced.Defenses.Append(Defense.Create(new DefenseId(id), position)))
                    with { Sub = advanced.Sub with
                    {
                        DefensesAvailable = _config.DefenseCapacity - deployed - 1,
                    } };
            }

            default:
                return state;
        }
    }

    /// <summary>
    /// Avanza el haz y registra lecturas. Solo los contactos están sujetos a gating:
    /// torpedos propios, defensas, torpedos enemigos y detonaciones entran al snapshot
    /// siempre (spec §4.1).
    ///
    /// Se guarda una sola lectura por contacto, la más reciente. Interpretar posiciones
    /// sucesivas es trabajo del jugador con su cuaderno, no del juego mostrando una traza:
    /// mostrarla convertiría la extrapolación en lectura pasiva y vaciaría la habilidad
    /// central que M1 está validando (TDD §5.2).
    /// </summary>
    private Sensing.SonarState AdvanceSweepAndRecord(
        Sensing.SonarState sonar, List<Contact> contacts, int tick, double dt,
        List<GameEvent> events)
    {
        var readings = sonar.Readings.ToList();

        // Retirar lecturas de contactos que ya no existen o murieron.
        readings.RemoveAll(r => !contacts.Any(c => c.Alive && c.Id == r.ContactId));

        if (!sonar.SweepRunning)
            return sonar with { Readings = readings.OrderBy(r => r.ContactId.Value).ToArray() };

        var previous = sonar.SweepDegrees;
        var current = (previous + 360.0 / _config.SweepPeriodSeconds * dt) % 360.0;

        foreach (var contact in contacts)
        {
            if (!contact.Alive) continue;
            if (contact.Position.Length > 1.0) continue;

            var bearing = Bearing.FromVector(contact.Position).Degrees;
            if (!Sensing.SweepCrossing.Crossed(previous, current, bearing)) continue;

            readings.RemoveAll(r => r.ContactId == contact.Id);
            readings.Add(new Sensing.Reading
            {
                ContactId = contact.Id,
                Position = contact.Position,
                ObservedAtTick = tick,
            });
        }

        // El haz cruzó el cero: se completó una vuelta.
        var revolution = sonar.Revolution + (current < previous ? 1 : 0);
        var ordered = readings.OrderBy(r => r.ContactId.Value).ToArray();

        if (revolution != sonar.Revolution)
            events.Add(new SweepCompleted(tick, revolution, ordered.Length));

        return sonar with
        {
            SweepDegrees = current,
            Revolution = revolution,
            Readings = ordered,
        };
    }

    /// <summary>
    /// Fase 10b · Qué delata al jugador. Cada enemigo guarda un solo dato —el tick en que lo
    /// escuchó por última vez— y esta fase es lo único que lo escribe.
    ///
    /// Corre después del barrido para que `SweepCompleted` de este tick ya esté en la lista.
    /// El disparo enemigo (fase 5) usa entonces los contactos del tick anterior: un tick de
    /// desfase, imperceptible, y a cambio el orden queda sin ambigüedad.
    /// </summary>
    private void RefreshEnemyFixes(List<Contact> contacts, List<GameEvent> events, int tick)
    {
        // Sólo ve lo que ya se emitió en las fases 1–10: esta fase corre en el punto 10b,
        // así que cualquier evento emitido en la fase 11 en adelante (por ejemplo
        // ReloadCompleted) queda invisible acá. Y como `events` se reconstruye entero en
        // cada tick, no queda tarde: queda perdido sin más. Si se agrega una fuente de
        // ruido nueva, hay que verificar en qué fase se emite, no sólo sumarla a la lista.
        var loud = events.Any(e => e is SweepCompleted
                                or TorpedoFired
                                or ReloadStarted
                                or DefenseDeployed);

        for (var i = 0; i < contacts.Count; i++)
        {
            var contact = contacts[i];
            if (contact.Kind != ContactKind.Enemy || !contact.Alive) continue;
            if (contact.Position.Length > 1.0) continue;

            // La cercanía oye sin que el jugador haga nada.
            var close = contact.Position.Length <= _config.PassiveHearingRadius;
            if (loud || close) contacts[i] = contact with { LastFixTick = tick };
        }
    }
}
