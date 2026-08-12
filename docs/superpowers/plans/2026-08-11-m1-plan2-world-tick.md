# M1 · Plan 2 — Entidades, estado y bucle de tick Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Construir el modelo de dominio, el estado del mundo y el bucle de tick determinista, de modo que el mundo se mueva correctamente alrededor de un submarino fijo — incluida la omisión deliberada que produce la anomalía de la defensa.

**Architecture:** `WorldState` es un `record` inmutable. `Simulation.Tick()` recibe los comandos del tick y devuelve un estado nuevo más la lista de eventos. Todas las colecciones son listas ordenadas por id: nunca diccionarios, porque su orden de enumeración no está garantizado y eso rompería el determinismo del que depende todo el diseño.

**Tech Stack:** C# / .NET, xUnit.

**Prerrequisito:** Plan 1 completo. Existen `Vec2`, `Bearing`, `Band`, `BandLayout`, `TargetCoordinate`, `SimulationConfig`, `IRandomSource`, `SeededRandomSource`.

**Documentos de referencia:**
- TDD: `docs/tdd/M1-core-simulation.md` — §2 dominio, §3 bucle, §8 determinismo
- Spec: `docs/superpowers/specs/2026-08-11-sonar-preproduction-design.md` — §4.9 anomalía de la defensa

---

## Estructura de archivos

| Archivo | Responsabilidad |
|---|---|
| `src/Sonar.Core/Entities/EntityIds.cs` | Envoltorios tipados de id: `ContactId`, `TorpedoId`, `DefenseId` |
| `src/Sonar.Core/Entities/Contact.cs` | Contacto enemigo o inerte |
| `src/Sonar.Core/Entities/Torpedo.cs` | Torpedo propio o enemigo |
| `src/Sonar.Core/Entities/Defense.cs` | Defensa desplegada |
| `src/Sonar.Core/Entities/PlayerSub.cs` | Estado del submarino del jugador |
| `src/Sonar.Core/Sensing/Reading.cs` | Última lectura de sonar de un contacto |
| `src/Sonar.Core/Sensing/SonarState.cs` | Ángulo del haz y lecturas vigentes |
| `src/Sonar.Core/WorldState.cs` | Estado completo de la simulación en un tick |
| `src/Sonar.Core/Commands/Command.cs` | Jerarquía de comandos |
| `src/Sonar.Core/Events/GameEvent.cs` | Jerarquía de eventos |
| `src/Sonar.Core/Simulation.cs` | Bucle de tick y orden de actualización |
| `src/Sonar.Core/StateHash.cs` | Huella del estado para el test de determinismo |
| `tests/Sonar.Core.Tests/Support/TestWorld.cs` | Constructores de mundos para tests |
| `tests/Sonar.Core.Tests/Support/ScenarioRunner.cs` | Arnés headless de guiones |
| `tests/Sonar.Core.Tests/*` | Tests |

`Entities/` agrupa lo que cambia junto: agregar un campo a `Contact` casi siempre implica tocar `WorldState` y `Simulation`, y nada más.

---

## Task 1: Identidades tipadas

**Files:**
- Create: `src/Sonar.Core/Entities/EntityIds.cs`
- Test: `tests/Sonar.Core.Tests/Entities/EntityIdTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Entities/EntityIdTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Sonar.Core.Entities;
using Xunit;

namespace Sonar.Core.Tests.Entities;

public class EntityIdTests
{
    [Fact]
    public void ContactIds_WithSameValue_AreEqual()
    {
        Assert.Equal(new ContactId(7), new ContactId(7));
    }

    [Fact]
    public void ContactIds_SortByValue()
    {
        var ids = new List<ContactId> { new(3), new(1), new(2) };
        var sorted = ids.OrderBy(id => id.Value).Select(id => id.Value).ToList();
        Assert.Equal(new[] { 1, 2, 3 }, sorted);
    }

    [Fact]
    public void DistinctIdTypes_AreNotInterchangeable()
    {
        // Compila porque son tipos distintos; el test documenta la intención.
        var contact = new ContactId(1);
        var torpedo = new TorpedoId(1);
        Assert.Equal(contact.Value, torpedo.Value);
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'ContactId' could not be found`.

- [ ] **Step 3: Implementar los ids**

Crea `src/Sonar.Core/Entities/EntityIds.cs`:

```csharp
namespace Sonar.Core.Entities;

/// <summary>
/// Ids monótonos asignados por WorldState. Nunca se reutilizan dentro de una corrida:
/// son la clave de orden determinista en la resolución de colisiones simultáneas (TDD §3.3).
/// </summary>
public readonly record struct ContactId(int Value);

public readonly record struct TorpedoId(int Value);

public readonly record struct DefenseId(int Value);
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 81`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/Entities/EntityIds.cs tests/Sonar.Core.Tests/Entities/EntityIdTests.cs
git commit -m "feat(core): add typed entity ids"
```

---

## Task 2: Contact

**Files:**
- Create: `src/Sonar.Core/Entities/Contact.cs`
- Test: `tests/Sonar.Core.Tests/Entities/ContactTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Entities/ContactTests.cs`:

```csharp
using Sonar.Core.Entities;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Core.Tests.Entities;

public class ContactTests
{
    [Fact]
    public void Enemy_CarriesItsOwnVelocity()
    {
        var contact = new Contact
        {
            Id = new ContactId(1),
            Kind = ContactKind.Enemy,
            Position = new Vec2(0.5, 0.0),
            Velocity = new Vec2(-0.1, 0.0),
            Alive = true,
            NextFireTick = 100,
        };

        Assert.Equal(ContactKind.Enemy, contact.Kind);
        Assert.Equal(-0.1, contact.Velocity.X, precision: 10);
    }

    [Fact]
    public void With_ProducesModifiedCopyLeavingOriginalIntact()
    {
        var original = new Contact
        {
            Id = new ContactId(1),
            Kind = ContactKind.Inert,
            Position = new Vec2(0.5, 0.0),
            Velocity = Vec2.Zero,
            Alive = true,
            NextFireTick = 0,
        };

        var moved = original with { Position = new Vec2(0.6, 0.0) };

        Assert.Equal(0.5, original.Position.X, precision: 10);
        Assert.Equal(0.6, moved.Position.X, precision: 10);
        Assert.Equal(original.Id, moved.Id);
    }

    [Fact]
    public void Inert_HasZeroOwnVelocity()
    {
        var wreck = Contact.CreateInert(new ContactId(9), new Vec2(0.3, 0.4));
        Assert.Equal(Vec2.Zero, wreck.Velocity);
        Assert.Equal(ContactKind.Inert, wreck.Kind);
        Assert.True(wreck.Alive);
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'Contact' could not be found`.

- [ ] **Step 3: Implementar `Contact`**

Crea `src/Sonar.Core/Entities/Contact.cs`:

```csharp
using Sonar.Core.Spatial;

namespace Sonar.Core.Entities;

public enum ContactKind
{
    /// <summary>Submarino o nave enemiga. Se mueve por sí misma y dispara.</summary>
    Enemy,

    /// <summary>
    /// Pecio, bajo rocoso o boya. No ataca, no evade y no se mueve por sí misma.
    /// Existe para dar referencia visual de que el submarino avanza — sin ella la
    /// anomalía de la defensa no se puede leer (ver spec §4.9).
    /// </summary>
    Inert,
}

public sealed record Contact
{
    public required ContactId Id { get; init; }
    public required ContactKind Kind { get; init; }
    public required Vec2 Position { get; init; }

    /// <summary>Velocidad propia, sin incluir la deriva del mundo. Cero para Inert.</summary>
    public required Vec2 Velocity { get; init; }

    public required bool Alive { get; init; }

    /// <summary>Tick en el que este contacto puede volver a disparar. Solo aplica a Enemy.</summary>
    public required int NextFireTick { get; init; }

    public static Contact CreateInert(ContactId id, Vec2 position) => new()
    {
        Id = id,
        Kind = ContactKind.Inert,
        Position = position,
        Velocity = Vec2.Zero,
        Alive = true,
        NextFireTick = int.MaxValue,
    };
}
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 84`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/Entities/Contact.cs tests/Sonar.Core.Tests/Entities/ContactTests.cs
git commit -m "feat(core): add Contact entity with enemy and inert kinds"
```

---

## Task 3: Torpedo, Defense y PlayerSub

**Files:**
- Create: `src/Sonar.Core/Entities/Torpedo.cs`
- Create: `src/Sonar.Core/Entities/Defense.cs`
- Create: `src/Sonar.Core/Entities/PlayerSub.cs`
- Test: `tests/Sonar.Core.Tests/Entities/TorpedoTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Entities/TorpedoTests.cs`:

```csharp
using Sonar.Core.Entities;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Core.Tests.Entities;

public class TorpedoTests
{
    private static BandLayout FiveBands() =>
        new(deadZoneRadius: 0.10, outerRadii: new[] { 0.28, 0.46, 0.64, 0.82, 1.00 });

    [Fact]
    public void CreateOwn_StartsAtDeadZoneEdgeOnTheGivenBearing()
    {
        var layout = FiveBands();
        var target = new TargetCoordinate(new Bearing(90), new Band(2));
        var torpedo = Torpedo.CreateOwn(new TorpedoId(1), target, layout, speed: 0.7);

        // Marcación 90 es este: (1, 0). Nace al borde de la zona muerta.
        Assert.Equal(0.10, torpedo.Position.X, precision: 10);
        Assert.Equal(0.00, torpedo.Position.Y, precision: 10);
        Assert.Equal(TorpedoSide.Own, torpedo.Side);
    }

    [Fact]
    public void CreateOwn_RunsToBandCenterRadius()
    {
        var layout = FiveBands();
        var target = new TargetCoordinate(new Bearing(0), new Band(2));
        var torpedo = Torpedo.CreateOwn(new TorpedoId(1), target, layout, speed: 0.7);

        Assert.Equal(0.55, torpedo.RunToRadius, precision: 10);
        Assert.Equal(0.10, torpedo.ArmedFromRadius, precision: 10);
    }

    [Fact]
    public void CreateOwn_DirectionMatchesBearing()
    {
        var layout = FiveBands();
        var target = new TargetCoordinate(new Bearing(180), new Band(1));
        var torpedo = Torpedo.CreateOwn(new TorpedoId(1), target, layout, speed: 0.7);

        // Marcación 180 es sur: (0, 1).
        Assert.Equal(0.0, torpedo.Direction.X, precision: 10);
        Assert.Equal(1.0, torpedo.Direction.Y, precision: 10);
    }

    [Fact]
    public void CreateEnemy_TravelsTowardTheOrigin()
    {
        var origin = new Vec2(0.6, 0.8);   // longitud 1.0
        var torpedo = Torpedo.CreateEnemy(new TorpedoId(2), origin, speed: 0.45);

        Assert.Equal(TorpedoSide.Enemy, torpedo.Side);
        Assert.Equal(origin, torpedo.Position);
        // Dirección unitaria apuntando al origen: (-0.6, -0.8)
        Assert.Equal(-0.6, torpedo.Direction.X, precision: 10);
        Assert.Equal(-0.8, torpedo.Direction.Y, precision: 10);
        Assert.Equal(1.0, torpedo.Direction.Length, precision: 10);
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'Torpedo' could not be found`.

- [ ] **Step 3: Implementar las tres entidades**

Crea `src/Sonar.Core/Entities/Torpedo.cs`:

```csharp
using Sonar.Core.Spatial;

namespace Sonar.Core.Entities;

public enum TorpedoSide { Own, Enemy }

/// <summary>
/// Se dice torpedo, no misil, para ambos bandos: los misiles lanzados desde submarino
/// no existen hasta los años cincuenta y el §4 del GDD pide que la época se infiera de
/// la terminología.
/// </summary>
public sealed record Torpedo
{
    public required TorpedoId Id { get; init; }
    public required TorpedoSide Side { get; init; }
    public required Vec2 Position { get; init; }

    /// <summary>Dirección unitaria de viaje.</summary>
    public required Vec2 Direction { get; init; }

    public required double Speed { get; init; }

    // --- Solo Own. Para Enemy valen cero y no se consultan. ---
    public required Bearing Bearing { get; init; }
    public required double ArmedFromRadius { get; init; }
    public required double RunToRadius { get; init; }

    public static Torpedo CreateOwn(TorpedoId id, TargetCoordinate target, BandLayout layout, double speed)
    {
        var direction = target.Bearing.ToUnitVector();
        return new Torpedo
        {
            Id = id,
            Side = TorpedoSide.Own,
            Position = direction * layout.DeadZoneRadius,
            Direction = direction,
            Speed = speed,
            Bearing = target.Bearing,
            ArmedFromRadius = layout.DeadZoneRadius,
            RunToRadius = layout.CenterRadius(target.Band),
        };
    }

    /// <summary>
    /// Torpedo enemigo: viaja recto hacia el origen desde donde nació. El submarino está
    /// fijo en el origen, así que no existe la distinción entre dónde está y dónde estará.
    /// </summary>
    public static Torpedo CreateEnemy(TorpedoId id, Vec2 origin, double speed) => new()
    {
        Id = id,
        Side = TorpedoSide.Enemy,
        Position = origin,
        Direction = (Vec2.Zero - origin).Normalized(),
        Speed = speed,
        Bearing = new Bearing(0),
        ArmedFromRadius = 0,
        RunToRadius = 0,
    };
}
```

Crea `src/Sonar.Core/Entities/Defense.cs`:

```csharp
using Sonar.Core.Spatial;

namespace Sonar.Core.Entities;

/// <summary>
/// Defensa desplegada en una coordenada. Su posición NO recibe la deriva del mundo:
/// esa omisión deliberada es toda la implementación de la anomalía del §25 del GDD.
/// No agregarle deriva creyendo que es un bug — ver TDD §4.4.
/// </summary>
public sealed record Defense
{
    public required DefenseId Id { get; init; }
    public required Vec2 Position { get; init; }
    public required bool Alive { get; init; }

    public static Defense Create(DefenseId id, Vec2 position) => new()
    {
        Id = id,
        Position = position,
        Alive = true,
    };
}
```

Crea `src/Sonar.Core/Entities/PlayerSub.cs`:

```csharp
using Sonar.Core.Spatial;

namespace Sonar.Core.Entities;

/// <summary>
/// El submarino del jugador está siempre en el origen (0,0) y nunca se mueve en espacio
/// de simulación. Course y Speed solo sirven para calcular la deriva del mundo.
/// </summary>
public sealed record PlayerSub
{
    public required int AmmoLoaded { get; init; }
    public required int AmmoReserve { get; init; }
    public required int DefensesAvailable { get; init; }
    public required bool ReloadInProgress { get; init; }
    public required int ReloadCompletesAtTick { get; init; }
    public required Bearing Course { get; init; }
    public required double Speed { get; init; }

    /// <summary>Impactos recibidos. Al llegar a 2 termina la corrida (§20 del GDD).</summary>
    public required int HitsTaken { get; init; }

    public required bool Destroyed { get; init; }

    public static PlayerSub Create(SimulationConfig config, Bearing course, double speed) => new()
    {
        AmmoLoaded = config.MagazineSize,
        AmmoReserve = config.AmmoReserve,
        DefensesAvailable = config.DefenseCapacity,
        ReloadInProgress = false,
        ReloadCompletesAtTick = 0,
        Course = course,
        Speed = speed,
        HitsTaken = 0,
        Destroyed = false,
    };
}
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 88`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/Entities/Torpedo.cs src/Sonar.Core/Entities/Defense.cs src/Sonar.Core/Entities/PlayerSub.cs tests/Sonar.Core.Tests/Entities/TorpedoTests.cs
git commit -m "feat(core): add Torpedo, Defense and PlayerSub entities"
```

---

## Task 4: Estado del sonar

En este plan el barrido solo gira. El registro de lecturas con gating llega en el Plan 4.

**Files:**
- Create: `src/Sonar.Core/Sensing/Reading.cs`
- Create: `src/Sonar.Core/Sensing/SonarState.cs`
- Test: `tests/Sonar.Core.Tests/Sensing/SonarStateTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Sensing/SonarStateTests.cs`:

```csharp
using System.Linq;
using Sonar.Core.Entities;
using Sonar.Core.Sensing;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Core.Tests.Sensing;

public class SonarStateTests
{
    [Fact]
    public void Initial_StartsAtZeroAndRunning()
    {
        var state = SonarState.CreateInitial();
        Assert.Equal(0.0, state.SweepDegrees, precision: 10);
        Assert.True(state.SweepRunning);
        Assert.Empty(state.Readings);
    }

    [Fact]
    public void Reading_RecordsPositionAndTick()
    {
        var reading = new Reading
        {
            ContactId = new ContactId(3),
            Position = new Vec2(0.2, -0.4),
            ObservedAtTick = 120,
        };

        Assert.Equal(120, reading.ObservedAtTick);
        Assert.Equal(new ContactId(3), reading.ContactId);
    }

    [Fact]
    public void Readings_AreHeldInContactIdOrder()
    {
        var state = SonarState.CreateInitial() with
        {
            Readings = new[]
            {
                new Reading { ContactId = new ContactId(1), Position = Vec2.Zero, ObservedAtTick = 0 },
                new Reading { ContactId = new ContactId(4), Position = Vec2.Zero, ObservedAtTick = 0 },
                new Reading { ContactId = new ContactId(9), Position = Vec2.Zero, ObservedAtTick = 0 },
            },
        };

        var ids = state.Readings.Select(r => r.ContactId.Value).ToArray();
        Assert.Equal(ids.OrderBy(v => v).ToArray(), ids);
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'SonarState' could not be found`.

- [ ] **Step 3: Implementar el estado del sonar**

Crea `src/Sonar.Core/Sensing/Reading.cs`:

```csharp
using Sonar.Core.Entities;
using Sonar.Core.Spatial;

namespace Sonar.Core.Sensing;

/// <summary>
/// Lo último que el barrido registró de un contacto. Se guarda una sola lectura por
/// contacto, la más reciente: interpretar posiciones sucesivas es trabajo del jugador,
/// no del juego mostrándole una traza (TDD §5.2).
/// </summary>
public sealed record Reading
{
    public required ContactId ContactId { get; init; }
    public required Vec2 Position { get; init; }
    public required int ObservedAtTick { get; init; }
}
```

Crea `src/Sonar.Core/Sensing/SonarState.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Sonar.Core.Sensing;

public sealed record SonarState
{
    /// <summary>Ángulo actual del haz en grados, [0, 360).</summary>
    public required double SweepDegrees { get; init; }

    public required bool SweepRunning { get; init; }

    /// <summary>Lecturas vigentes, siempre ordenadas por ContactId ascendente.</summary>
    public required IReadOnlyList<Reading> Readings { get; init; }

    public static SonarState CreateInitial() => new()
    {
        SweepDegrees = 0,
        SweepRunning = true,
        Readings = Array.Empty<Reading>(),
    };
}
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 91`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/Sensing tests/Sonar.Core.Tests/Sensing
git commit -m "feat(core): add sonar state and readings"
```

---

## Task 5: WorldState

**Files:**
- Create: `src/Sonar.Core/WorldState.cs`
- Test: `tests/Sonar.Core.Tests/WorldStateTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/WorldStateTests.cs`:

```csharp
using System.Linq;
using Sonar.Core;
using Sonar.Core.Entities;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Core.Tests;

public class WorldStateTests
{
    private static SimulationConfig Config() => new()
    {
        TickDurationSeconds = 1.0 / 60.0,
        MaxCatchUpTicks = 5,
        DeadZoneRadius = 0.10,
        BandOuterRadii = new[] { 0.28, 0.46, 0.64, 0.82, 1.00 },
        SweepPeriodSeconds = 3.5,
        TorpedoSpeed = 0.70,
        HitRadius = 0.040,
        BlastRadius = 0.100,
        NearMissRadius = 0.160,
        EnemyTorpedoSpeed = 0.45,
        EnemyFireIntervalTicks = 420,
        EnemyFireStaggerTicks = 70,
        SubHitRadius = 0.045,
        DefenseRadius = 0.070,
        DefenseCapacity = 3,
        MagazineSize = 4,
        AmmoReserve = 12,
        ReloadTicks = 300,
    };

    [Fact]
    public void CreateInitial_StartsAtTickZeroWithFullMagazine()
    {
        var state = WorldState.CreateInitial(Config(), new Bearing(0), speed: 0.05);

        Assert.Equal(0, state.Tick);
        Assert.Equal(4, state.Sub.AmmoLoaded);
        Assert.Equal(12, state.Sub.AmmoReserve);
        Assert.False(state.Sub.Destroyed);
        Assert.Empty(state.Contacts);
        Assert.Empty(state.Torpedoes);
        Assert.Empty(state.Defenses);
    }

    [Fact]
    public void NextEntityId_StartsAtOne()
    {
        Assert.Equal(1, WorldState.CreateInitial(Config(), new Bearing(0), 0.05).NextEntityId);
    }

    [Fact]
    public void TakeEntityId_ReturnsIncrementingIdsAndAdvancesState()
    {
        var state = WorldState.CreateInitial(Config(), new Bearing(0), 0.05);

        var (first, afterFirst) = state.TakeEntityId();
        var (second, afterSecond) = afterFirst.TakeEntityId();

        Assert.Equal(1, first);
        Assert.Equal(2, second);
        Assert.Equal(3, afterSecond.NextEntityId);
        // El estado original no cambió.
        Assert.Equal(1, state.NextEntityId);
    }

    [Fact]
    public void WithContacts_KeepsThemSortedById()
    {
        var state = WorldState.CreateInitial(Config(), new Bearing(0), 0.05);
        var contacts = new[]
        {
            Contact.CreateInert(new ContactId(5), new Vec2(0.5, 0)),
            Contact.CreateInert(new ContactId(2), new Vec2(0.4, 0)),
            Contact.CreateInert(new ContactId(8), new Vec2(0.3, 0)),
        };

        var updated = state.WithContacts(contacts);
        var ids = updated.Contacts.Select(c => c.Id.Value).ToArray();

        Assert.Equal(new[] { 2, 5, 8 }, ids);
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'WorldState' could not be found`.

- [ ] **Step 3: Implementar `WorldState`**

Crea `src/Sonar.Core/WorldState.cs`:

```csharp
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
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 95`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/WorldState.cs tests/Sonar.Core.Tests/WorldStateTests.cs
git commit -m "feat(core): add WorldState with id-ordered collections"
```

---

## Task 6: Comandos y eventos

**Files:**
- Create: `src/Sonar.Core/Commands/Command.cs`
- Create: `src/Sonar.Core/Events/GameEvent.cs`
- Test: `tests/Sonar.Core.Tests/Commands/CommandTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Commands/CommandTests.cs`:

```csharp
using Sonar.Core.Commands;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Core.Tests.Commands;

public class CommandTests
{
    [Fact]
    public void FireCommand_CarriesItsTarget()
    {
        var target = new TargetCoordinate(new Bearing(47), new Band(2));
        var command = new FireCommand(target);
        Assert.Equal(target, command.Target);
    }

    [Fact]
    public void UnrecognizedInput_IsACommandNotAnError()
    {
        // El §10 del GDD exige que todo input quede registrado y que lo desconocido
        // simplemente no haga nada. Modelarlo como comando mantiene el historial uniforme.
        Command command = new UnrecognizedInput("hello");
        Assert.IsType<UnrecognizedInput>(command);
        Assert.Equal("hello", ((UnrecognizedInput)command).RawText);
    }

    [Fact]
    public void Events_CarryTheTickTheyOccurredOn()
    {
        GameEvent evt = new SonarToggled(Tick: 42, Running: false);
        Assert.Equal(42, evt.Tick);
    }

    [Fact]
    public void InputNotRecognized_PreservesRawText()
    {
        var evt = new InputNotRecognized(7, "what is this");
        Assert.Equal("what is this", evt.RawText);
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'FireCommand' could not be found`.

- [ ] **Step 3: Implementar comandos y eventos**

Crea `src/Sonar.Core/Commands/Command.cs`:

```csharp
using Sonar.Core.Spatial;

namespace Sonar.Core.Commands;

public abstract record Command;

public sealed record FireCommand(TargetCoordinate Target) : Command;

public sealed record DeployDefenseCommand(TargetCoordinate Target) : Command;

public sealed record ReloadCommand : Command;

public sealed record ToggleSonarCommand : Command;

public sealed record StatusCommand : Command;

/// <summary>
/// Texto que no corresponde a ningún comando. No es un error: el §10 del GDD pide que
/// todo input quede registrado y que lo desconocido simplemente no haga nada.
/// </summary>
public sealed record UnrecognizedInput(string RawText) : Command;
```

Crea `src/Sonar.Core/Events/GameEvent.cs`:

```csharp
using Sonar.Core.Spatial;

namespace Sonar.Core.Events;

public abstract record GameEvent(int Tick);

// --- Sistemas ---
public sealed record SonarToggled(int Tick, bool Running) : GameEvent(Tick);
public sealed record StatusReported(int Tick, int AmmoLoaded, int AmmoReserve, int Defenses)
    : GameEvent(Tick);
public sealed record InputNotRecognized(int Tick, string RawText) : GameEvent(Tick);
```

> Los eventos de combate y de amenaza enemiga se agregan a este archivo en el Plan 3.

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 99`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/Commands src/Sonar.Core/Events tests/Sonar.Core.Tests/Commands
git commit -m "feat(core): add command and event hierarchies"
```

---

## Task 7: Simulation — bucle de tick

El orden de actualización del TDD §3.2 se implementa completo desde el principio, aunque varios pasos queden vacíos hasta el Plan 3. Cambiar ese orden después cambia el resultado de la simulación.

**Files:**
- Create: `src/Sonar.Core/Simulation.cs`
- Test: `tests/Sonar.Core.Tests/Support/TestWorld.cs`
- Test: `tests/Sonar.Core.Tests/SimulationTickTests.cs`

- [ ] **Step 1: Crear el helper de mundos de prueba**

Crea `tests/Sonar.Core.Tests/Support/TestWorld.cs`:

```csharp
using Sonar.Core;
using Sonar.Core.Spatial;

namespace Sonar.Core.Tests.Support;

public static class TestWorld
{
    /// Configuración de trabajo válida. Los valores definitivos salen de M1.
    public static SimulationConfig Config() => new()
    {
        TickDurationSeconds = 1.0 / 60.0,
        MaxCatchUpTicks = 5,
        DeadZoneRadius = 0.10,
        BandOuterRadii = new[] { 0.28, 0.46, 0.64, 0.82, 1.00 },
        SweepPeriodSeconds = 3.5,
        TorpedoSpeed = 0.70,
        HitRadius = 0.040,
        BlastRadius = 0.100,
        NearMissRadius = 0.160,
        EnemyTorpedoSpeed = 0.45,
        EnemyFireIntervalTicks = 420,
        EnemyFireStaggerTicks = 70,
        SubHitRadius = 0.045,
        DefenseRadius = 0.070,
        DefenseCapacity = 3,
        MagazineSize = 4,
        AmmoReserve = 12,
        ReloadTicks = 300,
    };

    /// Submarino inmóvil: sin deriva del mundo. Útil para aislar movimiento propio.
    public static Simulation Still(SimulationConfig? config = null)
    {
        var c = config ?? Config();
        return new Simulation(c, new SeededRandomSource(1),
            WorldState.CreateInitial(c, new Bearing(0), speed: 0.0));
    }

    /// Submarino navegando al norte. Produce deriva del mundo hacia el sur.
    public static Simulation SteamingNorth(double speed = 0.10, SimulationConfig? config = null)
    {
        var c = config ?? Config();
        return new Simulation(c, new SeededRandomSource(1),
            WorldState.CreateInitial(c, new Bearing(0), speed));
    }
}
```

- [ ] **Step 2: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/SimulationTickTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Sonar.Core;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests;

public class SimulationTickTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    [Fact]
    public void Tick_AdvancesTheTickCounter()
    {
        var sim = TestWorld.Still();
        sim.Tick(NoCommands);
        sim.Tick(NoCommands);
        Assert.Equal(2, sim.State.Tick);
    }

    [Fact]
    public void Tick_AdvancesTheSweep()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);

        // Un período completo debe devolver el haz cerca de cero.
        var ticksPerSweep = (int)Math.Round(config.SweepPeriodSeconds / config.TickDurationSeconds);
        for (var i = 0; i < ticksPerSweep; i++) sim.Tick(NoCommands);

        Assert.InRange(sim.State.Sonar.SweepDegrees, 0.0, 1.0);
    }

    [Fact]
    public void Tick_DoesNotAdvanceSweepWhenStopped()
    {
        var sim = TestWorld.Still();
        sim.Tick(new Command[] { new ToggleSonarCommand() });
        var frozen = sim.State.Sonar.SweepDegrees;

        for (var i = 0; i < 100; i++) sim.Tick(NoCommands);

        Assert.False(sim.State.Sonar.SweepRunning);
        Assert.Equal(frozen, sim.State.Sonar.SweepDegrees, precision: 10);
    }

    [Fact]
    public void ToggleSonar_EmitsSonarToggledEvent()
    {
        var sim = TestWorld.Still();
        var events = sim.Tick(new Command[] { new ToggleSonarCommand() });

        var toggled = Assert.Single(events, e => e is SonarToggled);
        Assert.False(((SonarToggled)toggled).Running);
    }

    [Fact]
    public void UnrecognizedInput_EmitsEventAndChangesNothingElse()
    {
        var sim = TestWorld.Still();
        var before = sim.State.Sub;

        var events = sim.Tick(new Command[] { new UnrecognizedInput("hello") });

        var evt = Assert.Single(events, e => e is InputNotRecognized);
        Assert.Equal("hello", ((InputNotRecognized)evt).RawText);
        Assert.Equal(before, sim.State.Sub);
    }

    [Fact]
    public void StatusCommand_ReportsCurrentResources()
    {
        var sim = TestWorld.Still();
        var events = sim.Tick(new Command[] { new StatusCommand() });

        var report = (StatusReported)Assert.Single(events, e => e is StatusReported);
        Assert.Equal(4, report.AmmoLoaded);
        Assert.Equal(12, report.AmmoReserve);
        Assert.Equal(3, report.Defenses);
    }

    [Fact]
    public void EnemyContact_MovesByItsOwnVelocity()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[]
        {
            new Contact
            {
                Id = new ContactId(1),
                Kind = ContactKind.Enemy,
                Position = new Vec2(0.5, 0.0),
                Velocity = new Vec2(-0.6, 0.0),   // acercándose al oeste
                Alive = true,
                NextFireTick = int.MaxValue,
            },
        });

        for (var i = 0; i < 60; i++) sim.Tick(NoCommands);

        // 60 ticks = 1 segundo a -0.6 por segundo.
        Assert.Equal(0.5 - 0.6, sim.State.Contacts[0].Position.X, precision: 6);
    }

    [Fact]
    public void OwnTorpedo_MovesAlongItsBearing()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        var layout = config.BuildBandLayout();
        sim.SetTorpedoes(new[]
        {
            Torpedo.CreateOwn(new TorpedoId(1),
                new TargetCoordinate(new Bearing(90), new Band(4)), layout, config.TorpedoSpeed),
        });

        for (var i = 0; i < 60; i++) sim.Tick(NoCommands);

        // Nace en x = 0.10 y avanza al este a 0.70 por segundo.
        Assert.Equal(0.10 + 0.70, sim.State.Torpedoes[0].Position.X, precision: 6);
        Assert.Equal(0.0, sim.State.Torpedoes[0].Position.Y, precision: 6);
    }
}
```

- [ ] **Step 3: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'Simulation' could not be found`.

- [ ] **Step 4: Implementar `Simulation`**

Crea `src/Sonar.Core/Simulation.cs`:

```csharp
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

        // 2 · Rumbo y velocidad del submarino: los fija el guion de misión, no este bucle

        // 3 · Deriva del mundo
        var worldDrift = state.Sub.Course.ToUnitVector() * -state.Sub.Speed;

        // 4 · Mover contactos — reciben la deriva
        state = state.WithContacts(state.Contacts.Select(c =>
            c.Alive ? c with { Position = c.Position + (c.Velocity + worldDrift) * dt } : c));

        // 5 · Disparo enemigo — Plan 3

        // 6 · Mover torpedos
        state = state.WithTorpedoes(state.Torpedoes.Select(t =>
            t with { Position = t.Position + t.Direction * t.Speed * dt }));

        // 7 · Colisiones — Plan 3
        // 8 · Corridas agotadas y detonaciones — Plan 3
        // 9 · Impactos sobre el submarino — Plan 3

        // 10 · Barrido. El registro de lecturas con gating llega en el Plan 4.
        state = state with { Sonar = AdvanceSweep(state, dt) };

        // 11 · Recarga — Plan 3

        // 12 · Retirar entidades fuera de rango
        state = state
            .WithContacts(state.Contacts.Where(c => c.Alive))
            .WithTorpedoes(state.Torpedoes.Where(t => t.Position.Length <= 1.0));

        // 13 · Avanzar el contador
        state = state with { Tick = state.Tick + 1 };

        State = state;
        return events;
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

            // FireCommand, DeployDefenseCommand y ReloadCommand llegan en el Plan 3.
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
```

> `_random` y `_layout` todavía no se usan. Quedan inyectados desde ahora para que el Plan 3
> no tenga que cambiar la firma del constructor, que es lo que rompería los tests ya escritos.

- [ ] **Step 5: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 107`

- [ ] **Step 6: Commit**

```bash
git add src/Sonar.Core/Simulation.cs tests/Sonar.Core.Tests/Support/TestWorld.cs tests/Sonar.Core.Tests/SimulationTickTests.cs
git commit -m "feat(core): add Simulation tick loop with fixed update order"
```

---

## Task 8: La anomalía de la defensa

Este es el test más importante del plan. Verifica la propiedad de la que depende toda la capa narrativa del §25 del GDD.

**Files:**
- Test: `tests/Sonar.Core.Tests/DefenseAnomalyTests.cs`

- [ ] **Step 1: Escribir el test**

Crea `tests/Sonar.Core.Tests/DefenseAnomalyTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests;

/// <summary>
/// La anomalía del §25 del GDD no se implementa: se produce por una omisión deliberada.
/// Los contactos reciben la deriva del mundo; las defensas no. Como el submarino está fijo
/// en el origen, una defensa quieta en espacio de simulación es una defensa que acompaña
/// al submarino en la ficción, mientras un pecio se aleja.
///
/// Si alguno de estos tests falla porque alguien le agregó deriva a las defensas, la
/// anomalía dejó de existir. No es un bug: es la mecánica.
/// </summary>
public class DefenseAnomalyTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    [Fact]
    public void InertContact_DriftsAsTheSubSteamsForward()
    {
        var sim = TestWorld.SteamingNorth(speed: 0.10);
        sim.SetContacts(new[] { Contact.CreateInert(new ContactId(1), new Vec2(0.0, -0.50)) });

        for (var i = 0; i < 60; i++) sim.Tick(NoCommands);

        // El submarino avanza al norte a 0.10/s, así que el pecio deriva al sur 0.10 en un segundo.
        Assert.Equal(-0.40, sim.State.Contacts[0].Position.Y, precision: 6);
    }

    [Fact]
    public void Defense_DoesNotDrift()
    {
        var sim = TestWorld.SteamingNorth(speed: 0.10);
        sim.SetDefenses(new[] { Defense.Create(new DefenseId(1), new Vec2(0.0, -0.50)) });

        for (var i = 0; i < 60; i++) sim.Tick(NoCommands);

        Assert.Equal(-0.50, sim.State.Defenses[0].Position.Y, precision: 10);
    }

    [Fact]
    public void WreckAndDefense_StartTogetherAndSeparate()
    {
        // Esta es la observación que el jugador puede hacer: dos cosas en el mismo sitio,
        // y una se queda contigo.
        var sim = TestWorld.SteamingNorth(speed: 0.10);
        var start = new Vec2(0.0, -0.50);
        sim.SetContacts(new[] { Contact.CreateInert(new ContactId(1), start) });
        sim.SetDefenses(new[] { Defense.Create(new DefenseId(2), start) });

        for (var i = 0; i < 120; i++) sim.Tick(NoCommands);

        var separation = Vec2.Distance(sim.State.Contacts[0].Position, sim.State.Defenses[0].Position);
        Assert.Equal(0.20, separation, precision: 6);
    }

    [Fact]
    public void WithSubStopped_NothingDrifts()
    {
        // Control: sin avance no hay deriva, y por tanto no hay anomalía observable.
        var sim = TestWorld.Still();
        var start = new Vec2(0.0, -0.50);
        sim.SetContacts(new[] { Contact.CreateInert(new ContactId(1), start) });
        sim.SetDefenses(new[] { Defense.Create(new DefenseId(2), start) });

        for (var i = 0; i < 120; i++) sim.Tick(NoCommands);

        Assert.Equal(start.Y, sim.State.Contacts[0].Position.Y, precision: 10);
        Assert.Equal(start.Y, sim.State.Defenses[0].Position.Y, precision: 10);
    }
}
```

- [ ] **Step 2: Correr los tests para verificar que pasan**

No hace falta implementar nada: la propiedad ya existe por cómo se escribió el paso 4 del tick.

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 111`

Si el tercer test falla con separación cero, alguien agregó deriva a las defensas en `Simulation.Tick`. Quítala.

- [ ] **Step 3: Commit**

```bash
git add tests/Sonar.Core.Tests/DefenseAnomalyTests.cs
git commit -m "test(core): lock in the defense drift anomaly"
```

---

## Task 9: StateHash

**Files:**
- Create: `src/Sonar.Core/StateHash.cs`
- Test: `tests/Sonar.Core.Tests/StateHashTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/StateHashTests.cs`:

```csharp
using Sonar.Core;
using Sonar.Core.Entities;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests;

public class StateHashTests
{
    private static WorldState Base() =>
        WorldState.CreateInitial(TestWorld.Config(), new Bearing(0), 0.05);

    [Fact]
    public void IdenticalStates_HashEqual()
    {
        Assert.Equal(StateHash.Compute(Base()), StateHash.Compute(Base()));
    }

    [Fact]
    public void DifferentTick_HashesDiffer()
    {
        var other = Base() with { Tick = 1 };
        Assert.NotEqual(StateHash.Compute(Base()), StateHash.Compute(other));
    }

    [Fact]
    public void DifferentContactPosition_HashesDiffer()
    {
        var a = Base().WithContacts(new[] { Contact.CreateInert(new ContactId(1), new Vec2(0.5, 0.0)) });
        var b = Base().WithContacts(new[] { Contact.CreateInert(new ContactId(1), new Vec2(0.6, 0.0)) });
        Assert.NotEqual(StateHash.Compute(a), StateHash.Compute(b));
    }

    [Fact]
    public void DifferenceBelowQuantum_HashesEqual()
    {
        // El hash debe detectar divergencia real, no ruido de último bit. Sin cuantizar,
        // el test de determinismo se vuelve inestable y se termina ignorando.
        var a = Base().WithContacts(new[] { Contact.CreateInert(new ContactId(1), new Vec2(0.5, 0.0)) });
        var b = Base().WithContacts(new[] { Contact.CreateInert(new ContactId(1), new Vec2(0.5 + 1e-12, 0.0)) });
        Assert.Equal(StateHash.Compute(a), StateHash.Compute(b));
    }

    [Fact]
    public void DifferentAmmo_HashesDiffer()
    {
        var other = Base();
        other = other with { Sub = other.Sub with { AmmoLoaded = 1 } };
        Assert.NotEqual(StateHash.Compute(Base()), StateHash.Compute(other));
    }

    [Fact]
    public void DifferentDefensePosition_HashesDiffer()
    {
        var a = Base().WithDefenses(new[] { Defense.Create(new DefenseId(1), new Vec2(0.2, 0.2)) });
        var b = Base().WithDefenses(new[] { Defense.Create(new DefenseId(1), new Vec2(0.2, 0.3)) });
        Assert.NotEqual(StateHash.Compute(a), StateHash.Compute(b));
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'StateHash' could not be found`.

- [ ] **Step 3: Implementar `StateHash`**

Crea `src/Sonar.Core/StateHash.cs`:

```csharp
namespace Sonar.Core;

/// <summary>
/// Huella FNV-1a de 64 bits del estado del mundo. Los valores en coma flotante se cuantizan
/// antes de mezclarse: el hash debe detectar divergencia real, no ruido de último bit.
/// Ver TDD §8.2.
/// </summary>
public static class StateHash
{
    private const ulong Offset = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    /// <summary>Resolución de cuantización. Por debajo de esto dos valores son el mismo.</summary>
    private const double Quantum = 1e-9;

    public static long Compute(WorldState state)
    {
        var hash = Offset;

        MixInt(ref hash, state.Tick);
        MixInt(ref hash, state.NextEntityId);

        MixInt(ref hash, state.Sub.AmmoLoaded);
        MixInt(ref hash, state.Sub.AmmoReserve);
        MixInt(ref hash, state.Sub.DefensesAvailable);
        MixInt(ref hash, state.Sub.HitsTaken);
        MixInt(ref hash, state.Sub.Destroyed ? 1 : 0);
        MixInt(ref hash, state.Sub.ReloadInProgress ? 1 : 0);
        MixInt(ref hash, state.Sub.ReloadCompletesAtTick);

        MixDouble(ref hash, state.Sonar.SweepDegrees);
        MixInt(ref hash, state.Sonar.SweepRunning ? 1 : 0);

        foreach (var contact in state.Contacts)
        {
            MixInt(ref hash, contact.Id.Value);
            MixInt(ref hash, (int)contact.Kind);
            MixInt(ref hash, contact.Alive ? 1 : 0);
            MixDouble(ref hash, contact.Position.X);
            MixDouble(ref hash, contact.Position.Y);
        }

        foreach (var torpedo in state.Torpedoes)
        {
            MixInt(ref hash, torpedo.Id.Value);
            MixInt(ref hash, (int)torpedo.Side);
            MixDouble(ref hash, torpedo.Position.X);
            MixDouble(ref hash, torpedo.Position.Y);
        }

        foreach (var defense in state.Defenses)
        {
            MixInt(ref hash, defense.Id.Value);
            MixInt(ref hash, defense.Alive ? 1 : 0);
            MixDouble(ref hash, defense.Position.X);
            MixDouble(ref hash, defense.Position.Y);
        }

        foreach (var reading in state.Sonar.Readings)
        {
            MixInt(ref hash, reading.ContactId.Value);
            MixInt(ref hash, reading.ObservedAtTick);
            MixDouble(ref hash, reading.Position.X);
            MixDouble(ref hash, reading.Position.Y);
        }

        return unchecked((long)hash);
    }

    private static void MixInt(ref ulong hash, int value)
    {
        unchecked
        {
            for (var i = 0; i < 4; i++)
            {
                hash ^= (byte)(value >> (i * 8));
                hash *= Prime;
            }
        }
    }

    private static void MixDouble(ref ulong hash, double value)
    {
        var quantized = (long)System.Math.Round(value / Quantum);
        unchecked
        {
            for (var i = 0; i < 8; i++)
            {
                hash ^= (byte)(quantized >> (i * 8));
                hash *= Prime;
            }
        }
    }
}
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 117`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/StateHash.cs tests/Sonar.Core.Tests/StateHashTests.cs
git commit -m "feat(core): add quantized state hash"
```

---

## Task 10: ScenarioRunner y el test de determinismo

**Files:**
- Test: `tests/Sonar.Core.Tests/Support/ScenarioRunner.cs`
- Test: `tests/Sonar.Core.Tests/DeterminismTests.cs`

- [ ] **Step 1: Crear el arnés**

Crea `tests/Sonar.Core.Tests/Support/ScenarioRunner.cs`:

```csharp
using System;
using System.Collections.Generic;
using Sonar.Core;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;

namespace Sonar.Core.Tests.Support;

/// <summary>
/// Arnés headless: corre un guion (tick → comandos) y devuelve los eventos producidos.
/// Es lo que hace cierta la afirmación de que la campaña se puede jugar sin abrir Godot.
/// </summary>
public sealed class ScenarioRunner
{
    private readonly Simulation _simulation;
    private readonly IReadOnlyDictionary<int, IReadOnlyList<Command>> _script;

    /// <summary>
    /// Tick absoluto desde el arranque. Es un campo y no una variable de bucle a propósito:
    /// los tests de determinismo llaman Run(1) repetidamente, y con un contador local el
    /// guion volvería a aplicar los comandos del tick 0 en cada llamada.
    /// </summary>
    private int _tick;

    public List<GameEvent> Events { get; } = new();

    public ScenarioRunner(
        SimulationConfig config,
        int seed,
        IReadOnlyDictionary<int, IReadOnlyList<Command>> script,
        IEnumerable<Contact>? contacts = null,
        double subSpeed = 0.05)
    {
        _simulation = new Simulation(config, new SeededRandomSource(seed),
            WorldState.CreateInitial(config, new Bearing(0), subSpeed));
        if (contacts is not null) _simulation.SetContacts(contacts);
        _script = script;
    }

    /// <summary>Avanza `ticks` ticks más, continuando desde donde quedó.</summary>
    public WorldState Run(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            var commands = _script.TryGetValue(_tick, out var queued)
                ? queued
                : Array.Empty<Command>();
            Events.AddRange(_simulation.Tick(commands));
            _tick++;
        }
        return _simulation.State;
    }

    public WorldState State => _simulation.State;
}

public static class ScenarioScripts
{
    /// Guion fijo de referencia para los tests de determinismo.
    public static IReadOnlyDictionary<int, IReadOnlyList<Command>> MixedActivity() =>
        new Dictionary<int, IReadOnlyList<Command>>
        {
            [10] = new Command[] { new StatusCommand() },
            [40] = new Command[] { new ToggleSonarCommand() },
            [90] = new Command[] { new ToggleSonarCommand() },
            [150] = new Command[] { new UnrecognizedInput("what is this") },
            [220] = new Command[] { new StatusCommand() },
        };

    /// Contactos de referencia: dos enemigos en movimiento y un pecio inmóvil.
    public static IEnumerable<Contact> ReferenceContacts() => new[]
    {
        new Contact
        {
            Id = new ContactId(101), Kind = ContactKind.Enemy,
            Position = new Vec2(0.60, -0.30), Velocity = new Vec2(-0.08, 0.05),
            Alive = true, NextFireTick = int.MaxValue,
        },
        new Contact
        {
            Id = new ContactId(102), Kind = ContactKind.Enemy,
            Position = new Vec2(-0.45, 0.55), Velocity = new Vec2(0.06, -0.09),
            Alive = true, NextFireTick = int.MaxValue,
        },
        Contact.CreateInert(new ContactId(103), new Vec2(0.10, 0.80)),
    };
}
```

- [ ] **Step 2: Escribir el test de determinismo**

Crea `tests/Sonar.Core.Tests/DeterminismTests.cs`:

```csharp
using Sonar.Core;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests;

public class DeterminismTests
{
    private static long RunAndHash(int seed, int ticks)
    {
        var runner = new ScenarioRunner(
            TestWorld.Config(), seed,
            ScenarioScripts.MixedActivity(),
            ScenarioScripts.ReferenceContacts());
        return StateHash.Compute(runner.Run(ticks));
    }

    [Fact]
    public void SameSeed_ProducesIdenticalStateHash()
    {
        Assert.Equal(RunAndHash(12345, 3600), RunAndHash(12345, 3600));
    }

    [Fact]
    public void SameSeed_ProducesIdenticalEventStream()
    {
        var a = new ScenarioRunner(TestWorld.Config(), 12345,
            ScenarioScripts.MixedActivity(), ScenarioScripts.ReferenceContacts());
        var b = new ScenarioRunner(TestWorld.Config(), 12345,
            ScenarioScripts.MixedActivity(), ScenarioScripts.ReferenceContacts());

        a.Run(3600);
        b.Run(3600);

        Assert.Equal(a.Events.Count, b.Events.Count);
        for (var i = 0; i < a.Events.Count; i++) Assert.Equal(a.Events[i], b.Events[i]);
    }

    [Fact]
    public void ReRunningFromScratch_ReproducesEveryIntermediateTick()
    {
        // No basta con que el estado final coincida: la divergencia puede aparecer y
        // cancelarse. Comparamos la huella tick a tick.
        var a = new ScenarioRunner(TestWorld.Config(), 999,
            ScenarioScripts.MixedActivity(), ScenarioScripts.ReferenceContacts());
        var b = new ScenarioRunner(TestWorld.Config(), 999,
            ScenarioScripts.MixedActivity(), ScenarioScripts.ReferenceContacts());

        for (var tick = 0; tick < 600; tick++)
        {
            a.Run(1);
            b.Run(1);
            Assert.Equal(StateHash.Compute(a.State), StateHash.Compute(b.State));
        }
    }
}
```

> **Nota sobre el test de semillas distintas.** El TDD §10.1 pide comprobar que dos semillas
> produzcan resultados distintos, porque si no una simulación accidentalmente sin azar pasaría
> el primer test y nadie se enteraría. En este plan la simulación **todavía no consume azar**,
> así que ese test se agrega en el Plan 3, cuando exista la primera fuente que lo use.

- [ ] **Step 3: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 120`

- [ ] **Step 4: Verificar que no se colaron diccionarios en la simulación**

```bash
grep -rn "Dictionary\|HashSet" src/Sonar.Core/ || echo "sin colecciones de orden no garantizado"
```

Expected: `sin colecciones de orden no garantizado`

- [ ] **Step 5: Commit**

```bash
git add tests/Sonar.Core.Tests/Support/ScenarioRunner.cs tests/Sonar.Core.Tests/DeterminismTests.cs
git commit -m "test(core): add scenario runner and determinism tests"
```

---

## Definition of Done — Plan 2

- [ ] `dotnet build` termina sin advertencias ni errores
- [ ] `dotnet test` reporta 120 tests pasando, 0 fallando
- [ ] `grep -rn "Dictionary\|HashSet" src/Sonar.Core/` no devuelve nada
- [ ] `DefenseAnomalyTests` pasa — la anomalía del §25 existe y está protegida por tests
- [ ] `DeterminismTests` compara huellas tick a tick, no solo el estado final
- [ ] Diez commits, uno por tarea

## Qué sigue

**Plan 3 — Combate y amenaza:** disparo, impacto directo, detonación de fin de corrida, casi-impacto, defensas, munición y recarga, IA enemiga con escalonado de disparo, torpedos enemigos y supervivencia del jugador a dos impactos.
