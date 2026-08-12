# M1 · Plan 3 — Combate y amenaza Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Completar la simulación de combate — disparo, impactos, detonación con radio, defensas, munición y recarga — y la amenaza enemiga que hace que el jugador pueda morir.

**Architecture:** `Simulation.Tick` se refactoriza para trabajar sobre listas mutables durante el tick y reconstruir el `WorldState` inmutable al final. Cada fase del orden de actualización del TDD §3.2 vive en un método privado propio, y este plan los va llenando de uno en uno.

**Tech Stack:** C# / .NET, xUnit.

**Prerrequisito:** Planes 1 y 2 completos. 120 tests pasando.

**Documentos de referencia:**
- TDD: `docs/tdd/M1-core-simulation.md` — §3.3 colisiones simultáneas, §4 combate completo
- Spec: `docs/superpowers/specs/2026-08-11-sonar-preproduction-design.md` — §4.3 modelo de torpedo, §4.6 confirmación

---

## Estructura de archivos

| Archivo | Cambio | Responsabilidad |
|---|---|---|
| `src/Sonar.Core/Events/GameEvent.cs` | Modificar | Sumar eventos de combate y amenaza |
| `src/Sonar.Core/Simulation.cs` | Modificar | Refactor a fases; una fase por tarea |
| `tests/Sonar.Core.Tests/Combat/*.cs` | Crear | Un archivo de tests por fase |

`Simulation.cs` crece en este plan. Si supera las ~400 líneas, extraer las fases de resolución a `src/Sonar.Core/Combat/CombatResolver.cs` manteniendo las firmas — el resto del plan sigue funcionando igual.

---

## Task 1: Eventos de combate y amenaza

**Files:**
- Modify: `src/Sonar.Core/Events/GameEvent.cs`
- Test: `tests/Sonar.Core.Tests/Combat/CombatEventTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Combat/CombatEventTests.cs`:

```csharp
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Core.Tests.Combat;

public class CombatEventTests
{
    [Fact]
    public void ContactDestroyed_CarriesCauseAndPosition()
    {
        var evt = new ContactDestroyed(10, 3, new Vec2(0.2, -0.4), DestructionCause.DirectHit);
        Assert.Equal(DestructionCause.DirectHit, evt.Cause);
        Assert.Equal(3, evt.ContactId);
        Assert.Equal(10, evt.Tick);
    }

    [Fact]
    public void FireRejected_CarriesReason()
    {
        Assert.Equal(RejectionReason.NoAmmo, new FireRejected(5, RejectionReason.NoAmmo).Reason);
    }

    [Fact]
    public void NearMiss_CarriesDistance()
    {
        var evt = new NearMiss(12, 7, 0.13);
        Assert.Equal(0.13, evt.Distance, precision: 10);
    }

    [Fact]
    public void SubDestroyed_IsDistinctFromSubHit()
    {
        GameEvent hit = new SubHit(20, 1);
        GameEvent dead = new SubDestroyed(30);
        Assert.IsType<SubHit>(hit);
        Assert.IsType<SubDestroyed>(dead);
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'ContactDestroyed' could not be found`.

- [ ] **Step 3: Ampliar `GameEvent.cs`**

Reemplaza el contenido completo de `src/Sonar.Core/Events/GameEvent.cs`:

```csharp
using Sonar.Core.Spatial;

namespace Sonar.Core.Events;

public abstract record GameEvent(int Tick);

// --- Sistemas ---
public sealed record SonarToggled(int Tick, bool Running) : GameEvent(Tick);
public sealed record StatusReported(int Tick, int AmmoLoaded, int AmmoReserve, int Defenses)
    : GameEvent(Tick);
public sealed record InputNotRecognized(int Tick, string RawText) : GameEvent(Tick);

// --- Combate propio ---
public sealed record TorpedoFired(int Tick, int TorpedoId, int BearingDegrees, char BandLetter)
    : GameEvent(Tick);
public sealed record TorpedoDetonated(int Tick, Vec2 Position, bool HitSomething) : GameEvent(Tick);
public sealed record ContactDestroyed(int Tick, int ContactId, Vec2 Position, DestructionCause Cause)
    : GameEvent(Tick);

/// <summary>
/// Detonación que no destruyó nada pero cayó cerca de un contacto vivo. No tiene efecto en M1:
/// se emite desde ahora para que el sistema de pistas lo consuma después sin tocar combate.
/// Es la base mecánica de la pista 2 (spec §4.10).
/// </summary>
public sealed record NearMiss(int Tick, int ContactId, double Distance) : GameEvent(Tick);

public sealed record FireRejected(int Tick, RejectionReason Reason) : GameEvent(Tick);

// --- Defensa ---
public sealed record DefenseDeployed(int Tick, int DefenseId, Vec2 Position) : GameEvent(Tick);
public sealed record DefenseRejected(int Tick, RejectionReason Reason) : GameEvent(Tick);

// --- Munición ---
public sealed record ReloadStarted(int Tick) : GameEvent(Tick);
public sealed record ReloadCompleted(int Tick, int AmmoLoaded) : GameEvent(Tick);

// --- Amenaza enemiga ---
public sealed record EnemyTorpedoFired(int Tick, int TorpedoId, int SourceContactId) : GameEvent(Tick);
public sealed record EnemyTorpedoDestroyed(int Tick, int TorpedoId, Vec2 Position,
    DestructionCause Cause) : GameEvent(Tick);
public sealed record SubHit(int Tick, int HitsTaken) : GameEvent(Tick);
public sealed record SubDestroyed(int Tick) : GameEvent(Tick);

public enum DestructionCause { DirectHit, Blast, Defense, TorpedoCollision }
public enum RejectionReason { NoAmmo, Reloading, NoneAvailable }
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 124`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/Events/GameEvent.cs tests/Sonar.Core.Tests/Combat/CombatEventTests.cs
git commit -m "feat(core): add combat and threat events"
```

---

## Task 2: Refactorizar Tick a fases

Sin cambios de comportamiento. Los 120 tests existentes deben seguir pasando exactamente igual.

**Files:**
- Modify: `src/Sonar.Core/Simulation.cs`

- [ ] **Step 1: Reemplazar el cuerpo de `Tick` por fases**

En `src/Sonar.Core/Simulation.cs`, reemplaza el método `Tick` completo y agrega las fases vacías justo después de él:

```csharp
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

    private WorldState FireEnemyTorpedoes(
        WorldState state, List<Contact> contacts, List<Torpedo> torpedoes, List<GameEvent> events)
        => state;

    private void ResolveDirectHits(
        List<Contact> contacts, List<Torpedo> torpedoes, List<GameEvent> events, int tick) { }

    private void ResolveTorpedoCollisions(
        List<Torpedo> torpedoes, List<GameEvent> events, int tick) { }

    private void ResolveDefenseCollisions(
        List<Contact> contacts, List<Torpedo> torpedoes, List<Defense> defenses,
        List<GameEvent> events, int tick) { }

    private void ResolveRunOuts(
        List<Contact> contacts, List<Torpedo> torpedoes, List<GameEvent> events, int tick) { }

    private PlayerSub ResolveSubHits(
        PlayerSub sub, List<Torpedo> torpedoes, List<GameEvent> events, int tick) => sub;

    private PlayerSub AdvanceReload(PlayerSub sub, int tick, List<GameEvent> events) => sub;
```

- [ ] **Step 2: Verificar que nada se rompió**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 124`

Si `DefenseAnomalyTests` falla, la deriva se aplicó a las defensas por error en el paso 4.

- [ ] **Step 3: Commit**

```bash
git add src/Sonar.Core/Simulation.cs
git commit -m "refactor(core): split tick into named phases"
```

---

## Task 3: Disparo y consumo de munición

**Files:**
- Modify: `src/Sonar.Core/Simulation.cs`
- Test: `tests/Sonar.Core.Tests/Combat/FiringTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Combat/FiringTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sonar.Core.Commands;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Combat;

public class FiringTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Command[] Fire(int bearing, int band) =>
        new Command[] { new FireCommand(new TargetCoordinate(new Bearing(bearing), new Band(band))) };

    [Fact]
    public void Fire_CreatesTorpedoAndSpendsOneRound()
    {
        var sim = TestWorld.Still();
        var events = sim.Tick(Fire(47, 2));

        Assert.Single(sim.State.Torpedoes);
        Assert.Equal(3, sim.State.Sub.AmmoLoaded);
        var fired = (TorpedoFired)Assert.Single(events, e => e is TorpedoFired);
        Assert.Equal(47, fired.BearingDegrees);
        Assert.Equal('C', fired.BandLetter);
    }

    [Fact]
    public void Fire_TorpedoStartsAtDeadZoneEdge()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.Tick(Fire(90, 2));

        // Tras un tick ya avanzó un poco; comprobamos que arrancó fuera de la zona muerta.
        Assert.True(sim.State.Torpedoes[0].Position.Length >= config.DeadZoneRadius);
    }

    [Fact]
    public void Fire_WithEmptyMagazine_IsRejected()
    {
        var sim = TestWorld.Still();
        for (var i = 0; i < 4; i++) sim.Tick(Fire(i * 10, 4));   // gasta el cargador
        Assert.Equal(0, sim.State.Sub.AmmoLoaded);

        var events = sim.Tick(Fire(200, 4));

        var rejected = (FireRejected)Assert.Single(events, e => e is FireRejected);
        Assert.Equal(RejectionReason.NoAmmo, rejected.Reason);
        Assert.DoesNotContain(events, e => e is TorpedoFired);
    }

    [Fact]
    public void Fire_WhileReloading_IsRejected()
    {
        var sim = TestWorld.Still();
        sim.Tick(new Command[] { new ReloadCommand() });

        var events = sim.Tick(Fire(47, 2));

        var rejected = (FireRejected)Assert.Single(events, e => e is FireRejected);
        Assert.Equal(RejectionReason.Reloading, rejected.Reason);
    }

    [Fact]
    public void Fire_AssignsUniqueIncrementingIds()
    {
        var sim = TestWorld.Still();
        sim.Tick(Fire(10, 4));
        sim.Tick(Fire(20, 4));

        var ids = sim.State.Torpedoes.Select(t => t.Id.Value).ToArray();
        Assert.Equal(2, ids.Distinct().Count());
        Assert.True(ids[1] > ids[0]);
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL. Los tests de disparo fallan porque `FireCommand` no hace nada todavía.

- [ ] **Step 3: Implementar el disparo**

En `src/Sonar.Core/Simulation.cs`, dentro de `ApplyCommand`, agrega este caso antes del `default`:

```csharp
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
```

Y agrega el caso de recarga, también antes del `default`:

```csharp
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
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 129`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/Simulation.cs tests/Sonar.Core.Tests/Combat/FiringTests.cs
git commit -m "feat(core): implement firing and ammo consumption"
```

---

## Task 4: Impacto directo

**Files:**
- Modify: `src/Sonar.Core/Simulation.cs`
- Test: `tests/Sonar.Core.Tests/Combat/DirectHitTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Combat/DirectHitTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Combat;

public class DirectHitTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Contact EnemyAt(int id, Vec2 position) => new()
    {
        Id = new ContactId(id),
        Kind = ContactKind.Enemy,
        Position = position,
        Velocity = Vec2.Zero,
        Alive = true,
        NextFireTick = int.MaxValue,
    };

    private static Command[] Fire(int bearing, int band) =>
        new Command[] { new FireCommand(new TargetCoordinate(new Bearing(bearing), new Band(band))) };

    [Fact]
    public void TorpedoOnBearing_DestroysContactOnTheWay()
    {
        var sim = TestWorld.Still();
        // Marcación 90 es este. Contacto en x = 0.40, dentro de la banda C.
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.40, 0.0)) });

        var events = new List<GameEvent>();
        events.AddRange(sim.Tick(Fire(90, 2)));
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        var destroyed = (ContactDestroyed)Assert.Single(events, e => e is ContactDestroyed);
        Assert.Equal(1, destroyed.ContactId);
        Assert.Equal(DestructionCause.DirectHit, destroyed.Cause);
        Assert.Empty(sim.State.Contacts);
    }

    [Fact]
    public void LongerBand_StillDestroysContactOnTheWay()
    {
        // El torpedo choca con lo primero que se le cruce, aunque su corrida sea más larga.
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.40, 0.0)) });

        var events = new List<GameEvent>();
        events.AddRange(sim.Tick(Fire(90, 4)));   // banda E, corrida larga
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.Contains(events, e => e is ContactDestroyed { Cause: DestructionCause.DirectHit });
    }

    [Fact]
    public void InertContact_StopsTheTorpedo()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            Contact.CreateInert(new ContactId(1), new Vec2(0.30, 0.0)),
            EnemyAt(2, new Vec2(0.60, 0.0)),
        });

        var events = new List<GameEvent>();
        events.AddRange(sim.Tick(Fire(90, 4)));
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        // El pecio muere; el enemigo detrás sobrevive.
        var destroyed = (ContactDestroyed)Assert.Single(events, e => e is ContactDestroyed);
        Assert.Equal(1, destroyed.ContactId);
        Assert.Single(sim.State.Contacts);
        Assert.Equal(2, sim.State.Contacts[0].Id.Value);
    }

    [Fact]
    public void TorpedoInsideDeadZone_CannotReachAContactThere()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        // Contacto dentro de la zona muerta: ningún torpedo nace antes de ese radio.
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.05, 0.0)) });

        var events = new List<GameEvent>();
        events.AddRange(sim.Tick(Fire(90, 0)));
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.DoesNotContain(events, e => e is ContactDestroyed);
        Assert.Single(sim.State.Contacts);
    }

    [Fact]
    public void SimultaneousCandidates_LowestContactIdWins()
    {
        var sim = TestWorld.Still();
        // Dos contactos prácticamente en el mismo punto: gana el de id menor (TDD §3.3).
        sim.SetContacts(new[]
        {
            EnemyAt(7, new Vec2(0.40, 0.0)),
            EnemyAt(3, new Vec2(0.40, 0.001)),
        });

        var events = new List<GameEvent>();
        events.AddRange(sim.Tick(Fire(90, 2)));
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        var destroyed = (ContactDestroyed)Assert.Single(events, e => e is ContactDestroyed);
        Assert.Equal(3, destroyed.ContactId);
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL. Los torpedos atraviesan los contactos sin detonar.

- [ ] **Step 3: Implementar `ResolveDirectHits`**

Reemplaza el método vacío en `src/Sonar.Core/Simulation.cs`:

```csharp
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
```

> Los torpedos se recorren hacia atrás porque el bucle los elimina de la lista.
> Los contactos se recorren hacia adelante: están ordenados por id y el primero que
> cumple es el que gana.

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 134`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/Simulation.cs tests/Sonar.Core.Tests/Combat/DirectHitTests.cs
git commit -m "feat(core): implement direct hit resolution"
```

---

## Task 5: Detonación de fin de corrida y casi-impacto

**Files:**
- Modify: `src/Sonar.Core/Simulation.cs`
- Test: `tests/Sonar.Core.Tests/Combat/RunOutTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Combat/RunOutTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Combat;

public class RunOutTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Contact EnemyAt(int id, Vec2 position) => new()
    {
        Id = new ContactId(id),
        Kind = ContactKind.Enemy,
        Position = position,
        Velocity = Vec2.Zero,
        Alive = true,
        NextFireTick = int.MaxValue,
    };

    private static Command[] Fire(int bearing, int band) =>
        new Command[] { new FireCommand(new TargetCoordinate(new Bearing(bearing), new Band(band))) };

    private static List<GameEvent> RunFor(Simulation sim, Command[] first, int ticks)
    {
        var events = new List<GameEvent>(sim.Tick(first));
        for (var i = 0; i < ticks; i++) events.AddRange(sim.Tick(NoCommands));
        return events;
    }

    [Fact]
    public void BandTooShort_DetonatesInEmptyWaterAndContactSurvives()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.80, 0.0)) });

        // Banda A: corrida a 0.19. El contacto está en 0.80.
        var events = RunFor(sim, Fire(90, 0), 120);

        var detonated = (TorpedoDetonated)Assert.Single(events, e => e is TorpedoDetonated);
        Assert.False(detonated.HitSomething);
        Assert.Single(sim.State.Contacts);
    }

    [Fact]
    public void ContactSlightlyOffBearing_IsKilledByTheBlast()
    {
        var sim = TestWorld.Still();
        // Centro de la banda C está a 0.55 en marcación 90 -> (0.55, 0).
        // Un contacto a 0.06 de ahí cae dentro del radio de explosión (0.10).
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.55, 0.06)) });

        var events = RunFor(sim, Fire(90, 2), 120);

        var destroyed = (ContactDestroyed)Assert.Single(events, e => e is ContactDestroyed);
        Assert.Equal(DestructionCause.Blast, destroyed.Cause);
        var detonated = (TorpedoDetonated)Assert.Single(events, e => e is TorpedoDetonated);
        Assert.True(detonated.HitSomething);
    }

    [Fact]
    public void TwoContactsInBlastRadius_BothDie()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            EnemyAt(1, new Vec2(0.55, 0.05)),
            EnemyAt(2, new Vec2(0.55, -0.05)),
        });

        var events = RunFor(sim, Fire(90, 2), 120);

        Assert.Equal(2, events.Count(e => e is ContactDestroyed));
        Assert.Empty(sim.State.Contacts);
    }

    [Fact]
    public void ContactJustOutsideBlast_SurvivesAndEmitsNearMiss()
    {
        var sim = TestWorld.Still();
        // A 0.13 del punto de detonación: fuera del radio de explosión (0.10),
        // dentro de la ventana de casi-impacto (0.16).
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.55, 0.13)) });

        var events = RunFor(sim, Fire(90, 2), 120);

        Assert.DoesNotContain(events, e => e is ContactDestroyed);
        var nearMiss = (NearMiss)Assert.Single(events, e => e is NearMiss);
        Assert.Equal(1, nearMiss.ContactId);
        Assert.InRange(nearMiss.Distance, 0.10, 0.16);
    }

    [Fact]
    public void ContactFarAway_EmitsNoNearMiss()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.55, 0.40)) });

        var events = RunFor(sim, Fire(90, 2), 120);

        Assert.DoesNotContain(events, e => e is NearMiss);
    }

    [Fact]
    public void TorpedoIsGoneAfterDetonating()
    {
        var sim = TestWorld.Still();
        RunFor(sim, Fire(90, 2), 120);
        Assert.Empty(sim.State.Torpedoes);
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL. Los torpedos siguen viajando más allá de su corrida sin detonar.

- [ ] **Step 3: Implementar `ResolveRunOuts`**

Reemplaza el método vacío en `src/Sonar.Core/Simulation.cs`:

```csharp
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
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 140`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/Simulation.cs tests/Sonar.Core.Tests/Combat/RunOutTests.cs
git commit -m "feat(core): implement run-out detonation and near miss"
```

---

## Task 6: Defensas

**Files:**
- Modify: `src/Sonar.Core/Simulation.cs`
- Test: `tests/Sonar.Core.Tests/Combat/DefenseTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Combat/DefenseTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Combat;

public class DefenseTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Command[] Deploy(int bearing, int band) =>
        new Command[] { new DeployDefenseCommand(new TargetCoordinate(new Bearing(bearing), new Band(band))) };

    [Fact]
    public void Deploy_PlacesDefenseAndSpendsOne()
    {
        var sim = TestWorld.Still();
        var events = sim.Tick(Deploy(90, 2));

        Assert.Single(sim.State.Defenses);
        Assert.Equal(2, sim.State.Sub.DefensesAvailable);
        Assert.Contains(events, e => e is DefenseDeployed);
    }

    [Fact]
    public void Deploy_AtBandCenter()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.Tick(Deploy(90, 2));

        Assert.Equal(0.55, sim.State.Defenses[0].Position.X, precision: 10);
        Assert.Equal(0.00, sim.State.Defenses[0].Position.Y, precision: 10);
    }

    [Fact]
    public void Deploy_BeyondCapacity_IsRejected()
    {
        var sim = TestWorld.Still();
        for (var i = 0; i < 3; i++) sim.Tick(Deploy(i * 30, 2));

        var events = sim.Tick(Deploy(200, 2));

        var rejected = (DefenseRejected)Assert.Single(events, e => e is DefenseRejected);
        Assert.Equal(RejectionReason.NoneAvailable, rejected.Reason);
        Assert.Equal(3, sim.State.Defenses.Count);
    }

    [Fact]
    public void Defense_DestroysAContactThatDriftsIntoIt()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            new Contact
            {
                Id = new ContactId(1), Kind = ContactKind.Enemy,
                Position = new Vec2(0.90, 0.0), Velocity = new Vec2(-0.5, 0.0),
                Alive = true, NextFireTick = int.MaxValue,
            },
        });

        var events = new List<GameEvent>(sim.Tick(Deploy(90, 2)));
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        var destroyed = (ContactDestroyed)Assert.Single(events, e => e is ContactDestroyed);
        Assert.Equal(DestructionCause.Defense, destroyed.Cause);
        // La defensa sobrevive al choque.
        Assert.Single(sim.State.Defenses);
    }

    [Fact]
    public void Defense_DestroysOwnTorpedoesToo()
    {
        // El §24 del GDD dice que la defensa interactúa con proyectiles propios.
        var sim = TestWorld.Still();
        sim.Tick(Deploy(90, 1));   // defensa a 0.37 en marcación 90

        var events = new List<GameEvent>(
            sim.Tick(new Command[] { new FireCommand(new TargetCoordinate(new Bearing(90), new Band(4))) }));
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        // El torpedo muere en la defensa, no llega a la banda E.
        Assert.Empty(sim.State.Torpedoes);
        Assert.DoesNotContain(events, e => e is TorpedoDetonated { HitSomething: true });
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL. `DeployDefenseCommand` todavía no hace nada.

- [ ] **Step 3: Implementar despliegue y colisiones de defensa**

En `ApplyCommand`, agrega antes del `default`:

```csharp
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
```

Y reemplaza el método vacío `ResolveDefenseCollisions`:

```csharp
    /// <summary>
    /// Una defensa destruye cualquier contacto o torpedo — propio o ajeno — que la toque.
    /// La defensa sobrevive. Es el §24 del GDD.
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
                if (!contact.Alive) continue;
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
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 145`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/Simulation.cs tests/Sonar.Core.Tests/Combat/DefenseTests.cs
git commit -m "feat(core): implement defense deployment and collisions"
```

---

## Task 7: Recarga

**Files:**
- Modify: `src/Sonar.Core/Simulation.cs`
- Test: `tests/Sonar.Core.Tests/Combat/ReloadTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Combat/ReloadTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Sonar.Core.Commands;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Combat;

public class ReloadTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Command[] Fire(int bearing, int band) =>
        new Command[] { new FireCommand(new TargetCoordinate(new Bearing(bearing), new Band(band))) };

    [Fact]
    public void Reload_StartsAndCompletesAfterConfiguredTicks()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        for (var i = 0; i < 4; i++) sim.Tick(Fire(i * 20, 4));
        Assert.Equal(0, sim.State.Sub.AmmoLoaded);

        var events = new List<GameEvent>(sim.Tick(new Command[] { new ReloadCommand() }));
        Assert.Contains(events, e => e is ReloadStarted);

        for (var i = 0; i < config.ReloadTicks; i++) events.AddRange(sim.Tick(NoCommands));

        var completed = (ReloadCompleted)Assert.Single(events, e => e is ReloadCompleted);
        Assert.Equal(4, completed.AmmoLoaded);
        Assert.Equal(4, sim.State.Sub.AmmoLoaded);
        Assert.Equal(8, sim.State.Sub.AmmoReserve);
        Assert.False(sim.State.Sub.ReloadInProgress);
    }

    [Fact]
    public void Reload_DoesNotPauseTheWorld()
    {
        // El §23 del GDD: la recarga ocurre en tiempo real. El barrido sigue girando.
        var sim = TestWorld.Still();
        sim.Tick(new Command[] { new ReloadCommand() });
        var before = sim.State.Sonar.SweepDegrees;

        for (var i = 0; i < 30; i++) sim.Tick(NoCommands);

        Assert.NotEqual(before, sim.State.Sonar.SweepDegrees);
    }

    [Fact]
    public void Reload_WithEmptyReserve_DoesNothing()
    {
        var config = TestWorld.Config() with { AmmoReserve = 0 };
        var sim = TestWorld.Still(config);

        var events = sim.Tick(new Command[] { new ReloadCommand() });

        Assert.DoesNotContain(events, e => e is ReloadStarted);
        Assert.False(sim.State.Sub.ReloadInProgress);
    }

    [Fact]
    public void Reload_TopsUpPartialMagazine()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.Tick(Fire(10, 4));   // quedan 3 de 4

        var events = new List<GameEvent>(sim.Tick(new Command[] { new ReloadCommand() }));
        for (var i = 0; i < config.ReloadTicks; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.Equal(4, sim.State.Sub.AmmoLoaded);
        Assert.Equal(11, sim.State.Sub.AmmoReserve);   // solo consumió uno
    }

    [Fact]
    public void Reload_CannotBeStartedTwice()
    {
        var sim = TestWorld.Still();
        sim.Tick(new Command[] { new ReloadCommand() });
        var events = sim.Tick(new Command[] { new ReloadCommand() });

        Assert.DoesNotContain(events, e => e is ReloadStarted);
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL. La recarga arranca pero nunca completa.

- [ ] **Step 3: Implementar `AdvanceReload`**

Reemplaza el método vacío en `src/Sonar.Core/Simulation.cs`:

```csharp
    /// <summary>
    /// La recarga ocurre en tiempo real y no pausa nada (§23 del GDD). No se puede
    /// interrumpir ni cancelar en M1: interrupción y fallo dependen del sistema de daño,
    /// que va a un milestone posterior.
    /// </summary>
    private PlayerSub AdvanceReload(PlayerSub sub, int tick, List<GameEvent> events)
    {
        if (!sub.ReloadInProgress || tick < sub.ReloadCompletesAtTick) return sub;

        var room = _config.MagazineSize - sub.AmmoLoaded;
        var moved = System.Math.Min(room, sub.AmmoReserve);

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
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 150`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/Simulation.cs tests/Sonar.Core.Tests/Combat/ReloadTests.cs
git commit -m "feat(core): implement real-time reload"
```

---

## Task 8: Disparo enemigo

**Files:**
- Modify: `src/Sonar.Core/Simulation.cs`
- Test: `tests/Sonar.Core.Tests/Combat/EnemyFireTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Combat/EnemyFireTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Combat;

public class EnemyFireTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Contact EnemyReadyToFire(int id, Vec2 position, int nextFireTick) => new()
    {
        Id = new ContactId(id),
        Kind = ContactKind.Enemy,
        Position = position,
        Velocity = Vec2.Zero,
        Alive = true,
        NextFireTick = nextFireTick,
    };

    [Fact]
    public void Enemy_FiresWhenItsTickArrives()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { EnemyReadyToFire(1, new Vec2(0.0, -0.80), nextFireTick: 3) });

        var events = new List<GameEvent>();
        for (var i = 0; i < 5; i++) events.AddRange(sim.Tick(NoCommands));

        var fired = (EnemyTorpedoFired)Assert.Single(events, e => e is EnemyTorpedoFired);
        Assert.Equal(1, fired.SourceContactId);
        Assert.Single(sim.State.Torpedoes, t => t.Side == TorpedoSide.Enemy);
    }

    [Fact]
    public void EnemyTorpedo_TravelsTowardTheOrigin()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { EnemyReadyToFire(1, new Vec2(0.0, -0.80), nextFireTick: 0) });

        for (var i = 0; i < 30; i++) sim.Tick(NoCommands);

        var torpedo = sim.State.Torpedoes.Single(t => t.Side == TorpedoSide.Enemy);
        // Nació en y = -0.80 y viene bajando hacia el origen.
        Assert.True(torpedo.Position.Y > -0.80);
        Assert.True(torpedo.Position.Length < 0.80);
    }

    [Fact]
    public void Enemy_RearmsAfterTheConfiguredInterval()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { EnemyReadyToFire(1, new Vec2(0.0, -0.95), nextFireTick: 0) });

        var events = new List<GameEvent>();
        for (var i = 0; i < config.EnemyFireIntervalTicks + 5; i++)
            events.AddRange(sim.Tick(NoCommands));

        Assert.Equal(2, events.Count(e => e is EnemyTorpedoFired));
    }

    [Fact]
    public void InertContact_NeverFires()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { Contact.CreateInert(new ContactId(1), new Vec2(0.0, -0.80)) });

        var events = new List<GameEvent>();
        for (var i = 0; i < 600; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.DoesNotContain(events, e => e is EnemyTorpedoFired);
    }

    [Fact]
    public void DeadContact_StopsFiring()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { EnemyReadyToFire(1, new Vec2(0.90, 0.0), nextFireTick: 0) });
        // Lo destruye una defensa colocada encima.
        sim.Tick(new Command[]
        {
            new DeployDefenseCommand(new TargetCoordinate(new Bearing(90), new Band(4))),
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 600; i++) events.AddRange(sim.Tick(NoCommands));

        // A lo sumo disparó en el tick inicial; después de morir, nada más.
        Assert.True(events.Count(e => e is EnemyTorpedoFired) <= 1);
        Assert.Empty(sim.State.Contacts);
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL. Los enemigos no disparan.

- [ ] **Step 3: Implementar `FireEnemyTorpedoes`**

Reemplaza el método vacío en `src/Sonar.Core/Simulation.cs`:

```csharp
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
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 155`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/Simulation.cs tests/Sonar.Core.Tests/Combat/EnemyFireTests.cs
git commit -m "feat(core): implement minimal enemy firing AI"
```

---

## Task 9: Colisiones de torpedos y supervivencia del jugador

**Files:**
- Modify: `src/Sonar.Core/Simulation.cs`
- Test: `tests/Sonar.Core.Tests/Combat/SurvivalTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Combat/SurvivalTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Combat;

public class SurvivalTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Contact EnemyReadyToFire(int id, Vec2 position, int nextFireTick) => new()
    {
        Id = new ContactId(id),
        Kind = ContactKind.Enemy,
        Position = position,
        Velocity = Vec2.Zero,
        Alive = true,
        NextFireTick = nextFireTick,
    };

    [Fact]
    public void FirstEnemyTorpedo_HitsButDoesNotKill()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { EnemyReadyToFire(1, new Vec2(0.0, -0.50), nextFireTick: 0) });

        var events = new List<GameEvent>();
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        var hit = (SubHit)Assert.Single(events, e => e is SubHit);
        Assert.Equal(1, hit.HitsTaken);
        Assert.False(sim.State.Sub.Destroyed);
        Assert.DoesNotContain(events, e => e is SubDestroyed);
    }

    [Fact]
    public void SecondHit_DestroysTheSub()
    {
        var config = TestWorld.Config() with { EnemyFireIntervalTicks = 30 };
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { EnemyReadyToFire(1, new Vec2(0.0, -0.30), nextFireTick: 0) });

        var events = new List<GameEvent>();
        for (var i = 0; i < 400; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.Equal(2, events.Count(e => e is SubHit));
        Assert.Single(events, e => e is SubDestroyed);
        Assert.True(sim.State.Sub.Destroyed);
    }

    [Fact]
    public void AfterDestruction_TickDoesNothing()
    {
        var config = TestWorld.Config() with { EnemyFireIntervalTicks = 30 };
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { EnemyReadyToFire(1, new Vec2(0.0, -0.30), nextFireTick: 0) });

        for (var i = 0; i < 400; i++) sim.Tick(NoCommands);
        Assert.True(sim.State.Sub.Destroyed);

        var frozenTick = sim.State.Tick;
        var events = sim.Tick(NoCommands);

        Assert.Empty(events);
        Assert.Equal(frozenTick, sim.State.Tick);
    }

    [Fact]
    public void OwnAndEnemyTorpedo_DestroyEachOther()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { EnemyReadyToFire(1, new Vec2(0.0, -0.95), nextFireTick: 0) });

        var events = new List<GameEvent>();
        // Dispara al norte, de frente al torpedo entrante.
        events.AddRange(sim.Tick(new Command[]
        {
            new FireCommand(new TargetCoordinate(new Bearing(0), new Band(4))),
        }));
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.Contains(events, e => e is EnemyTorpedoDestroyed
        {
            Cause: DestructionCause.TorpedoCollision,
        });
        Assert.DoesNotContain(events, e => e is SubHit);
    }

    [Fact]
    public void EnemyTorpedo_PassesThroughInertContacts()
    {
        // No hay fuego amigo entre enemigos y los torpedos enemigos ignoran los pecios.
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            EnemyReadyToFire(1, new Vec2(0.0, -0.90), nextFireTick: 0),
            Contact.CreateInert(new ContactId(2), new Vec2(0.0, -0.50)),
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 200; i++) events.AddRange(sim.Tick(NoCommands));

        // El pecio sigue vivo y el submarino recibió el impacto.
        Assert.Contains(sim.State.Contacts, c => c.Id.Value == 2);
        Assert.Contains(events, e => e is SubHit);
    }

    [Fact]
    public void EnemyTorpedo_PassesThroughOtherEnemies()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            EnemyReadyToFire(1, new Vec2(0.0, -0.90), nextFireTick: 0),
            EnemyReadyToFire(2, new Vec2(0.0, -0.50), nextFireTick: int.MaxValue),
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 200; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.Contains(sim.State.Contacts, c => c.Id.Value == 2);
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL. Los torpedos enemigos atraviesan el submarino sin impactarlo.

- [ ] **Step 3: Implementar colisiones de torpedos y `ResolveSubHits`**

Reemplaza los dos métodos vacíos en `src/Sonar.Core/Simulation.cs`:

```csharp
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

                destroyed.Add(own.Id.Value);
                destroyed.Add(enemy.Id.Value);
                break;
            }
        }

        if (destroyed.Count > 0) torpedoes.RemoveAll(t => destroyed.Contains(t.Id.Value));
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
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 161`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/Simulation.cs tests/Sonar.Core.Tests/Combat/SurvivalTests.cs
git commit -m "feat(core): implement torpedo collisions and player survival"
```

---

## Task 10: Escalonado de disparo y determinismo con combate

**Files:**
- Create: `src/Sonar.Core/ContactSpawner.cs`
- Modify: `tests/Sonar.Core.Tests/Support/ScenarioRunner.cs`
- Test: `tests/Sonar.Core.Tests/Combat/StaggerTests.cs`
- Test: `tests/Sonar.Core.Tests/DeterminismTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Combat/StaggerTests.cs`:

```csharp
using System.Linq;
using Sonar.Core;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Combat;

public class StaggerTests
{
    [Fact]
    public void SpawnedEnemies_GetStaggeredFireTicks()
    {
        // Sin escalonar, todos disparan en el mismo tick y la presión llega en oleadas
        // sincronizadas en vez de sostenida (TDD §4.6).
        var config = TestWorld.Config();
        var spawner = new ContactSpawner(config);

        var contacts = spawner.SpawnEnemies(
            spawnTick: 0,
            firstId: 1,
            positions: new[]
            {
                new Vec2(0.0, -0.8), new Vec2(0.8, 0.0), new Vec2(0.0, 0.8), new Vec2(-0.8, 0.0),
            },
            velocities: new[] { Vec2.Zero, Vec2.Zero, Vec2.Zero, Vec2.Zero }).ToList();

        var fireTicks = contacts.Select(c => c.NextFireTick).ToList();
        Assert.Equal(fireTicks.Distinct().Count(), fireTicks.Count);

        for (var i = 1; i < fireTicks.Count; i++)
            Assert.Equal(config.EnemyFireStaggerTicks, fireTicks[i] - fireTicks[i - 1]);
    }

    [Fact]
    public void SpawnedEnemies_GetSequentialIds()
    {
        var spawner = new ContactSpawner(TestWorld.Config());
        var contacts = spawner.SpawnEnemies(0, firstId: 10,
            positions: new[] { new Vec2(0.0, -0.8), new Vec2(0.8, 0.0) },
            velocities: new[] { Vec2.Zero, Vec2.Zero }).ToList();

        Assert.Equal(new[] { 10, 11 }, contacts.Select(c => c.Id.Value).ToArray());
    }

    [Fact]
    public void Spawner_IsDeterministic()
    {
        var spawner = new ContactSpawner(TestWorld.Config());
        var positions = new[] { new Vec2(0.0, -0.8), new Vec2(0.8, 0.0) };
        var velocities = new[] { new Vec2(0.0, 0.1), new Vec2(-0.1, 0.0) };

        var first = spawner.SpawnEnemies(5, 1, positions, velocities).ToList();
        var second = spawner.SpawnEnemies(5, 1, positions, velocities).ToList();

        Assert.Equal(first, second);
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'ContactSpawner' could not be found`.

- [ ] **Step 3: Implementar `ContactSpawner`**

Crea `src/Sonar.Core/ContactSpawner.cs`:

```csharp
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
```

- [ ] **Step 4: Ampliar los tests de determinismo con combate**

Agrega estos tests al final de la clase `DeterminismTests` en
`tests/Sonar.Core.Tests/DeterminismTests.cs`, y añade los `using` necesarios
(`Sonar.Core.Commands`, `Sonar.Core.Entities`, `Sonar.Core.Spatial`, `System.Collections.Generic`, `System.Linq`):

```csharp
    private static IReadOnlyDictionary<int, IReadOnlyList<Command>> CombatScript() =>
        new Dictionary<int, IReadOnlyList<Command>>
        {
            [5] = new Command[] { new FireCommand(new TargetCoordinate(new Bearing(0), new Band(3))) },
            [30] = new Command[] { new DeployDefenseCommand(new TargetCoordinate(new Bearing(90), new Band(2))) },
            [60] = new Command[] { new FireCommand(new TargetCoordinate(new Bearing(180), new Band(2))) },
            [120] = new Command[] { new ReloadCommand() },
            [300] = new Command[] { new FireCommand(new TargetCoordinate(new Bearing(270), new Band(4))) },
        };

    private static IEnumerable<Contact> CombatContacts()
    {
        var spawner = new ContactSpawner(TestWorld.Config());
        return spawner.SpawnEnemies(
            spawnTick: 0, firstId: 200,
            positions: new[] { new Vec2(0.0, -0.75), new Vec2(0.70, 0.20), new Vec2(-0.60, 0.40) },
            velocities: new[] { new Vec2(0.02, 0.05), new Vec2(-0.06, 0.01), new Vec2(0.04, -0.03) });
    }

    [Fact]
    public void CombatScenario_IsReproducibleTickByTick()
    {
        var a = new ScenarioRunner(TestWorld.Config(), 4242, CombatScript(), CombatContacts());
        var b = new ScenarioRunner(TestWorld.Config(), 4242, CombatScript(), CombatContacts());

        for (var tick = 0; tick < 1200; tick++)
        {
            a.Run(1);
            b.Run(1);
            Assert.Equal(StateHash.Compute(a.State), StateHash.Compute(b.State));
        }
    }

    [Fact]
    public void CombatScenario_ProducesIdenticalEventStreams()
    {
        var a = new ScenarioRunner(TestWorld.Config(), 4242, CombatScript(), CombatContacts());
        var b = new ScenarioRunner(TestWorld.Config(), 4242, CombatScript(), CombatContacts());

        a.Run(1200);
        b.Run(1200);

        Assert.Equal(a.Events.Count, b.Events.Count);
        for (var i = 0; i < a.Events.Count; i++) Assert.Equal(a.Events[i], b.Events[i]);
    }

    [Fact]
    public void CombatScenario_ActuallyProducesCombat()
    {
        // Un test de determinismo que corre sobre un escenario vacío pasa trivialmente.
        // Este verifica que el escenario de referencia ejercita el sistema.
        var runner = new ScenarioRunner(TestWorld.Config(), 4242, CombatScript(), CombatContacts());
        runner.Run(1200);

        Assert.Contains(runner.Events, e => e is Events.TorpedoFired);
        Assert.Contains(runner.Events, e => e is Events.EnemyTorpedoFired);
        Assert.Contains(runner.Events, e => e is Events.TorpedoDetonated);
    }
```

- [ ] **Step 5: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 167`

- [ ] **Step 6: Commit**

```bash
git add src/Sonar.Core/ContactSpawner.cs tests/Sonar.Core.Tests/Combat/StaggerTests.cs tests/Sonar.Core.Tests/DeterminismTests.cs
git commit -m "feat(core): add staggered contact spawner and combat determinism tests"
```

---

## Definition of Done — Plan 3

- [ ] `dotnet build` termina sin advertencias ni errores
- [ ] `dotnet test` reporta 167 tests pasando, 0 fallando
- [ ] `DefenseAnomalyTests` sigue pasando — el refactor a fases no rompió la anomalía
- [ ] `CombatScenario_ActuallyProducesCombat` pasa: el escenario de determinismo no está vacío
- [ ] El jugador puede morir, y tras morir la simulación deja de avanzar
- [ ] Diez commits, uno por tarea

## Nota sobre el azar

Al terminar este plan, la simulación **sigue sin consumir `IRandomSource`**: el escalonado de
disparo es determinista por construcción y no hay nada aleatorio en el combate, que es
exactamente lo que decidió el spec §4.3.

Eso significa que el test *"semillas distintas producen resultados distintos"* del TDD §10.1
**no se puede escribir todavía**, y no debe forzarse inventando aleatoriedad para satisfacerlo.
Se escribirá cuando exista la primera fuente que realmente la use — probablemente la generación
de encuentros del guion de misión, en un milestone posterior.

## Qué sigue

**Plan 4 — Sonar, Controller y Godot:** registro de lecturas con gating por clase, `Sonar.Controller`
con parser, snapshot inmutable, acumulador de paso fijo con tope, y la escena fea de Godot que
permite jugarlo con las manos.
