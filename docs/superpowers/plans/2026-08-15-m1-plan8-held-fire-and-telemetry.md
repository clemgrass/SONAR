# Plan 8 — Fuego contenido, embestida y telemetría

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que los enemigos se cuiden entre ellos —y que se equivoquen de vez en cuando—, que el RAM avise antes de matar, y que veinte testers produzcan datos que se puedan separar por personalidad.

**Architecture:** Invierte la regla de fuego amigo del TDD §4.5, que hoy lo hace imposible. Lo raro no sale de un dado: sale de que los enemigos **no disparan a través de un compañero**, y de que ese chequeo proyecta en línea recta mientras los cascos viran. Encima, la telemetría gana la columna que hace separables los datos.

**Tech Stack:** C# / .NET (TFM `net9.0`), xUnit, Godot 4.7 mono. Sin dependencias nuevas.

**Depende de:** [Plan 7](2026-08-15-m1-plan7-enemy-mind.md) completo. Este plan usa `Contact.Personality`, `Personality.Rams`, `EstimatedSubPosition` y `SimulationConfig.PassiveHearingRadius`.

**Spec:** [2026-08-15-enemy-ai-design.md](../specs/2026-08-15-enemy-ai-design.md)

---

## Estructura de archivos

| Archivo | Responsabilidad |
|---|---|
| `src/Sonar.Core/Events/GameEvent.cs` | Tres eventos nuevos: `FireHeldForFriendly`, `RammingRunDetected`, y `ContactDestroyed` gana la causa. |
| `src/Sonar.Core/Simulation.cs` | Fuego amigo en dos fases existentes, más el chequeo de carril y el aviso del RAM. |
| `src/Sonar.Core/SimulationConfig.cs` | `FriendlyLaneRadius`. |
| `src/Sonar.Game/Telemetry/SessionRecorder.cs` | La personalidad en cada línea y los contadores nuevos. |
| `src/Sonar.Game/Main.cs` | Los eventos nuevos en el registro. |
| `docs/tdd/M1-core-simulation.md` | Se revierte el §4.5. |

**Convención de nombres:** `SimulationConfig.FriendlyLaneRadius` · `Simulation.LaneIsBlocked` · `Simulation.WarnAboutRammingRuns` · `FireHeldForFriendly(Tick, ContactId, BlockedByContactId)` · `RammingRunDetected(Tick, ContactId, Distance)` · `DestructionCause.FriendlyFire`.

---

### Task 1: Un torpedo enemigo puede destruir a un submarino enemigo

**Files:**
- Modify: `src/Sonar.Core/Simulation.cs` (`ResolveDirectHits`)
- Modify: `src/Sonar.Core/Events/GameEvent.cs`
- Modify: `tests/Sonar.Core.Tests/Combat/SurvivalTests.cs` (el test que afirma lo contrario)
- Test: `tests/Sonar.Core.Tests/Combat/FriendlyFireTests.cs` *(nuevo)*

- [ ] **Step 1: Leer el test que hay que invertir**

Run: `grep -n "EnemyTorpedo_PassesThroughOtherEnemies" -A 22 tests/Sonar.Core.Tests/Combat/SurvivalTests.cs`

Ese test afirma que un torpedo enemigo atraviesa a otro enemigo. El spec §7 revierte esa decisión, así que el test se reescribe — no se borra, porque su escenario sigue siendo el bueno.

- [ ] **Step 2: Escribir el test nuevo**

Crear `tests/Sonar.Core.Tests/Combat/FriendlyFireTests.cs`:

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

public class FriendlyFireTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Contact Enemy(int id, Vec2 position, int nextFireTick) => new()
    {
        Id = new ContactId(id),
        Kind = ContactKind.Enemy,
        Position = position,
        Velocity = Vec2.Zero,
        Alive = true,
        NextFireTick = nextFireTick,
    };

    [Fact]
    public void AnEnemyTorpedoDestroysAnEnemySubmarineInItsPath()
    {
        // Revierte el TDD §4.5. El de atrás dispara al origen y el de adelante está justo
        // sobre esa línea. Se le pone NextFireTick imposible al de adelante para que el
        // único torpedo del escenario sea el del de atrás.
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            Enemy(1, new Vec2(0.0, -0.90), nextFireTick: 0),
            Enemy(2, new Vec2(0.0, -0.50), nextFireTick: int.MaxValue),
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 400; i++) events.AddRange(sim.Tick(NoCommands));

        var killed = Assert.Single(events.OfType<ContactDestroyed>(), e => e.ContactId == 2);
        Assert.Equal(DestructionCause.FriendlyFire, killed.Cause);
        Assert.DoesNotContain(sim.State.Contacts, c => c.Id.Value == 2);
    }

    [Fact]
    public void AnEnemyTorpedoStillPassesThroughWrecks()
    {
        // Los pecios siguen inmunes: la anomalía de defensas del §25 necesita que ese pecio
        // sobreviva como referencia observable.
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            Enemy(1, new Vec2(0.0, -0.90), nextFireTick: 0),
            Contact.CreateInert(new ContactId(2), new Vec2(0.0, -0.50)),
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 400; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.Contains(sim.State.Contacts, c => c.Id.Value == 2);

        // Control positivo: el torpedo recorrió la línea entera, así que cruzó el pecio.
        Assert.Contains(events, e => e is SubHit);
    }

    [Fact]
    public void AnEnemyTorpedoDoesNotDestroyTheContactThatFiredIt()
    {
        // Sin esta guarda, un torpedo detonaría contra su propio lanzador en el tick de salida:
        // nace exactamente encima de él.
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { Enemy(1, new Vec2(0.0, -0.90), nextFireTick: 0) });

        var events = new List<GameEvent>();
        for (var i = 0; i < 10; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.DoesNotContain(events, e => e is ContactDestroyed);
        Assert.Contains(sim.State.Contacts, c => c.Id.Value == 1);
    }
}
```

- [ ] **Step 3: Correrlos y verlos fallar**

Run: `dotnet test tests/Sonar.Core.Tests --filter "FullyQualifiedName~FriendlyFireTests"`
Expected: FAIL a la compilación — `DestructionCause` no tiene `FriendlyFire`.

- [ ] **Step 4: Agregar la causa**

En `src/Sonar.Core/Events/GameEvent.cs`, agregar a `DestructionCause`:

```csharp
    /// <summary>
    /// Un torpedo enemigo alcanzó a otro enemigo. Tiene su propia causa porque en los datos
    /// hay que poder separarla de una muerte que se anotó el jugador.
    /// </summary>
    FriendlyFire,
```

- [ ] **Step 5: Dejar que los torpedos enemigos impacten**

En `src/Sonar.Core/Simulation.cs`, `ResolveDirectHits`, reemplazar la guarda
`if (torpedo.Side != TorpedoSide.Own) continue;` por el cuerpo completo del bucle:

```csharp
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
```

- [ ] **Step 6: Reescribir el test que afirmaba lo contrario**

En `tests/Sonar.Core.Tests/Combat/SurvivalTests.cs`, reemplazar `EnemyTorpedo_PassesThroughOtherEnemies` entero por:

```csharp
    [Fact]
    public void EnemyTorpedo_StopsAtTheFirstEnemyItCrosses()
    {
        // Antes este test afirmaba que lo atravesaba. El spec del 2026-08-15 §7 revierte esa
        // decisión del TDD §4.5: ahora el compañero muere y el torpedo no llega al submarino.
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            EnemyReadyToFire(1, new Vec2(0.0, -0.90), nextFireTick: 0),
            EnemyReadyToFire(2, new Vec2(0.0, -0.50), nextFireTick: int.MaxValue),
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 200; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.DoesNotContain(sim.State.Contacts, c => c.Id.Value == 2);
        Assert.DoesNotContain(events, e => e is SubHit);
    }
```

- [ ] **Step 7: Correr toda la suite**

Run: `dotnet test`
Expected: PASS. Revisar con cuidado cualquier otro test que falle: si un escenario tenía dos enemigos alineados sin querer, **hay que mover el contacto, no debilitar la aserción.**

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(core): let enemy torpedoes kill enemy submarines"
```

---

### Task 2: Un torpedo enemigo puede destruir a otro torpedo enemigo

**Files:**
- Modify: `src/Sonar.Core/Simulation.cs` (`ResolveTorpedoCollisions`)
- Test: `tests/Sonar.Core.Tests/Combat/FriendlyFireTests.cs`

- [ ] **Step 1: Escribir el test**

Agregar a `FriendlyFireTests`:

```csharp
    [Fact]
    public void TwoEnemyTorpedoesDestroyEachOther()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(Array.Empty<Contact>());
        // Dos torpedos enemigos de frente, de lanzadores distintos, que se cruzan.
        sim.SetTorpedoes(new[]
        {
            Torpedo.CreateEnemyToward(new TorpedoId(901), new Vec2(-0.30, 0.0),
                new Vec2(0.30, 0.0), 0.03, sourceContactId: 1),
            Torpedo.CreateEnemyToward(new TorpedoId(902), new Vec2(0.30, 0.0),
                new Vec2(-0.30, 0.0), 0.03, sourceContactId: 2),
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 700; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.Equal(2, events.OfType<EnemyTorpedoDestroyed>()
                              .Count(e => e.Cause == DestructionCause.TorpedoCollision));
        Assert.Empty(sim.State.Torpedoes);
    }

    [Fact]
    public void ATorpedoDoesNotCollideWithItself()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(Array.Empty<Contact>());
        sim.SetTorpedoes(new[]
        {
            Torpedo.CreateEnemyToward(new TorpedoId(901), new Vec2(-0.30, 0.0),
                new Vec2(0.30, 0.0), 0.03, sourceContactId: 1),
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 60; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.DoesNotContain(events, e => e is EnemyTorpedoDestroyed);
    }
```

- [ ] **Step 2: Correrlos y verlos fallar**

Run: `dotnet test tests/Sonar.Core.Tests --filter "FullyQualifiedName~TwoEnemyTorpedoesDestroyEachOther"`
Expected: FAIL — hoy sólo se cruzan propio contra enemigo.

- [ ] **Step 3: Agregar el cruce enemigo contra enemigo**

En `ResolveTorpedoCollisions`, después del bucle que ya existe, agregar:

```csharp
        // Enemigo contra enemigo. Va en un bucle aparte y no mezclado con el de arriba para
        // que el orden de resolución siga siendo el de ids ascendentes dentro de cada clase,
        // que es lo que hace reproducible el desempate.
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
```

- [ ] **Step 4: Correr toda la suite**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): let enemy torpedoes destroy each other"
```

---

### Task 3: No disparan a través de un compañero

**Files:**
- Modify: `src/Sonar.Core/SimulationConfig.cs`
- Modify: `src/Sonar.Core/Simulation.cs` (`FireEnemyTorpedoes`)
- Modify: `src/Sonar.Core/Events/GameEvent.cs`
- Modify: `config/simulation.working.json` y los constructores de config de test
- Test: `tests/Sonar.Core.Tests/Combat/HeldFireTests.cs` *(nuevo)*

- [ ] **Step 1: Escribir los tests**

Crear `tests/Sonar.Core.Tests/Combat/HeldFireTests.cs`:

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

public class HeldFireTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Contact Enemy(int id, Vec2 position, int nextFireTick) => new()
    {
        Id = new ContactId(id),
        Kind = ContactKind.Enemy,
        Position = position,
        Velocity = Vec2.Zero,
        Alive = true,
        NextFireTick = nextFireTick,
    };

    [Fact]
    public void AnEnemyHoldsFireWhenAFriendIsInTheLane()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            Enemy(1, new Vec2(0.0, -0.90), nextFireTick: 0),
            Enemy(2, new Vec2(0.0, -0.50), nextFireTick: int.MaxValue),   // justo en medio
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.DoesNotContain(events, e => e is EnemyTorpedoFired);
        Assert.Contains(events, e => e is FireHeldForFriendly);
    }

    [Fact]
    public void AnEnemyWithAClearLaneFires()
    {
        // Control positivo: el mismo escenario con el compañero fuera de la línea.
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            Enemy(1, new Vec2(0.0, -0.90), nextFireTick: 0),
            Enemy(2, new Vec2(0.50, 0.0), nextFireTick: int.MaxValue),
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.Contains(events, e => e is EnemyTorpedoFired);
    }

    [Fact]
    public void AFriendlyTorpedoAlsoBlocksTheLane()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { Enemy(1, new Vec2(0.0, -0.90), nextFireTick: 0) });
        sim.SetTorpedoes(new[]
        {
            Torpedo.CreateEnemyToward(new TorpedoId(901), new Vec2(0.0, -0.50),
                new Vec2(0.0, -0.49), 0.0, sourceContactId: 2),
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 60; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.DoesNotContain(events, e => e is EnemyTorpedoFired);
    }

    [Fact]
    public void TheEnemyFiresOnceTheFriendMovesOutOfTheWay()
    {
        // Aguantar no es renunciar: vuelve a mirar cada tick.
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            Enemy(1, new Vec2(0.0, -0.90), nextFireTick: 0),
            Enemy(2, new Vec2(0.0, -0.50), nextFireTick: int.MaxValue),
        });
        for (var i = 0; i < 60; i++) sim.Tick(NoCommands);

        // Se aparta el compañero.
        sim.SetContacts(new[]
        {
            sim.State.Contacts.Single(c => c.Id.Value == 1),
            Enemy(2, new Vec2(0.70, 0.0), nextFireTick: int.MaxValue),
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 60; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.Contains(events, e => e is EnemyTorpedoFired);
    }
}
```

- [ ] **Step 2: Correrlos y verlos fallar**

Run: `dotnet test tests/Sonar.Core.Tests --filter "FullyQualifiedName~HeldFireTests"`
Expected: FAIL a la compilación — `FireHeldForFriendly` no existe.

- [ ] **Step 3: Agregar el evento y el radio de carril**

En `src/Sonar.Core/Events/GameEvent.cs`:

```csharp
/// <summary>
/// Un enemigo estaba listo para disparar y no lo hizo porque tenía un compañero en la línea.
/// Es invisible en pantalla: sin este evento no hay forma de saber si los enemigos se están
/// coordinando o si simplemente dejaron de disparar. Se emite muestreado, no en cada tick.
/// </summary>
public sealed record FireHeldForFriendly(int Tick, int ContactId, int BlockedByContactId)
    : GameEvent(Tick);
```

En `src/Sonar.Core/SimulationConfig.cs`:

```csharp
    /// <summary>
    /// Ancho del corredor que un enemigo considera bloqueado por un compañero. Arranca en
    /// HitRadius: evita exactamente aquello con lo que chocaría. Subirlo los vuelve más
    /// cautos y baja el volumen de fuego; bajarlo produce más accidentes. Es la perilla con
    /// la que se afina qué tan raro resulta el fuego amigo.
    /// </summary>
    public double FriendlyLaneRadius { get; init; }
```

En `Validate()`: `Positive(FriendlyLaneRadius, nameof(FriendlyLaneRadius));`

En `config/simulation.working.json`, después de `"passiveHearingRadius"`: `"friendlyLaneRadius": 0.040,`

Y `FriendlyLaneRadius = 0.040,` en los seis constructores de config de test y en los dos JSON literales de `SimulationConfigLoaderTests.cs`.

- [ ] **Step 4: Escribir el chequeo de carril**

En `src/Sonar.Core/Simulation.cs`:

```csharp
    /// <summary>
    /// ¿Hay un compañero en la línea de tiro? Los enemigos se conocen perfectamente, así que
    /// no se disparan entre ellos a propósito: si el carril está tapado, aguantan.
    ///
    /// La proyección es <b>lineal</b> — se estima dónde va a estar el compañero cuando el
    /// torpedo pase por su altura, suponiendo que sigue derecho. Los cascos con personalidad
    /// viran hacia su radio preferido, así que la predicción falla de vez en cuando y ahí es
    /// donde ocurre el accidente. Eso es exactamente lo que se busca: raro, y por una razón
    /// física, no por un dado.
    /// </summary>
    private bool LaneIsBlocked(
        Contact shooter, Vec2 target, List<Contact> contacts, List<Torpedo> torpedoes,
        Vec2 worldDrift, out int blockedBy)
    {
        blockedBy = -1;

        var lane = target - shooter.Position;
        var length = lane.Length;
        if (length < 1e-9) return false;

        var direction = lane * (1.0 / length);
        var speed = _config.EnemyTorpedoSpeed;

        bool Blocks(Vec2 position, Vec2 velocity)
        {
            // Distancia a lo largo del carril hasta el punto más cercano, acotada al tramo.
            var toIt = position - shooter.Position;
            var along = toIt.X * direction.X + toIt.Y * direction.Y;
            if (along <= 0.0 || along >= length) return false;

            // Cuándo llegaría el torpedo a esa altura, y dónde estaría el compañero entonces.
            var seconds = along / speed;
            var projected = position + (velocity + worldDrift) * seconds;

            var toProjected = projected - shooter.Position;
            var alongProjected = toProjected.X * direction.X + toProjected.Y * direction.Y;
            var onLane = shooter.Position + direction * alongProjected;

            return Vec2.Distance(projected, onLane) < _config.FriendlyLaneRadius;
        }

        foreach (var other in contacts)
        {
            if (!other.Alive || other.Kind != ContactKind.Enemy) continue;
            if (other.Id.Value == shooter.Id.Value) continue;
            if (!Blocks(other.Position, other.Velocity)) continue;
            blockedBy = other.Id.Value;
            return true;
        }

        foreach (var torpedo in torpedoes)
        {
            if (torpedo.Side != TorpedoSide.Enemy) continue;
            if (torpedo.SourceContactId == shooter.Id.Value) continue;
            if (!Blocks(torpedo.Position, torpedo.Direction * torpedo.Speed - worldDrift)) continue;
            blockedBy = torpedo.SourceContactId;
            return true;
        }

        return false;
    }
```

- [ ] **Step 5: Usarlo antes de disparar**

En `FireEnemyTorpedoes`, después de calcular `target` y antes de crear el torpedo:

```csharp
            var target = EstimatedSubPosition(contact, state.Tick, worldDrift, dt);

            if (LaneIsBlocked(contact, target, contacts, torpedoes, worldDrift, out var blockedBy))
            {
                // Muestreado, no en cada tick: un enemigo puede pasar cientos de ticks con el
                // carril tapado y no hace falta una línea por cada uno para saber que ocurre.
                if (state.Tick % 60 == 0)
                    events.Add(new FireHeldForFriendly(state.Tick, contact.Id.Value, blockedBy));
                continue;
            }
```

- [ ] **Step 6: Correr toda la suite**

Run: `dotnet test`
Expected: PASS. **Aviso:** varios tests de combate ponen dos enemigos alineados con el origen. Si alguno falla ahora porque el de atrás aguanta el tiro, mover el contacto fuera de la línea — no bajar `FriendlyLaneRadius`.

- [ ] **Step 7: Mutación — que el chequeo muerda**

```bash
mkdir -p /tmp/mut && cp src/Sonar.Core/Simulation.cs /tmp/mut/
perl -pi -e 's/if \(LaneIsBlocked\(contact, target, contacts, torpedoes, worldDrift, out var blockedBy\)\)/if (false \&\& LaneIsBlocked(contact, target, contacts, torpedoes, worldDrift, out var blockedBy))/' src/Sonar.Core/Simulation.cs
dotnet test tests/Sonar.Core.Tests --filter "FullyQualifiedName~HeldFireTests"
```
Expected: FAIL `AnEnemyHoldsFireWhenAFriendIsInTheLane`. Restaurar y confirmar con `diff -q`.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(core): make enemies hold fire rather than shoot through a friend"
```

---

### Task 4: El RAM avisa antes de matar

**Files:**
- Modify: `src/Sonar.Core/Events/GameEvent.cs`
- Modify: `src/Sonar.Core/Simulation.cs`
- Modify: `src/Sonar.Game/Main.cs`
- Test: `tests/Sonar.Core.Tests/Combat/RammingTests.cs` *(nuevo)*

- [ ] **Step 1: Escribir los tests**

Crear `tests/Sonar.Core.Tests/Combat/RammingTests.cs`:

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

public class RammingTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Contact Rammer(int id, double radius) => new()
    {
        Id = new ContactId(id),
        Kind = ContactKind.Enemy,
        Position = new Vec2(0.0, -radius),
        Velocity = Vec2.Zero,
        Alive = true,
        NextFireTick = int.MaxValue,
        LastFixTick = 0,
        Personality = PersonalityCatalog.Ram,
    };

    [Fact]
    public void ARammingRunIsAnnouncedBeforeItArrives()
    {
        // Un kamikaze silencioso mata con algo que no se pudo anticipar. El aviso es lo que
        // convierte la embestida en algo que el jugador pudo leer.
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { Rammer(1, config.PassiveHearingRadius * 0.8) });

        var events = new List<GameEvent>();
        for (var i = 0; i < 600 && !sim.State.Sub.Destroyed; i++)
            events.AddRange(sim.Tick(NoCommands));

        var warnings = events.OfType<RammingRunDetected>().ToList();
        Assert.NotEmpty(warnings);

        var death = events.OfType<SubDestroyed>().FirstOrDefault();
        if (death is not null)
            Assert.True(warnings[0].Tick < death.Tick, "el aviso tiene que llegar antes que la muerte");
    }

    [Fact]
    public void OnlyRammersAnnounceThemselves()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[]
        {
            Rammer(1, config.PassiveHearingRadius * 0.8) with
            {
                Personality = PersonalityCatalog.Stalker,
            },
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 600; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.DoesNotContain(events, e => e is RammingRunDetected);
    }

    [Fact]
    public void ARammerFarAwayIsNotYetAnnounced()
    {
        // El aviso es de proximidad, no de existencia: si sonara desde el borde, dejaría de
        // significar "esto viene por ti ahora".
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { Rammer(1, 0.95) });

        var events = new List<GameEvent>();
        for (var i = 0; i < 30; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.DoesNotContain(events, e => e is RammingRunDetected);
    }
}
```

- [ ] **Step 2: Correrlos y verlos fallar**

Run: `dotnet test tests/Sonar.Core.Tests --filter "FullyQualifiedName~RammingTests"`
Expected: FAIL a la compilación — `RammingRunDetected` no existe.

- [ ] **Step 3: Agregar el evento**

En `src/Sonar.Core/Events/GameEvent.cs`:

```csharp
/// <summary>
/// Un contacto que mata por contacto está lo bastante cerca como para oírse acelerar. Se
/// repite mientras siga cerca: es un redoble, no un aviso único.
/// </summary>
public sealed record RammingRunDetected(int Tick, int ContactId, double Distance)
    : GameEvent(Tick);
```

- [ ] **Step 4: Emitirlo**

En `src/Sonar.Core/Simulation.cs`:

```csharp
    /// <summary>Cada cuántos ticks se repite el aviso de embestida. Tres segundos: redoble,
    /// no ráfaga — el registro del juego está paceado y una línea por tick lo ahogaría.</summary>
    private const int RamWarningIntervalTicks = 180;

    /// <summary>
    /// Fase 9c · El ruido de aproximación del RAM. Sin esto, la muerte por embestida llega
    /// desde una marca que el jugador vio hace siete segundos y se siente como una trampa.
    /// </summary>
    private void WarnAboutRammingRuns(List<Contact> contacts, List<GameEvent> events, int tick)
    {
        if (tick % RamWarningIntervalTicks != 0) return;

        foreach (var contact in contacts)
        {
            if (!contact.Alive || contact.Personality is null) continue;
            if (!contact.Personality.Rams) continue;

            var distance = contact.Position.Length;
            if (distance > _config.PassiveHearingRadius) continue;

            events.Add(new RammingRunDetected(tick, contact.Id.Value, distance));
        }
    }
```

Y llamarla en `Tick`, después de la fase 9b:

```csharp
        // 9c · Ruido de aproximación de lo que mata por contacto
        WarnAboutRammingRuns(contacts, events, state.Tick);
```

- [ ] **Step 5: Mostrarlo en el registro del juego**

En `src/Sonar.Game/Main.cs`, en el `switch` de eventos que alimenta el log, agregar:

```csharp
            case RammingRunDetected e:
                Instrument($"!! FAST SCREWS CLOSING · RANGE {e.Distance:F2}");
                break;
```

El evento lleva distancia y no rumbo a propósito: el jugador tiene que ir al scope a
averiguar **cuál** de las marcas viene por él. Un aviso que ya trae el rumbo resuelve el
problema en vez de plantearlo.

- [ ] **Step 6: Correr todo**

Run: `dotnet test`
Expected: PASS.

Run: `dotnet build src/Sonar.Game -v q --nologo`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(core): make the ramming run audible before it lands"
```

---

### Task 5: La personalidad en la telemetría

**Files:**
- Modify: `src/Sonar.Core/Events/GameEvent.cs` (`ContactDestroyed`, `EnemyTorpedoFired`)
- Modify: `src/Sonar.Core/Simulation.cs`
- Modify: `src/Sonar.Game/Telemetry/SessionRecorder.cs`
- Test: `tests/Sonar.Core.Tests/Combat/PersonalityTelemetryTests.cs` *(nuevo)*

- [ ] **Step 1: Escribir el test**

Crear `tests/Sonar.Core.Tests/Combat/PersonalityTelemetryTests.cs`:

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

public class PersonalityTelemetryTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    [Fact]
    public void EveryEnemyShotCarriesThePersonalityThatFiredIt()
    {
        // Con cinco personalidades y veinte testers, sin esta columna los datos son el
        // promedio de cinco cosas distintas y no responden nada.
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            new Contact
            {
                Id = new ContactId(1), Kind = ContactKind.Enemy,
                Position = new Vec2(0.0, -0.90), Velocity = Vec2.Zero,
                Alive = true, NextFireTick = 0, LastFixTick = 0,
                Personality = PersonalityCatalog.Goad,
            },
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 60; i++) events.AddRange(sim.Tick(NoCommands));

        var fired = Assert.Single(events.OfType<EnemyTorpedoFired>());
        Assert.Equal("GOAD", fired.Personality);
    }

    [Fact]
    public void AnEnemyWithoutPersonalityReportsNone()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            new Contact
            {
                Id = new ContactId(1), Kind = ContactKind.Enemy,
                Position = new Vec2(0.0, -0.90), Velocity = Vec2.Zero,
                Alive = true, NextFireTick = 0,
            },
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 60; i++) events.AddRange(sim.Tick(NoCommands));

        var fired = Assert.Single(events.OfType<EnemyTorpedoFired>());
        Assert.Null(fired.Personality);
    }
}
```

- [ ] **Step 2: Correrlo y verlo fallar**

Run: `dotnet test tests/Sonar.Core.Tests --filter "FullyQualifiedName~PersonalityTelemetryTests"`
Expected: FAIL a la compilación — `EnemyTorpedoFired` no tiene `Personality`.

- [ ] **Step 3: Agregar la columna a los eventos**

En `src/Sonar.Core/Events/GameEvent.cs`:

```csharp
public sealed record EnemyTorpedoFired(int Tick, int TorpedoId, int SourceContactId,
    string? Personality = null) : GameEvent(Tick);
```

Y a `ContactDestroyed`, agregar `string? Personality = null` como último parámetro opcional.
El valor por defecto mantiene compilando todos los sitios de construcción existentes.

- [ ] **Step 4: Rellenarla**

En `FireEnemyTorpedoes`:

```csharp
            events.Add(new EnemyTorpedoFired(state.Tick, id, contact.Id.Value,
                contact.Personality?.Name));
```

En `ResolveDirectHits`, al construir `ContactDestroyed`, agregar `contact.Personality?.Name` como último argumento. Lo mismo en cualquier otro sitio donde se emita `ContactDestroyed` — buscarlos con:

Run: `grep -n "new ContactDestroyed" src/Sonar.Core/Simulation.cs`

- [ ] **Step 5: Escribirla en el JSONL**

En `src/Sonar.Game/Telemetry/SessionRecorder.cs`, en los casos correspondientes:

```csharp
            case ContactDestroyed e:
                _writer.Write(new JsonLine("kill")
                    .Add("tick", e.Tick)
                    .Add("contact", e.ContactId)
                    .Add("cause", e.Cause.ToString())
                    .Add("personality", e.Personality ?? "none"));
                break;
```

y agregar el caso de disparo enemigo si no existe:

```csharp
            case EnemyTorpedoFired e:
                _writer.Write(new JsonLine("enemy_fire")
                    .Add("tick", e.Tick)
                    .Add("torpedo", e.TorpedoId)
                    .Add("contact", e.SourceContactId)
                    .Add("personality", e.Personality ?? "none"));
                break;
```

- [ ] **Step 6: Correr todo**

Run: `dotnet test && dotnet build src/Sonar.Game -v q --nologo`
Expected: PASS y build limpio.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(telemetry): tag every enemy line with the personality behind it"
```

---

### Task 6: Los contadores que dicen si el diseño funciona

**Files:**
- Modify: `src/Sonar.Game/Telemetry/SessionRecorder.cs`
- Modify: `src/Sonar.Game/Main.cs`

- [ ] **Step 1: Registrar el fuego contenido**

En `SessionRecorder`, agregar al `switch`:

```csharp
            case FireHeldForFriendly e:
                _writer.Write(new JsonLine("held_fire")
                    .Add("tick", e.Tick)
                    .Add("contact", e.ContactId)
                    .Add("blocked_by", e.BlockedByContactId));
                break;

            case RammingRunDetected e:
                _writer.Write(new JsonLine("ram_closing")
                    .Add("tick", e.Tick)
                    .Add("contact", e.ContactId)
                    .Add("range", e.Distance));
                break;
```

`held_fire` es el que dice si el §7 del spec funciona. Es invisible en pantalla: sin medirlo
no hay forma de saber si los enemigos se coordinan o si simplemente dejaron de disparar. Se
emite muestreado cada 60 ticks, así que es un contador **relativo**, no exacto — sirve para
comparar oleadas y sesiones, no para contar ocasiones.

- [ ] **Step 2: Registrar los torpedos propios perdidos por choque**

`EnemyTorpedoDestroyed` con causa `TorpedoCollision` ya se emite junto a un `TorpedoDetonated`
propio. Para poder contar cuántos torpedos del jugador mueren así, agregar:

```csharp
            case EnemyTorpedoDestroyed e when e.Cause == DestructionCause.TorpedoCollision:
                _writer.Write(new JsonLine("torpedo_collision")
                    .Add("tick", e.Tick)
                    .Add("torpedo", e.TorpedoId));
                break;
```

- [ ] **Step 3: Registrar el silencio**

En `src/Sonar.Game/Main.cs`, donde ya se atiende `SonarToggled`, agregar la escritura:

```csharp
            case SonarToggled e:
                Instrument(e.Running ? "SONAR RUNNING" : "SONAR STOPPED");
                _recorder.SonarToggled(e.Tick, e.Running);
                break;
```

Y en `SessionRecorder`:

```csharp
    /// <summary>
    /// Cuándo se apaga y se enciende el barrido. Con el modelo de ruido, apagarlo es una
    /// decisión táctica: responde si alguien descubrió esconderse y si le sirvió.
    /// </summary>
    public void SonarToggled(int tick, bool running) =>
        _writer.Write(new JsonLine("sonar").Add("tick", tick).Add("running", running));
```

- [ ] **Step 4: Verificar que se escribe de verdad**

Run: `dotnet build src/Sonar.Game -v q --nologo && timeout 180 godot-mono --headless --path src/Sonar.Game -- --m1-selftest`
Expected: `[SELFTEST] PASA`

Run: `grep -c "held_fire\|enemy_fire\|personality" ~/.local/share/godot/app_userdata/Sonar/m1-session.jsonl`
Expected: mayor que cero. Si es cero, el selftest no llega a la primera oleada — correr el juego con ventana unos minutos y volver a mirar.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(telemetry): count held fire, ram runs and time spent silent"
```

---

### Task 7: Poner al día lo que quedó desactualizado

**Files:**
- Modify: `docs/tdd/M1-core-simulation.md`
- Modify: `docs/development/godot-wiring.md`
- Modify: `project-state.yaml`

- [ ] **Step 1: Revertir el §4.5 del TDD**

Buscar el bloque que dice *«Un torpedo enemigo no daña a los contactos — ni fuego amigo entre
enemigos ni choque con pecios»* y reemplazarlo por:

```markdown
`[DECIDED]` **Un torpedo enemigo sí daña a otros enemigos, y no a los pecios.** Esta decisión
revierte la anterior, que lo prohibía por completo; el spec del
[2026-08-15](../superpowers/specs/2026-08-15-enemy-ai-design.md) §7 explica por qué.

Lo raro no sale de un dado: los enemigos **no disparan a través de un compañero**, y el
chequeo de carril proyecta en línea recta mientras los cascos viran hacia su radio preferido.
El accidente ocurre cuando esa predicción falla.

Los pecios siguen siendo inmunes: la anomalía de defensas del §25 necesita que el pecio
sobreviva como referencia observable.
```

- [ ] **Step 2: Poner al día el cableado de Godot**

En `docs/development/godot-wiring.md`, actualizar la descripción del HUD de munición
(`tubes ***  reserve 12`, del plan 7) y agregar los dos eventos nuevos —`FireHeldForFriendly`
y `RammingRunDetected`— a la lista de lo que la escena consume.

- [ ] **Step 3: Poner al día el estado del proyecto**

En `project-state.yaml`:

```yaml
  plan_8_held_fire_and_telemetry: done   # 8/8 tareas
```

y en `systems:`, cambiar `enemy_threat` a
`done  # cinco personalidades, puntería por ruido, fuego amigo evitado`.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "docs: reverse the friendly-fire rule and record why"
```

---

### Task 8: Cerrar el plan

- [ ] **Step 1: Verificación completa**

```bash
dotnet build -v q --nologo
dotnet test
dotnet build src/Sonar.Game -v q --nologo
timeout 180 godot-mono --headless --path src/Sonar.Game -- --m1-selftest
grep -rn "using Godot" src/Sonar.Core src/Sonar.Controller --include=*.cs | grep -v "/obj/\|/bin/" | wc -l
grep -rnE "\bDictionary<|\bHashSet<" src/Sonar.Core --include=*.cs | grep -v "/obj/\|/bin/" | wc -l
```
Expected: build limpio, toda la suite verde, selftest verde, **0** en los dos greps.

- [ ] **Step 2: Jugar una partida de verdad, con ventana**

Run: `godot-mono --path src/Sonar.Game`

Mirar tres cosas que ningún test puede ver:

1. ¿Se distinguen las personalidades mirando el scope? Si un PICKET y un STALKER se ven
   iguales, el diseño falló aunque los tests pasen.
2. ¿El aviso del RAM llega con tiempo suficiente para reaccionar?
3. ¿Los enemigos se apiñan visiblemente sobre agua vacía al apagar el sonar?

- [ ] **Step 3: Contrastar contra el `[RISK]` de letalidad del spec §12**

Del JSONL de esa partida, calcular la tasa de acierto enemiga:

```bash
python3 - <<'PY'
import json, collections
path = __import__('os').path.expanduser('~/.local/share/godot/app_userdata/Sonar/m1-session.jsonl')
counts = collections.Counter()
for line in open(path):
    try: row = json.loads(line)
    except ValueError: continue
    counts[row.get('type')] += 1
fired, hits = counts['enemy_fire'], counts['sub_hit']
print(f"disparos enemigos: {fired}  impactos: {hits}")
print(f"tasa de acierto: {hits/fired:.0%}" if fired else "sin disparos")
print(f"fuego contenido (muestreado): {counts['held_fire']}")
PY
```

El spec predice **alrededor de un 50%** cuando el jugador suena. Anotar el número real.

- [ ] **Step 4: Anotar los hallazgos**

Agregar la entrada a `docs/development/m1-findings.md` con la tasa medida, si las
personalidades se distinguieron a ojo, y cuántas veces se contuvo el fuego. Si la tasa quedó
muy lejos del 50%, **no tocar el código: anotar el número y proponer el ajuste de
`hitRadius`**, que es la perilla que el spec dejó para eso.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "docs: close plan 8 with the measured enemy hit rate"
```

---

### Task 9: Conectar el remoto de GitHub

Lo pidió el jugador explícitamente. Va al final porque publica el repositorio entero.

- [ ] **Step 1: Confirmar antes de publicar**

Preguntar al jugador antes de ejecutar nada. `git push` **publica el código, los specs y los
documentos de diseño**; si el repositorio de GitHub es público, quedan visibles para
cualquiera. Es una acción difícil de revertir.

- [ ] **Step 2: Resolver el conflicto de ramas**

El jugador dio estos comandos:

```
git remote add origin https://github.com/clemgrass/SONAR.git
git branch -M main
git push -u origin main
```

**`git branch -M main` es destructivo acá.** El trabajo va en `m1-validation` y ya existe una
rama `main`: `-M` fuerza el renombrado y **la `main` que existe se pierde**. Verificar primero:

```bash
git branch -vv
git log --oneline main..m1-validation | wc -l
git log --oneline m1-validation..main | wc -l
```

Si la segunda cuenta es 0 —`main` no tiene nada que `m1-validation` no tenga—, lo correcto es
fusionar en vez de renombrar:

```bash
git remote add origin https://github.com/clemgrass/SONAR.git
git checkout main
git merge --ff-only m1-validation
git push -u origin main
```

Si `main` sí tiene commits propios, **parar y preguntarle al jugador** qué quiere conservar.

- [ ] **Step 3: Verificar**

```bash
git remote -v
git log --oneline -1 origin/main
```
Expected: el remoto apunta a `clemgrass/SONAR` y el último commit coincide con el local.

---

## Nota sobre el orden

Las tareas 1 a 4 cambian reglas de simulación y cada una deja el juego jugable. Las 5 y 6 son
telemetría y no cambian comportamiento. La 7 es documentación. La 8 es la única que requiere
jugar a mano, y es la que puede mandar a ajustar `hitRadius` — por eso va después de todo lo
demás y no en medio.
