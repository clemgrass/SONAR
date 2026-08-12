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

        // 4 · Mover contactos — reciben la deriva. Las defensas NO: esa omisión
        //     deliberada es la anomalía del §25 del GDD. Ver TDD §4.4.
        for (var i = 0; i < contacts.Count; i++)
        {
            var c = contacts[i];
            if (!c.Alive) continue;
            contacts[i] = c with { Position = c.Position + (c.Velocity + worldDrift) * dt };
        }

        // 5 · Disparo enemigo
        state = FireEnemyTorpedoes(state, contacts, torpedoes, events);

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

        // 10 · Barrido
        var sonar = AdvanceSweep(state, dt);

        // 11 · Recarga
        sub = AdvanceReload(sub, state.Tick, events);

        // 12 · Retirar entidades muertas y fuera de rango
        contacts.RemoveAll(c => !c.Alive);
        torpedoes.RemoveAll(t => t.Position.Length > 1.0);
        defenses.RemoveAll(d => !d.Alive);

        // 13 · Reconstruir el estado inmutable y avanzar el contador
        state = state with { Sub = sub, Sonar = sonar, Tick = state.Tick + 1 };
        State = state.WithContacts(contacts).WithTorpedoes(torpedoes).WithDefenses(defenses);
        return events;
    }

    // --- Fases. Se llenan en las tareas siguientes de este plan. ---

    /// <summary>
    /// IA mínima de M1: cada enemigo dispara recto hacia el origen a intervalos fijos.
    /// No evade, no persigue y no elige blanco — el §63.11 completo va a un milestone
    /// posterior. Lo que M1 mide es al jugador bajo presión, no la inteligencia del enemigo.
    /// </summary>
    private WorldState FireEnemyTorpedoes(
        WorldState state, List<Contact> contacts, List<Torpedo> torpedoes, List<GameEvent> events)
    {
        for (var i = 0; i < contacts.Count; i++)
        {
            var contact = contacts[i];
            if (!contact.Alive) continue;
            if (contact.Kind != ContactKind.Enemy) continue;
            if (state.Tick < contact.NextFireTick) continue;
            if (contact.Position.Length > 1.0) continue;

            var (id, advanced) = state.TakeEntityId();
            state = advanced;

            torpedoes.Add(Torpedo.CreateEnemy(
                new TorpedoId(id), contact.Position, _config.EnemyTorpedoSpeed));
            events.Add(new EnemyTorpedoFired(state.Tick, id, contact.Id.Value));

            contacts[i] = contact with
            {
                NextFireTick = state.Tick + _config.EnemyFireIntervalTicks,
            };
        }

        return state;
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
            if (torpedo.Side != TorpedoSide.Own) continue;
            if (torpedo.Position.Length < torpedo.ArmedFromRadius) continue;

            for (var c = 0; c < contacts.Count; c++)
            {
                var contact = contacts[c];
                if (!contact.Alive) continue;
                if (Vec2.Distance(torpedo.Position, contact.Position) >= _config.HitRadius) continue;

                contacts[c] = contact with { Alive = false };
                events.Add(new ContactDestroyed(tick, contact.Id.Value,
                    torpedo.Position, DestructionCause.DirectHit));
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
                events.Add(new TorpedoDetonated(tick, own.Position, HitSomething: true));

                destroyed.Add(own.Id.Value);
                destroyed.Add(enemy.Id.Value);
                break;
            }
        }

        if (destroyed.Count > 0) torpedoes.RemoveAll(t => destroyed.Contains(t.Id.Value));
    }

    /// <summary>
    /// Una defensa destruye cualquier torpedo — propio o ajeno — y cualquier contacto
    /// enemigo que la toque. La defensa sobrevive. Es el §24 del GDD.
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
        foreach (var defense in defenses)
        {
            if (!defense.Alive) continue;

            for (var c = 0; c < contacts.Count; c++)
            {
                var contact = contacts[c];
                if (!contact.Alive || contact.Kind != ContactKind.Enemy) continue;
                if (Vec2.Distance(defense.Position, contact.Position) >= _config.DefenseRadius) continue;

                contacts[c] = contact with { Alive = false };
                events.Add(new ContactDestroyed(tick, contact.Id.Value, contact.Position,
                    DestructionCause.Defense));
            }

            for (var t = torpedoes.Count - 1; t >= 0; t--)
            {
                var torpedo = torpedoes[t];
                if (Vec2.Distance(defense.Position, torpedo.Position) >= _config.DefenseRadius) continue;

                if (torpedo.Side == TorpedoSide.Enemy)
                    events.Add(new EnemyTorpedoDestroyed(tick, torpedo.Id.Value, torpedo.Position,
                        DestructionCause.Defense));

                torpedoes.RemoveAt(t);
            }
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
                        DestructionCause.Blast));
                    killed = true;
                }
                else if (distance < nearestSurvivor)
                {
                    nearestSurvivor = distance;
                    nearestSurvivorId = contact.Id.Value;
                }
            }

            events.Add(new TorpedoDetonated(tick, point, killed));

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
                events.Add(new SubDestroyed(tick));
            }
        }

        return sub;
    }

    /// <summary>
    /// La recarga ocurre en tiempo real y no pausa nada (§23 del GDD). No se puede
    /// interrumpir ni cancelar en M1: interrupción y fallo dependen del sistema de daño,
    /// que va a un milestone posterior.
    /// </summary>
    private PlayerSub AdvanceReload(PlayerSub sub, int tick, List<GameEvent> events)
    {
        if (!sub.ReloadInProgress || tick < sub.ReloadCompletesAtTick) return sub;

        var room = _config.MagazineSize - sub.AmmoLoaded;
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
                if (state.Sub.DefensesAvailable <= 0)
                {
                    events.Add(new DefenseRejected(state.Tick, RejectionReason.NoneAvailable));
                    return state;
                }

                var (id, advanced) = state.TakeEntityId();
                var position = deploy.Target.ToPosition(_layout);
                events.Add(new DefenseDeployed(state.Tick, id, position));

                return advanced
                    .WithDefenses(advanced.Defenses.Append(Defense.Create(new DefenseId(id), position)))
                    with { Sub = advanced.Sub with { DefensesAvailable = advanced.Sub.DefensesAvailable - 1 } };
            }

            default:
                return state;
        }
    }

    private Sensing.SonarState AdvanceSweep(WorldState state, double dt)
    {
        if (!state.Sonar.SweepRunning) return state.Sonar;
        var degreesPerSecond = 360.0 / _config.SweepPeriodSeconds;
        var advanced = (state.Sonar.SweepDegrees + degreesPerSecond * dt) % 360.0;
        return state.Sonar with { SweepDegrees = advanced };
    }
}
