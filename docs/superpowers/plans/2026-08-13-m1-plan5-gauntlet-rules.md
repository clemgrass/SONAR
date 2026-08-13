# Plan 5 — Reglas del gauntlet (Core + Controller)

> **Para agentes:** SUB-SKILL REQUERIDA: usa `superpowers:subagent-driven-development` o
> `superpowers:executing-plans` para ejecutar este plan tarea por tarea. Los pasos usan
> checkbox (`- [ ]`) para seguimiento.

**Goal:** Convertir la demo en un gauntlet de oleadas dobladoras con muerte por colisión,
RETRY y medición de precisión de disparo, todo testeable sin abrir Godot.

**Architecture:** Las reglas nuevas (colisión, vuelta de barrido, evaluación de disparo) van a
`Sonar.Core` porque operan sobre la verdad. El director de oleadas va a `Sonar.Controller`
porque es guion de misión, no regla: la simulación recibe contactos y no se entera de que
existen oleadas. `RETRY` reconstruye la `Simulation` desde el Controller, porque Core se
congela al morir el submarino y no podría procesar el comando.

**Tech Stack:** C# / .NET, TFM `net9.0`, xUnit. Correr todo con `dotnet test` desde la raíz.

**Spec:** [2026-08-13](../specs/2026-08-13-m1-gauntlet-and-telemetry-design.md)

---

## Reglas del repositorio

Antes de tocar código, lee
[docs/development/AGENT_CONTRACT.md](../../development/AGENT_CONTRACT.md). Lo esencial:

- **Rojo antes que verde.** Escribe el test, córrelo, míralo fallar, y recién implementa.
- **Verificación por mutación.** Toda propiedad que sostenga el diseño se rompe a propósito
  para confirmar que los tests la pinchan. Restaurar después.
- Dentro de `src/Sonar.Core/` **no hay `Dictionary` ni `HashSet`**, y nada de `System.Random`.
- Ni Core ni Controller referencian Godot.
- Comentarios y documentación en español; los strings del juego en inglés.

Estado de partida: **252 tests pasando**, build sin advertencias.

---

## Estructura de archivos

| Archivo | Responsabilidad |
|---|---|
| `src/Sonar.Core/Events/GameEvent.cs` | +`SweepCompleted`, +`ShotAssessed`, `SubDestroyed` gana causa |
| `src/Sonar.Core/Spatial/Bearing.cs` | +`DegreesOf(Vec2)` y `Separation(a,b)` en doble precisión |
| `src/Sonar.Core/Sensing/SonarState.cs` | +`Revolution` |
| `src/Sonar.Core/Simulation.cs` | Colisión, vuelta de barrido, evaluación de disparo, `AddContacts` |
| `src/Sonar.Core/Commands/Command.cs` | +`RetryCommand` |
| `src/Sonar.Controller/WaveDirector.cs` | **nuevo** — oleadas, spawns de borde, velocidad de cierre |
| `src/Sonar.Controller/GameController.cs` | Integra el director, implementa `Retry` |
| `src/Sonar.Controller/CommandParser.cs` | Acepta `RETRY` |
| `src/Sonar.Controller/OpeningScenario.cs` | **se elimina** en la tarea 8 |
| `tests/Sonar.Controller.Tests/GauntletTests.cs` | **nuevo** — reemplaza `OpeningScenarioTests` |

---

## Task 1: Marcación en doble precisión

`Bearing.FromVector` redondea a grados enteros. Para medir error de disparo eso pierde
justamente la precisión que se quiere medir.

**Files:**
- Modify: `src/Sonar.Core/Spatial/Bearing.cs`
- Test: `tests/Sonar.Core.Tests/Spatial/BearingTests.cs`

- [ ] **Step 1: Escribir los tests que fallan**

Agrega al final de la clase `BearingTests` en
`tests/Sonar.Core.Tests/Spatial/BearingTests.cs`:

```csharp
    [Fact]
    public void DegreesOf_KeepsFractionsThatFromVectorRoundsAway()
    {
        // Un contacto a 0.5 grados al este del norte. FromVector devolvería 000 o 001;
        // para medir error de puntería hace falta el valor sin redondear.
        var v = new Vec2(System.Math.Sin(0.5 * System.Math.PI / 180.0),
                        -System.Math.Cos(0.5 * System.Math.PI / 180.0));

        Assert.Equal(0.5, Bearing.DegreesOf(v), precision: 6);
    }

    [Fact]
    public void DegreesOf_IsAlwaysInRange()
    {
        Assert.Equal(90.0, Bearing.DegreesOf(new Vec2(1, 0)), precision: 6);
        Assert.Equal(180.0, Bearing.DegreesOf(new Vec2(0, 1)), precision: 6);
        Assert.Equal(270.0, Bearing.DegreesOf(new Vec2(-1, 0)), precision: 6);
        Assert.Equal(0.0, Bearing.DegreesOf(new Vec2(0, -1)), precision: 6);
        Assert.Equal(0.0, Bearing.DegreesOf(Vec2.Zero), precision: 6);
    }

    [Fact]
    public void Separation_TakesTheShortWayAround()
    {
        Assert.Equal(10.0, Bearing.Separation(5.0, 355.0), precision: 6);
        Assert.Equal(10.0, Bearing.Separation(355.0, 5.0), precision: 6);
        Assert.Equal(180.0, Bearing.Separation(0.0, 180.0), precision: 6);
        Assert.Equal(0.0, Bearing.Separation(47.0, 47.0), precision: 6);
    }
```

- [ ] **Step 2: Correr para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `'Bearing' does not contain a definition for 'DegreesOf'`.

- [ ] **Step 3: Implementar**

En `src/Sonar.Core/Spatial/Bearing.cs`, agrega dentro del struct, después de `FromVector`:

```csharp
    /// <summary>
    /// Marcación exacta del vector, sin redondear a grados enteros. Existe para medir error
    /// de puntería: FromVector redondea, y redondear el error a un grado borra buena parte
    /// de lo que se quiere observar.
    /// </summary>
    public static double DegreesOf(Vec2 v)
    {
        if (v.X == 0 && v.Y == 0) return 0.0;
        var degrees = System.Math.Atan2(v.X, -v.Y) * 180.0 / System.Math.PI;
        return (degrees + 360.0) % 360.0;
    }

    /// <summary>Diferencia angular por el camino corto, en [0, 180].</summary>
    public static double Separation(double a, double b)
    {
        var d = System.Math.Abs(a - b) % 360.0;
        return d > 180.0 ? 360.0 - d : d;
    }
```

- [ ] **Step 4: Correr para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 255` en `Sonar.Core.Tests`.

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/Spatial/Bearing.cs tests/Sonar.Core.Tests/Spatial/BearingTests.cs
git commit -m "feat(core): add double-precision bearing and angular separation"
```

---

## Task 2: Vuelta de barrido

**Files:**
- Modify: `src/Sonar.Core/Sensing/SonarState.cs`
- Modify: `src/Sonar.Core/Events/GameEvent.cs`
- Modify: `src/Sonar.Core/Simulation.cs`
- Test: `tests/Sonar.Core.Tests/Sensing/SweepRevolutionTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Sensing/SweepRevolutionTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sonar.Core;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Sensing;

public class SweepRevolutionTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static int TicksPerSweep(SimulationConfig config) =>
        (int)Math.Round(config.SweepPeriodSeconds / config.TickDurationSeconds);

    [Fact]
    public void OneEventPerRevolution()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);

        var events = new List<GameEvent>();
        for (var i = 0; i < TicksPerSweep(config) * 3; i++) events.AddRange(sim.Tick(NoCommands));

        var laps = events.OfType<SweepCompleted>().ToArray();
        Assert.Equal(3, laps.Length);
        Assert.Equal(new[] { 1, 2, 3 }, laps.Select(l => l.Revolution).ToArray());
    }

    [Fact]
    public void CarriesHowManyContactsAreOnTheBoard()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[]
        {
            Contact.CreateInert(new ContactId(1), new Vec2(0.5, 0.0)),
            Contact.CreateInert(new ContactId(2), new Vec2(0.0, 0.5)),
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < TicksPerSweep(config) * 2; i++) events.AddRange(sim.Tick(NoCommands));

        // A la segunda vuelta ya barrió a los dos.
        Assert.Equal(2, events.OfType<SweepCompleted>().Last().ReadingCount);
    }

    [Fact]
    public void StoppedSweep_CompletesNoRevolutions()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.Tick(new Command[] { new ToggleSonarCommand() });

        var events = new List<GameEvent>();
        for (var i = 0; i < TicksPerSweep(config) * 3; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.Empty(events.OfType<SweepCompleted>());

        // Control positivo: con el haz andando los mismos ticks sí dan vueltas. Sin esto el
        // test pasaría igual si el evento no se emitiera nunca.
        var running = TestWorld.Still(config);
        var runningEvents = new List<GameEvent>();
        for (var i = 0; i < TicksPerSweep(config) * 3; i++)
            runningEvents.AddRange(running.Tick(NoCommands));

        Assert.NotEmpty(runningEvents.OfType<SweepCompleted>());
    }
}
```

- [ ] **Step 2: Correr para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'SweepCompleted' could not be found`.

- [ ] **Step 3: Agregar el evento**

En `src/Sonar.Core/Events/GameEvent.cs`, después de la sección `// --- Sistemas ---`:

```csharp
/// <summary>
/// El haz completó una vuelta. No cambia ningún estado ni habilita ninguna acción: es un
/// latido, para que el silencio tenga una unidad contable y "hace dos barridos" sea una
/// frase que el jugador pueda pensar.
/// </summary>
public sealed record SweepCompleted(int Tick, int Revolution, int ReadingCount) : GameEvent(Tick);
```

- [ ] **Step 4: Agregar el contador a `SonarState`**

En `src/Sonar.Core/Sensing/SonarState.cs`, agrega la propiedad y el valor inicial:

```csharp
    /// <summary>Vueltas completas del haz desde el arranque.</summary>
    public required int Revolution { get; init; }
```

Y dentro de `CreateInitial()`, después de `SweepDegrees = 0,`:

```csharp
        Revolution = 0,
```

- [ ] **Step 5: Emitir el evento**

En `src/Sonar.Core/Simulation.cs`, cambia la firma de `AdvanceSweepAndRecord` para que reciba
la lista de eventos:

```csharp
    private Sensing.SonarState AdvanceSweepAndRecord(
        Sensing.SonarState sonar, List<Contact> contacts, int tick, double dt,
        List<GameEvent> events)
```

Y reemplaza el `return` final del método por:

```csharp
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
```

Y en `Tick`, el paso 10 pasa a ser:

```csharp
        // 10 · Barrido y registro de lecturas
        var sonar = AdvanceSweepAndRecord(state.Sonar, contacts, state.Tick, dt, events);
```

- [ ] **Step 6: Correr para verificar que pasan**

Run: `dotnet test`
Expected: `Failed: 0`. El total sube en 3.

- [ ] **Step 7: Verificación por mutación**

Cambia `current < previous` por `false`, corre `dotnet test`, y confirma que fallan
`OneEventPerRevolution` y `CarriesHowManyContactsAreOnTheBoard`. **Restaura** y confirma
verde. Si alguna mutación da 0 fallos, repórtalo en vez de seguir.

- [ ] **Step 8: Commit**

```bash
git add src/Sonar.Core tests/Sonar.Core.Tests/Sensing/SweepRevolutionTests.cs
git commit -m "feat(core): emit a sweep-completed heartbeat once per revolution"
```

---

## Task 3: Muerte por colisión

**Files:**
- Modify: `src/Sonar.Core/Events/GameEvent.cs`
- Modify: `src/Sonar.Core/Simulation.cs`
- Test: `tests/Sonar.Core.Tests/Combat/CollisionTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Combat/CollisionTests.cs`:

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

public class CollisionTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Contact ClosingEnemy(int id) => new()
    {
        Id = new ContactId(id),
        Kind = ContactKind.Enemy,
        Position = new Vec2(0.30, 0.0),
        Velocity = new Vec2(-0.30, 0.0),
        Alive = true,
        NextFireTick = int.MaxValue,
    };

    [Fact]
    public void EnemyReachingTheSub_KillsItOutright()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { ClosingEnemy(1) });

        var events = new List<GameEvent>();
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        var dead = (SubDestroyed)Assert.Single(events, e => e is SubDestroyed);
        Assert.Equal(SubDeathCause.Collision, dead.Cause);
        Assert.True(sim.State.Sub.Destroyed);

        // De una: no hacen falta dos impactos.
        Assert.Empty(events.OfType<SubHit>());
    }

    [Fact]
    public void InertContact_PassesThroughWithoutKilling()
    {
        // Un pecio ya está hundido. Misma lógica que la exención en las defensas.
        var sim = TestWorld.SteamingNorth(speed: 0.30);
        sim.SetContacts(new[] { Contact.CreateInert(new ContactId(1), new Vec2(0.0, -0.30)) });

        var events = new List<GameEvent>();
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        Assert.DoesNotContain(events, e => e is SubDestroyed);
        Assert.False(sim.State.Sub.Destroyed);

        // Control positivo: pasó de verdad por encima del submarino.
        Assert.True(sim.State.Contacts[0].Position.Y > 0.0,
            "El pecio no llegó a cruzar el origen; el test no probó nada.");
    }

    [Fact]
    public void TorpedoDeath_StillReportsItsOwnCause()
    {
        var config = TestWorld.Config() with { ReloadTicks = 30 };
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[]
        {
            new Contact
            {
                Id = new ContactId(1), Kind = ContactKind.Enemy,
                Position = new Vec2(0.0, -0.30), Velocity = Vec2.Zero,
                Alive = true, NextFireTick = 0,
            },
        });

        var events = new List<GameEvent>();
        for (var i = 0; i < 400; i++) events.AddRange(sim.Tick(NoCommands));

        var dead = (SubDestroyed)Assert.Single(events, e => e is SubDestroyed);
        Assert.Equal(SubDeathCause.Torpedoes, dead.Cause);
    }
}
```

- [ ] **Step 2: Correr para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'SubDeathCause' could not be found`.

- [ ] **Step 3: Dar causa a `SubDestroyed`**

En `src/Sonar.Core/Events/GameEvent.cs`, reemplaza la línea de `SubDestroyed`:

```csharp
public sealed record SubDestroyed(int Tick, SubDeathCause Cause) : GameEvent(Tick);
```

Y agrega el enum junto a los otros, al final del archivo:

```csharp
/// <summary>
/// Cómo terminó la corrida. Morir por descuido de rumbo y morir bajo fuego son dos lecciones
/// distintas, y la telemetría necesita poder separarlas.
/// </summary>
public enum SubDeathCause { Torpedoes, Collision }
```

- [ ] **Step 4: Actualizar el sitio que ya lo emitía**

En `src/Sonar.Core/Simulation.cs`, dentro de `ResolveSubHits`:

```csharp
                events.Add(new SubDestroyed(tick, SubDeathCause.Torpedoes));
```

- [ ] **Step 5: Implementar la colisión**

En `src/Sonar.Core/Simulation.cs`, agrega el método después de `ResolveSubHits`:

```csharp
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
```

Y en `Tick`, justo después del paso 9:

```csharp
        // 9b · Embestidas. Va después de los torpedos para que, si en el mismo tick te
        //      llegan las dos cosas, la causa reportada sea la que ya te había matado.
        sub = ResolveContactCollisions(sub, contacts, events, state.Tick);
```

- [ ] **Step 6: Correr para verificar que pasan**

Run: `dotnet test`
Expected: `Failed: 0`.

- [ ] **Step 7: Verificación por mutación**

Vacía el cuerpo de `ResolveContactCollisions` para que sólo haga `return sub;`. Corre
`dotnet test` y confirma que falla `EnemyReachingTheSub_KillsItOutright`. **Restaura.**

- [ ] **Step 8: Commit**

```bash
git add src/Sonar.Core tests/Sonar.Core.Tests/Combat/CollisionTests.cs
git commit -m "feat(core): a contact that reaches the sub is an instant kill"
```

---

## Task 4: Evaluación de disparo

La métrica que responde la pregunta 1 de M1. Se calcula en Core porque necesita la posición
**real** del contacto, que el snapshot esconde a propósito.

**Files:**
- Modify: `src/Sonar.Core/Events/GameEvent.cs`
- Modify: `src/Sonar.Core/Simulation.cs`
- Test: `tests/Sonar.Core.Tests/Combat/ShotAssessmentTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Combat/ShotAssessmentTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sonar.Core;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Combat;

public class ShotAssessmentTests
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

    private static int TicksPerSweep(SimulationConfig config) =>
        (int)Math.Round(config.SweepPeriodSeconds / config.TickDurationSeconds);

    [Fact]
    public void MeasuresAngularErrorAgainstTheNearestContact()
    {
        var sim = TestWorld.Still();
        // Contacto exactamente en marcación 090, banda C.
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.55, 0.0)) });

        var events = new List<GameEvent>(sim.Tick(Fire(100, 2)));

        var shot = (ShotAssessed)Assert.Single(events, e => e is ShotAssessed);
        Assert.Equal(1, shot.NearestContactId);
        Assert.Equal(10.0, shot.ErrorDegrees, precision: 6);
        Assert.Equal(0.0, shot.ErrorRadius, precision: 6);
    }

    [Fact]
    public void MeasuresRadialErrorAgainstTheBandCentre()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        // Contacto en marcación 090 a radio 0.70. El centro de la banda C está en 0.55.
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.70, 0.0)) });

        var events = new List<GameEvent>(sim.Tick(Fire(90, 2)));

        var shot = (ShotAssessed)Assert.Single(events, e => e is ShotAssessed);
        Assert.Equal(0.0, shot.ErrorDegrees, precision: 6);
        Assert.Equal(0.15, shot.ErrorRadius, precision: 6);
    }

    [Fact]
    public void PicksTheContactClosestInBearing()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            EnemyAt(1, new Vec2(0.0, -0.55)),   // marcación 000
            EnemyAt(2, new Vec2(0.55, 0.0)),    // marcación 090
        });

        var events = new List<GameEvent>(sim.Tick(Fire(80, 2)));

        var shot = (ShotAssessed)Assert.Single(events, e => e is ShotAssessed);
        Assert.Equal(2, shot.NearestContactId);
        Assert.Equal(10.0, shot.ErrorDegrees, precision: 6);
    }

    [Fact]
    public void CarriesTheAgeOfTheReadingTheShooterCouldSee()
    {
        // La edad de la lectura es la otra mitad de la curva de extrapolación: sin ella no
        // se puede preguntar si la precisión se degrada conforme la marca envejece.
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.55, 0.0)) });

        // Deja pasar una vuelta larga para que la lectura tenga edad medible.
        for (var i = 0; i < TicksPerSweep(config) + 60; i++) sim.Tick(NoCommands);
        Assert.Single(sim.State.Sonar.Readings);

        var events = new List<GameEvent>(sim.Tick(Fire(90, 2)));
        var shot = (ShotAssessed)Assert.Single(events, e => e is ShotAssessed);

        Assert.True(shot.ReadingAgeTicks > 0,
            $"La edad de lectura no se registró: {shot.ReadingAgeTicks}");
        Assert.True(shot.ReadingAgeTicks < TicksPerSweep(config),
            "La edad no puede superar una vuelta del haz.");
    }

    [Fact]
    public void NeverReadContact_ReportsAgeMinusOne()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.55, 0.0)) });

        // Tick 0: el haz todavía no barrió nada.
        var events = new List<GameEvent>(sim.Tick(Fire(90, 2)));

        var shot = (ShotAssessed)Assert.Single(events, e => e is ShotAssessed);
        Assert.Equal(-1, shot.ReadingAgeTicks);
    }

    [Fact]
    public void NoContacts_NoAssessment()
    {
        // Disparar al vacío no tiene contra qué medirse. Se omite en vez de inventar un
        // error sentinela que después contamine los promedios.
        var sim = TestWorld.Still();

        var events = new List<GameEvent>(sim.Tick(Fire(90, 2)));

        Assert.Contains(events, e => e is TorpedoFired);
        Assert.DoesNotContain(events, e => e is ShotAssessed);
    }

    [Fact]
    public void RejectedShot_IsNotAssessed()
    {
        var config = TestWorld.Config() with { MagazineSize = 1 };
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.55, 0.0)) });

        sim.Tick(Fire(90, 2));
        var events = new List<GameEvent>(sim.Tick(Fire(90, 2)));

        Assert.Contains(events, e => e is FireRejected);
        Assert.DoesNotContain(events, e => e is ShotAssessed);
    }
}
```

- [ ] **Step 2: Correr para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'ShotAssessed' could not be found`.

- [ ] **Step 3: Agregar el evento**

En `src/Sonar.Core/Events/GameEvent.cs`, después de `NearMiss`:

```csharp
/// <summary>
/// Medición del disparo en el instante de lanzarlo, contra la verdad del mundo.
/// **Sólo telemetría: no se muestra al jugador.** Mostrarlo le entregaría gratis la
/// corrección que el juego le pide calcular.
///
/// `ErrorDegrees` es la separación angular al contacto vivo más cercano en marcación;
/// `ErrorRadius`, la diferencia entre el centro de la banda pedida y el radio real de ese
/// contacto; `ReadingAgeTicks`, cuán vieja era la lectura que el jugador podía ver, o -1 si
/// ese contacto todavía no había sido barrido nunca.
///
/// Juntos dan la curva de extrapolación, que es la respuesta a la pregunta 1 de M1.
/// </summary>
public sealed record ShotAssessed(
    int Tick, int TorpedoId, int NearestContactId,
    double ErrorDegrees, double ErrorRadius, int ReadingAgeTicks) : GameEvent(Tick);
```

- [ ] **Step 4: Implementar**

En `src/Sonar.Core/Simulation.cs`, agrega el método privado justo antes de `ApplyCommand`:

```csharp
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
```

Y en `ApplyCommand`, dentro del `case FireCommand fire:`, justo después de la línea que agrega
`TorpedoFired`:

```csharp
                AssessShot(state, fire.Target, id, events);
```

- [ ] **Step 5: Correr para verificar que pasan**

Run: `dotnet test`
Expected: `Failed: 0`.

- [ ] **Step 6: Verificación por mutación**

Reemplaza `bestSeparation` en el `events.Add` por la constante `0.0`. Corre `dotnet test` y
confirma que fallan `MeasuresAngularErrorAgainstTheNearestContact` y
`PicksTheContactClosestInBearing`. **Restaura.**

Luego cambia `age = state.Tick - reading.ObservedAtTick;` por `age = 0;` y confirma que falla
`CarriesTheAgeOfTheReadingTheShooterCouldSee`. **Restaura** y confirma verde.

- [ ] **Step 7: Commit**

```bash
git add src/Sonar.Core tests/Sonar.Core.Tests/Combat/ShotAssessmentTests.cs
git commit -m "feat(core): measure shot error against reading age for telemetry"
```

---

## Task 5: `AddContacts` y comando `RETRY`

Dos piezas chicas que las tareas siguientes necesitan.

**Files:**
- Modify: `src/Sonar.Core/Simulation.cs`
- Modify: `src/Sonar.Core/Commands/Command.cs`
- Modify: `src/Sonar.Controller/CommandParser.cs`
- Test: `tests/Sonar.Core.Tests/AddContactsTests.cs`
- Test: `tests/Sonar.Controller.Tests/CommandParserTests.cs`

- [ ] **Step 1: Escribir los tests que fallan**

Crea `tests/Sonar.Core.Tests/AddContactsTests.cs`:

```csharp
using System.Linq;
using Sonar.Core.Entities;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests;

public class AddContactsTests
{
    [Fact]
    public void AddContacts_KeepsTheOnesAlreadyThere()
    {
        // SetContacts reemplaza; el director de oleadas necesita sumar sin borrar.
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { Contact.CreateInert(new ContactId(1), new Vec2(0.5, 0.0)) });

        sim.AddContacts(new[] { Contact.CreateInert(new ContactId(2), new Vec2(0.0, 0.5)) });

        Assert.Equal(new[] { 1, 2 }, sim.State.Contacts.Select(c => c.Id.Value).ToArray());
    }

    [Fact]
    public void AddContacts_KeepsTheListSortedById()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { Contact.CreateInert(new ContactId(9), new Vec2(0.5, 0.0)) });

        sim.AddContacts(new[] { Contact.CreateInert(new ContactId(3), new Vec2(0.0, 0.5)) });

        Assert.Equal(new[] { 3, 9 }, sim.State.Contacts.Select(c => c.Id.Value).ToArray());
    }
}
```

Y agrega a `tests/Sonar.Controller.Tests/CommandParserTests.cs`, dentro de la clase:

```csharp
    [Theory]
    [InlineData("RETRY")]
    [InlineData("retry")]
    [InlineData("  Retry  ")]
    public void ParsesRetry(string input) => Assert.IsType<RetryCommand>(Parser.Parse(input));
```

- [ ] **Step 2: Correr para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `'Simulation' does not contain a definition for 'AddContacts'` y
`The type or namespace name 'RetryCommand' could not be found`.

- [ ] **Step 3: Implementar**

En `src/Sonar.Core/Simulation.cs`, junto a `SetContacts`:

```csharp
    /// <summary>Suma contactos a los que ya están. Lo usa el director de oleadas.</summary>
    public void AddContacts(IEnumerable<Contact> contacts) =>
        State = State.WithContacts(State.Contacts.Concat(contacts));
```

En `src/Sonar.Core/Commands/Command.cs`, junto a los otros comandos:

```csharp
/// <summary>
/// Reinicio puro: no persiste nada (spec §4.4). Lo atiende el Controller y no la simulación,
/// porque con el submarino destruido el bucle se congela y nunca lo consumiría.
/// </summary>
public sealed record RetryCommand : Command;
```

En `src/Sonar.Controller/CommandParser.cs`, agrega al `switch (normalized)`:

```csharp
            case "RETRY": return new RetryCommand();
```

- [ ] **Step 4: Correr para verificar que pasan**

Run: `dotnet test`
Expected: `Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core src/Sonar.Controller tests
git commit -m "feat: add Simulation.AddContacts and the RETRY command"
```

---

## Task 6: `WaveDirector`

**Files:**
- Create: `src/Sonar.Controller/WaveDirector.cs`
- Test: `tests/Sonar.Controller.Tests/WaveDirectorTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Controller.Tests/WaveDirectorTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Sonar.Core;
using Sonar.Core.Entities;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Controller.Tests;

public class WaveDirectorTests
{
    private static SimulationConfig Config() => new()
    {
        TickDurationSeconds = 1.0 / 60.0,
        MaxCatchUpTicks = 5,
        DeadZoneRadius = 0.10,
        BandOuterRadii = new[] { 0.28, 0.46, 0.64, 0.82, 1.00 },
        SweepPeriodSeconds = 7.0,
        TorpedoSpeed = 0.043,
        HitRadius = 0.040,
        BlastRadius = 0.100,
        NearMissRadius = 0.160,
        EnemyTorpedoSpeed = 0.03,
        EnemyFireIntervalTicks = 1500,
        EnemyFireStaggerTicks = 300,
        SubHitRadius = 0.045,
        DefenseRadius = 0.070,
        DefenseCapacity = 1,
        MagazineSize = 3,
        AmmoReserve = 12,
        ReloadTicks = 420,
    };

    private static WaveDirector New(int seed = 1) =>
        new(Config(), new SeededRandomSource(seed), new Bearing(0), subSpeed: 0.012);

    /// Corre el director hasta `ticks`, fingiendo que los contactos se destruyen al instante.
    private static List<(int Tick, IReadOnlyList<Contact> Spawned)> RunClearingInstantly(
        WaveDirector director, int ticks)
    {
        var log = new List<(int, IReadOnlyList<Contact>)>();
        for (var tick = 0; tick < ticks; tick++)
        {
            var spawned = director.Advance(tick, liveEnemies: 0);
            if (spawned.Count > 0) log.Add((tick, spawned));
        }
        return log;
    }

    [Fact]
    public void NothingHappensDuringTheOpeningQuiet()
    {
        var director = New();
        for (var tick = 0; tick < WaveDirector.OpeningQuietTicks; tick++)
            Assert.Empty(director.Advance(tick, liveEnemies: 0));

        Assert.Empty(director.Advance(WaveDirector.OpeningQuietTicks, liveEnemies: 0));
        Assert.Equal(1, director.Wave);
    }

    [Fact]
    public void WavesDouble()
    {
        var log = RunClearingInstantly(New(), 20000);
        Assert.Equal(new[] { 1, 2, 4, 8 }, log.Take(4).Select(e => e.Spawned.Count).ToArray());
    }

    [Fact]
    public void ANewWaveWaitsForTheBoardToClear()
    {
        var director = New();
        for (var tick = 0; tick <= WaveDirector.OpeningQuietTicks; tick++)
            director.Advance(tick, liveEnemies: 0);
        Assert.Equal(1, director.Wave);

        // Con un enemigo vivo no entra nada, por muchos ticks que pasen.
        for (var tick = 0; tick < 5000; tick++)
            Assert.Empty(director.Advance(WaveDirector.OpeningQuietTicks + tick, liveEnemies: 1));

        Assert.Equal(1, director.Wave);
    }

    [Fact]
    public void EverythingEntersFromTheEdge()
    {
        var log = RunClearingInstantly(New(), 20000);
        var all = log.SelectMany(e => e.Spawned).ToArray();

        Assert.NotEmpty(all);
        Assert.All(all, c => Assert.Equal(WaveDirector.SpawnRadius, c.Position.Length, precision: 6));
    }

    [Fact]
    public void ContactsInAWaveAreAngularlySeparated()
    {
        // Sin separación mínima, dos entradas cercanas se leen como una sola marca y la
        // oleada mide menos de lo que dice medir.
        var log = RunClearingInstantly(New(), 20000);

        foreach (var (_, spawned) in log.Where(e => e.Spawned.Count > 1))
        {
            var bearings = spawned.Select(c => Bearing.DegreesOf(c.Position)).ToArray();
            var floor = 360.0 / bearings.Length / 2.0;

            for (var i = 0; i < bearings.Length; i++)
            for (var j = i + 1; j < bearings.Length; j++)
                Assert.True(Bearing.Separation(bearings[i], bearings[j]) >= floor - 1e-6,
                    $"Marcaciones {bearings[i]:F1} y {bearings[j]:F1} más juntas que {floor:F1}°.");
        }
    }

    [Fact]
    public void ContactsCloseOnTheSub()
    {
        var log = RunClearingInstantly(New(), 20000);

        foreach (var contact in log.SelectMany(e => e.Spawned))
        {
            // Velocidad propia más deriva del mundo: el neto tiene que apuntar al origen.
            var net = contact.Velocity + new Vec2(0.0, 0.012);
            var towardOrigin = (Vec2.Zero - contact.Position).Normalized();
            var dot = net.X * towardOrigin.X + net.Y * towardOrigin.Y;

            Assert.True(dot > 0, $"El contacto {contact.Id.Value} no se acerca: dot={dot:F4}.");
        }
    }

    [Fact]
    public void LaterWavesCloseFaster()
    {
        var log = RunClearingInstantly(New(), 20000);

        double NetSpeed(Contact c) => (c.Velocity + new Vec2(0.0, 0.012)).Length;

        var first = NetSpeed(log[0].Spawned[0]);
        var third = NetSpeed(log[2].Spawned[0]);

        Assert.True(third > first * 1.5,
            $"La tercera oleada no cierra más rápido: {first:F4} -> {third:F4}.");
    }

    [Fact]
    public void ContactIdsAreUniqueAcrossWaves()
    {
        var all = RunClearingInstantly(New(), 20000).SelectMany(e => e.Spawned).ToArray();
        var ids = all.Select(c => c.Id.Value).ToArray();

        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void SameSeed_SameSpawns()
    {
        var a = RunClearingInstantly(New(seed: 77), 20000).SelectMany(e => e.Spawned).ToArray();
        var b = RunClearingInstantly(New(seed: 77), 20000).SelectMany(e => e.Spawned).ToArray();

        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentSeed_DifferentSpawns()
    {
        // El primer test del proyecto que puede afirmar esto: hasta ahora nada consumía
        // IRandomSource y forzarlo habría sido inventar aleatoriedad para satisfacer un test.
        var a = RunClearingInstantly(New(seed: 1), 20000).SelectMany(e => e.Spawned).ToArray();
        var b = RunClearingInstantly(New(seed: 2), 20000).SelectMany(e => e.Spawned).ToArray();

        Assert.NotEqual(a, b);
    }
}
```

- [ ] **Step 2: Correr para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'WaveDirector' could not be found`.

- [ ] **Step 3: Implementar**

Crea `src/Sonar.Controller/WaveDirector.cs`:

```csharp
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

    // Velocidad de cierre NETA deseada, ya descontada la deriva del mundo. La oleada 1 casi
    // no se mueve para que un jugador lento igual alcance a disparar.
    private const double ClosingBase = 0.006;
    private const double ClosingStep = 0.007;

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
        return Spawn(1 << (Wave - 1));
    }

    private IReadOnlyList<Contact> Spawn(int count)
    {
        var closing = ClosingBase + (Wave - 1) * ClosingStep;
        var spawned = new List<Contact>(count);

        // Un sector por contacto, con temblor acotado a la mitad central del sector. Eso
        // garantiza una separación mínima de medio sector sin dejar las entradas repartidas
        // de forma perfectamente regular, que se leería como artificial.
        var sector = 360.0 / count;
        var offset = _random.NextDouble() * 360.0;

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
                NextFireTick = _config.EnemyFireIntervalTicks,
            });
        }

        return spawned;
    }
}
```

- [ ] **Step 4: Correr para verificar que pasan**

Run: `dotnet test`
Expected: `Failed: 0`.

- [ ] **Step 5: Verificación por mutación**

Cambia `Velocity = outward * -closing - _worldDrift` por `Velocity = outward * -closing`.
Corre `dotnet test` y confirma que falla `ContactsCloseOnTheSub`. **Restaura.**

Luego cambia `var jitter = (_random.NextDouble() - 0.5) * sector * 0.5;` por
`var jitter = (_random.NextDouble() - 0.5) * sector * 2.0;` y confirma que falla
`ContactsInAWaveAreAngularlySeparated`. **Restaura** y confirma verde.

- [ ] **Step 6: Commit**

```bash
git add src/Sonar.Controller/WaveDirector.cs tests/Sonar.Controller.Tests/WaveDirectorTests.cs
git commit -m "feat(controller): add the doubling wave director with edge spawns"
```

---

## Task 7: Integrar el director y `RETRY` en `GameController`

**Files:**
- Modify: `src/Sonar.Controller/GameController.cs`
- Test: `tests/Sonar.Controller.Tests/GameControllerTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Agrega al final de la clase `GameControllerTests`:

```csharp
    [Fact]
    public void Waves_ArriveThroughTheController()
    {
        var config = Config();
        var controller = New();

        // Antes del silencio inicial no hay nada.
        for (var i = 0; i < WaveDirector.OpeningQuietTicks - 1; i++)
            controller.Advance(config.TickDurationSeconds);
        Assert.Equal(0, controller.Wave);
        Assert.Equal(0, controller.LiveEnemyCount);

        for (var i = 0; i < 10; i++) controller.Advance(config.TickDurationSeconds);

        Assert.Equal(1, controller.Wave);
        Assert.Equal(1, controller.LiveEnemyCount);
    }

    [Fact]
    public void SubmitInput_ReportsWhetherItUnderstood()
    {
        // La telemetría necesita saberlo y la escena no puede deducirlo: el parser vive
        // dentro del controller.
        var controller = New();

        Assert.True(controller.SubmitInput("C047"));
        Assert.False(controller.SubmitInput("hello"));
        Assert.False(controller.SubmitInput("   "));
    }

    [Fact]
    public void Retry_RestartsFromZeroWithADifferentSeed()
    {
        var config = Config();
        var controller = New();

        for (var i = 0; i < 600; i++) controller.Advance(config.TickDurationSeconds);
        var firstSeed = controller.Seed;
        Assert.True(controller.Snapshot.Tick > 0);

        controller.ForceDestroyForTests();
        controller.SubmitInput("RETRY");

        Assert.Equal(0, controller.Snapshot.Tick);
        Assert.False(controller.Snapshot.Destroyed);
        Assert.Equal(0, controller.Wave);
        Assert.Equal(2, controller.Attempt);
        Assert.NotEqual(firstSeed, controller.Seed);
    }

    [Fact]
    public void Retry_IsIgnoredWhileAlive()
    {
        var config = Config();
        var controller = New();
        for (var i = 0; i < 600; i++) controller.Advance(config.TickDurationSeconds);

        var tick = controller.Snapshot.Tick;
        controller.SubmitInput("RETRY");

        Assert.Equal(tick, controller.Snapshot.Tick);
        Assert.Equal(1, controller.Attempt);
    }

    [Fact]
    public void SameBaseSeed_ReproducesTheWholeSession()
    {
        // Semilla base más número de intento: la sesión completa es reproducible aunque los
        // spawns cambien entre intentos.
        static int[] SpawnBearings(GameController c, SimulationConfig cfg)
        {
            for (var i = 0; i < WaveDirector.OpeningQuietTicks + 10; i++)
                c.Advance(cfg.TickDurationSeconds);
            return c.SpawnedBearingsForTests();
        }

        var config = Config();
        Assert.Equal(SpawnBearings(New(), config), SpawnBearings(New(), config));
    }
```

Reemplaza también el helper `New()` de esa clase por:

```csharp
    private static GameController New() =>
        new(Config(), baseSeed: 1, course: new Bearing(0), speed: 0.012);
```

- [ ] **Step 2: Correr para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `'GameController' does not contain a definition for 'Wave'`.

- [ ] **Step 3: Reescribir `GameController`**

Reemplaza el contenido completo de `src/Sonar.Controller/GameController.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sonar.Controller.Presentation;
using Sonar.Core;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;

namespace Sonar.Controller;

/// <summary>
/// Frontera entre la presentación y la simulación. Acumula tiempo real y avanza Core en
/// pasos fijos; Core nunca ve tiempo de pared. También hospeda el director de oleadas y el
/// reinicio, porque ninguno de los dos es una regla del mundo.
/// </summary>
public sealed class GameController
{
    private readonly SimulationConfig _config;
    private readonly BandLayout _layout;
    private readonly CommandParser _parser;
    private readonly Bearing _course;
    private readonly double _speed;
    private readonly int _baseSeed;
    private readonly List<Command> _pending = new();
    private readonly List<int> _spawnedBearings = new();

    private Simulation _simulation = null!;
    private WaveDirector _waves = null!;
    private double _accumulator;

    public WorldSnapshot Snapshot { get; private set; } = null!;

    /// <summary>Eventos producidos por la última llamada a Advance.</summary>
    public IReadOnlyList<GameEvent> LastEvents { get; private set; } = Array.Empty<GameEvent>();

    /// <summary>Intento actual, base 1. RETRY lo incrementa.</summary>
    public int Attempt { get; private set; }

    /// <summary>Semilla del intento actual. Anotarla permite reproducirlo entero.</summary>
    public int Seed { get; private set; }

    public int Wave => _waves.Wave;

    public int LiveEnemyCount =>
        _simulation.State.Contacts.Count(c => c.Alive && c.Kind == ContactKind.Enemy);

    public GameController(SimulationConfig config, int baseSeed, Bearing course, double speed)
    {
        config.Validate();
        _config = config;
        _layout = config.BuildBandLayout();
        _parser = new CommandParser(_layout.Count);
        _course = course;
        _speed = speed;
        _baseSeed = baseSeed;

        StartRun();
    }

    /// <summary>
    /// Arranca un intento. La semilla se deriva de la base y del número de intento, así que
    /// los spawns cambian entre intentos y la sesión completa sigue siendo reproducible con
    /// sólo anotar la semilla base.
    /// </summary>
    private void StartRun()
    {
        Attempt++;
        Seed = _baseSeed + Attempt * 7919;   // primo, para separar bien las secuencias

        _simulation = new Simulation(_config, new SeededRandomSource(Seed),
            WorldState.CreateInitial(_config, _course, _speed));
        _waves = new WaveDirector(_config, new SeededRandomSource(Seed ^ 0x5EED),
            _course, _speed);

        _accumulator = 0;
        _pending.Clear();
        _spawnedBearings.Clear();
        LastEvents = Array.Empty<GameEvent>();
        Snapshot = SnapshotBuilder.Build(_simulation.State, _layout);
    }

    /// <summary>
    /// Encola texto crudo. Se parsea aquí y se aplica en la próxima frontera de tick, nunca
    /// a mitad: es lo que mantiene la simulación reproducible.
    ///
    /// RETRY es la excepción y se atiende de inmediato: con el submarino destruido la
    /// simulación se congela y un comando encolado no se consumiría nunca.
    /// </summary>
    /// <returns>
    /// `true` si el texto correspondía a un comando. Lo devuelve porque la telemetría
    /// necesita saberlo y la escena no puede deducirlo: el parser vive acá dentro.
    /// </returns>
    public bool SubmitInput(string rawText)
    {
        var command = _parser.Parse(rawText);
        if (command is null) return false;

        if (command is RetryCommand)
        {
            if (Snapshot.Destroyed) StartRun();
            return true;
        }

        _pending.Add(command);
        return command is not UnrecognizedInput;
    }

    /// <summary>
    /// Acumula tiempo real y ejecuta los ticks completos que quepan, hasta MaxCatchUpTicks.
    /// El excedente se descarta: sin tope, un congelamiento produce una avalancha que el
    /// jugador percibe como un salto.
    /// </summary>
    public IReadOnlyList<GameEvent> Advance(double deltaSeconds)
    {
        _accumulator += deltaSeconds;

        var events = new List<GameEvent>();
        var executed = 0;

        while (_accumulator >= _config.TickDurationSeconds && executed < _config.MaxCatchUpTicks)
        {
            _accumulator -= _config.TickDurationSeconds;

            var spawned = _waves.Advance(_simulation.State.Tick, LiveEnemyCount);
            if (spawned.Count > 0)
            {
                _simulation.AddContacts(spawned);
                foreach (var contact in spawned)
                    _spawnedBearings.Add((int)Math.Round(Bearing.DegreesOf(contact.Position)));
            }

            var batch = _pending.ToArray();
            _pending.Clear();
            events.AddRange(_simulation.Tick(batch));
            executed++;
        }

        if (_accumulator >= _config.TickDurationSeconds) _accumulator = 0;

        Snapshot = SnapshotBuilder.Build(_simulation.State, _layout);
        LastEvents = events;
        return events;
    }

    // --- Sólo para tests. No los use la presentación. ---

    /// <summary>Marca el submarino como destruido, para poder ejercitar RETRY sin morir.</summary>
    public void ForceDestroyForTests() =>
        Snapshot = Snapshot with { Destroyed = true };

    public int[] SpawnedBearingsForTests() => _spawnedBearings.ToArray();
}
```

- [ ] **Step 4: Correr para verificar que pasan**

Run: `dotnet test`
Expected: `Failed: 0`.

> Si `Waves_ArriveThroughTheController` falla porque `Assert.Single` está mal escrito en el
> test, corrígelo a `Assert.Equal(1, controller.LiveEnemyCount);` y borra la aserción del
> `Assert.Single` — es ruido que no aporta.

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Controller/GameController.cs tests/Sonar.Controller.Tests/GameControllerTests.cs
git commit -m "feat(controller): drive waves and RETRY from the game controller"
```

---

## Task 8: Retirar `OpeningScenario` y migrar sus tests

`OpeningScenarioTests` tiene siete tests y **casi todos siguen valiendo**. Se migran, no se
borran. En particular `ContactsDoNotPileOntoTheSub`: con colisión mortal, ese fallo pasó de
"escenario aburrido" a "muerte instantánea sin haber jugado".

**Files:**
- Delete: `src/Sonar.Controller/OpeningScenario.cs`
- Delete: `tests/Sonar.Controller.Tests/OpeningScenarioTests.cs`
- Create: `tests/Sonar.Controller.Tests/GauntletTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Controller.Tests/GauntletTests.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sonar.Core;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Controller.Tests;

/// <summary>
/// Recorre el gauntlet con la configuración real de disco, igual que lo hace el juego. Es lo
/// más cerca de "el juego funciona" que se puede correr sin abrir Godot.
/// </summary>
public class GauntletTests
{
    private const int BaseSeed = 20260813;
    private const double SubSpeed = 0.012;

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Sonar.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static SimulationConfig WorkingConfig()
    {
        var path = Path.Combine(RepoRoot(), "config", "simulation.working.json");
        Assert.True(File.Exists(path), $"No se encontró {path}");
        return SimulationConfigLoader.LoadFromJson(File.ReadAllText(path));
    }

    private static GameController Start(out SimulationConfig config)
    {
        config = WorkingConfig();
        return new GameController(config, BaseSeed, new Bearing(0), SubSpeed);
    }

    private static List<GameEvent> Run(GameController controller, SimulationConfig config, double seconds)
    {
        var events = new List<GameEvent>();
        var ticks = (int)(seconds / config.TickDurationSeconds);
        for (var i = 0; i < ticks; i++) events.AddRange(controller.Advance(config.TickDurationSeconds));
        return events;
    }

    private static double QuietSeconds(SimulationConfig config) =>
        WaveDirector.OpeningQuietTicks * config.TickDurationSeconds;

    [Fact]
    public void TheOpeningIsSilent()
    {
        var controller = Start(out var config);
        Run(controller, config, QuietSeconds(config) - 1.0);

        Assert.Equal(0, controller.Wave);
        Assert.Equal(0, controller.LiveEnemyCount);
        Assert.False(controller.Snapshot.Destroyed);
    }

    [Fact]
    public void TheFirstWaveIsASingleContact()
    {
        var controller = Start(out var config);
        Run(controller, config, QuietSeconds(config) + 1.0);

        Assert.Equal(1, controller.Wave);
        Assert.Equal(1, controller.LiveEnemyCount);
    }

    [Fact]
    public void TheFirstContactGetsReadWithinASweep()
    {
        var controller = Start(out var config);
        Run(controller, config, QuietSeconds(config) + config.SweepPeriodSeconds + 1.0);

        Assert.Single(controller.Snapshot.Contacts);
    }

    [Fact]
    public void ContactsDoNotPileOntoTheSub()
    {
        // La deriva del mundo se suma a la velocidad de todos los contactos. Con velocidades
        // mal elegidas, todo lo que entra cae encima del submarino en segundos. Con colisión
        // mortal eso ya no es un escenario aburrido: es morir sin haber jugado.
        var controller = Start(out var config);
        Run(controller, config, QuietSeconds(config) + 30.0);

        Assert.False(controller.Snapshot.Destroyed,
            "El submarino murió a los treinta segundos de la primera oleada.");
        Assert.Equal(1, controller.LiveEnemyCount);
    }

    [Fact]
    public void TypedCoordinate_FiresATorpedo()
    {
        var controller = Start(out var config);
        Run(controller, config, QuietSeconds(config) + 1.0);

        controller.SubmitInput("C047");
        var events = Run(controller, config, 0.1);

        Assert.Contains(events, e => e is TorpedoFired);
        Assert.Equal(config.MagazineSize - 1, controller.Snapshot.AmmoLoaded);
    }

    [Fact]
    public void Typo_IsLoggedAndDoesNothing()
    {
        var controller = Start(out var config);
        controller.SubmitInput("C400");
        var events = Run(controller, config, 0.1);

        Assert.Contains(events, e => e is InputNotRecognized);
        Assert.DoesNotContain(events, e => e is TorpedoFired);
        Assert.Equal(config.MagazineSize, controller.Snapshot.AmmoLoaded);
    }

    [Fact]
    public void StoppingTheSweep_FreezesTheLabels()
    {
        var controller = Start(out var config);
        Run(controller, config, QuietSeconds(config) + config.SweepPeriodSeconds + 1.0);

        controller.SubmitInput("SONAR");
        Run(controller, config, 0.1);
        Assert.False(controller.Snapshot.SweepRunning);

        var frozen = controller.Snapshot.Contacts.Select(c => c.Position).ToArray();
        Assert.NotEmpty(frozen);

        Run(controller, config, 10.0);
        Assert.Equal(frozen, controller.Snapshot.Contacts.Select(c => c.Position).ToArray());

        // Control positivo: con el haz andando esos mismos diez segundos SÍ mueven las
        // etiquetas. Sin esto el test pasaría por vacuidad.
        var running = Start(out var runningConfig);
        Run(running, runningConfig, QuietSeconds(runningConfig) + runningConfig.SweepPeriodSeconds + 1.0);
        var before = running.Snapshot.Contacts.Select(c => c.Position).ToArray();
        Run(running, runningConfig, 10.0);

        Assert.NotEqual(before, running.Snapshot.Contacts.Select(c => c.Position).ToArray());
    }

    [Fact]
    public void ReloadDoesNotStopTheSweep()
    {
        var controller = Start(out var config);
        controller.SubmitInput("RELOAD");
        Run(controller, config, 0.1);

        var before = controller.Snapshot.SweepDegrees;
        Run(controller, config, 0.5);

        Assert.True(controller.Snapshot.ReloadInProgress);
        Assert.NotEqual(before, controller.Snapshot.SweepDegrees);
    }
}
```

- [ ] **Step 2: Correr para verificar que falla**

Run: `dotnet test`
Expected: FAIL, porque `RELOAD` en `ReloadDoesNotStopTheSweep` arranca con el cargador lleno.
Si ese test falla, cámbialo para disparar primero:

```csharp
        Run(controller, config, QuietSeconds(config) + 1.0);
        controller.SubmitInput("C047");
        Run(controller, config, 0.1);
        controller.SubmitInput("RELOAD");
        Run(controller, config, 0.1);
```

- [ ] **Step 3: Borrar lo viejo**

```bash
git rm src/Sonar.Controller/OpeningScenario.cs tests/Sonar.Controller.Tests/OpeningScenarioTests.cs
```

Esto rompe la compilación de `src/Sonar.Game/Main.cs`, que todavía llama a
`OpeningScenario.Contacts`. Se arregla en el plan 6. **`dotnet test` corre sobre `Sonar.sln`,
que no incluye `Sonar.Game`, así que la suite sigue verde.**

- [ ] **Step 4: Correr para verificar que pasan**

Run: `dotnet test`
Expected: `Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "test(controller): migrate the opening scenario tests to the gauntlet"
```

---

## Task 9: Determinismo con el gauntlet encendido

**Files:**
- Modify: `tests/Sonar.Core.Tests/DeterminismTests.cs`
- Test: `tests/Sonar.Controller.Tests/GauntletDeterminismTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Controller.Tests/GauntletDeterminismTests.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sonar.Core;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Controller.Tests;

public class GauntletDeterminismTests
{
    private static SimulationConfig WorkingConfig()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Sonar.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return SimulationConfigLoader.LoadFromJson(
            File.ReadAllText(Path.Combine(dir!.FullName, "config", "simulation.working.json")));
    }

    /// Corre un guion de comandos por tick absoluto y devuelve el flujo de eventos.
    private static List<GameEvent> RunScripted(int baseSeed, SimulationConfig config, int ticks)
    {
        var controller = new GameController(config, baseSeed, new Bearing(0), 0.012);
        var script = new Dictionary<int, string>
        {
            [700] = "C047",
            [900] = "STATUS",
            [1100] = "DEF C090",
            [1400] = "RELOAD",
            [2000] = "E200",
        };

        var events = new List<GameEvent>();
        for (var tick = 0; tick < ticks; tick++)
        {
            if (script.TryGetValue(tick, out var text)) controller.SubmitInput(text);
            events.AddRange(controller.Advance(config.TickDurationSeconds));
        }
        return events;
    }

    [Fact]
    public void SameSeed_ProducesIdenticalEventStreams()
    {
        var config = WorkingConfig();
        var a = RunScripted(4242, config, 3000);
        var b = RunScripted(4242, config, 3000);

        // Sin esto el test sería vacuo: dos flujos vacíos también son iguales.
        Assert.NotEmpty(a);
        Assert.Contains(a, e => e is TorpedoFired);

        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++) Assert.Equal(a[i], b[i]);
    }

    [Fact]
    public void DifferentSeed_ProducesADifferentRun()
    {
        var config = WorkingConfig();
        var a = RunScripted(4242, config, 3000);
        var b = RunScripted(9001, config, 3000);

        Assert.NotEqual(a, b);
    }
}
```

- [ ] **Step 2: Correr**

Run: `dotnet test`
Expected: PASS. Si `DifferentSeed_ProducesADifferentRun` falla, el director no está
consumiendo el RNG y hay que revisar la tarea 6.

- [ ] **Step 3: Marcar la nota del plan 3 como resuelta**

En `docs/superpowers/plans/2026-08-11-m1-plan3-combat-threat.md`, en la sección
`## Nota sobre el azar`, agrega al final:

```markdown
> **Resuelto el 2026-08-13.** El `WaveDirector` del plan 5 es el primer consumidor real de
> `IRandomSource`, y el test de semillas distintas vive en
> `GauntletDeterminismTests.DifferentSeed_ProducesADifferentRun`.
```

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "test(controller): pin gauntlet determinism across seeds"
```

---

## Task 10: Actualizar la documentación

**Files:**
- Modify: `docs/tdd/M1-core-simulation.md`
- Modify: `docs/development/godot-wiring.md`
- Modify: `project-state.yaml`

- [ ] **Step 1: TDD — colisión**

En `docs/tdd/M1-core-simulation.md`, al final de `### 4.7 Supervivencia del jugador`:

```markdown
`[DECIDED]` **Un contacto enemigo que llegue a `config.SubHitRadius` destruye el submarino de
inmediato**, sin importar los impactos previos. Es lo que convierte no disparar en una
consecuencia y no en una espera.

Los contactos **inertes no chocan**: un pecio ya está hundido, y morir contra terreno es una
muerte barata. Misma lógica que su exención frente a las defensas (§4.4).

`SubDestroyed` lleva una causa, `Torpedoes` o `Collision`. La telemetría necesita separarlas,
y el jugador también: morir por descuido de rumbo y morir bajo fuego son dos lecciones
distintas.
```

- [ ] **Step 2: TDD — vuelta de barrido y evaluación de disparo**

Al final de `### 5.1 Barrido`:

```markdown
`[DECIDED]` Cada cruce del cero emite `SweepCompleted(Revolution, ReadingCount)`. No cambia
ningún estado ni habilita ninguna acción: es un latido, para que el silencio tenga una unidad
contable.
```

Y una sección nueva antes de `## 6`:

```markdown
### 5.3 Evaluación de disparo — sólo telemetría

`[DECIDED]` Al lanzar un torpedo se emite `ShotAssessed` con la separación angular al contacto
vivo más cercano en marcación, la diferencia entre el centro de la banda pedida y el radio real
de ese contacto, y la edad de la lectura que el jugador podía ver (-1 si nunca fue barrido).

Se calcula en Core porque necesita la posición **real**, que el snapshot esconde a propósito:
calcularlo arriba obligaría a filtrar la verdad hacia la presentación.

**No se muestra al jugador.** Mostrarlo le entregaría gratis la corrección que el juego le pide
calcular. Es la respuesta a la pregunta 1 de M1: error contra edad de lectura da la curva de
extrapolación.

Si no hay contactos vivos no se emite: un disparo al vacío no tiene contra qué medirse, y un
valor sentinela contaminaría los promedios.
```

- [ ] **Step 3: Wiring — eventos nuevos**

En `docs/development/godot-wiring.md`, en la tabla del §6:

```markdown
| `SweepCompleted` | `Revolution`, `ReadingCount` | El haz dio una vuelta. Latido del silencio |
| `ShotAssessed` | error y edad de lectura | **No mostrar.** Sólo telemetría |
| `SubDestroyed` | `Cause` | `Torpedoes` o `Collision` |
```

Y en la tabla de comandos del §8:

```markdown
| `RETRY` | Reinicia desde cero. Sólo con el submarino destruido |
```

- [ ] **Step 4: Estado del proyecto**

En `project-state.yaml`, dentro de `progress:`:

```yaml
  plan_5_gauntlet_rules: done      # 10/10 tareas
```

Y en `systems:`:

```yaml
  wave_gauntlet: done              # oleadas dobladoras, spawns de borde, RETRY
  shot_telemetry: core_only        # el evento existe; falta escribirlo a disco (plan 6)
```

Y borra `random_source` de `open_decisions` si estuviera, agregando en su lugar bajo
`systems:`:

```yaml
  random_source: in_use            # WaveDirector es el primer consumidor real
```

- [ ] **Step 5: Verificación final**

```bash
dotnet test
dotnet build
grep -rn "^using Godot" src/Sonar.Core/ src/Sonar.Controller/
grep -rn "new Dictionary\|new HashSet" src/Sonar.Core/
```

Expected: tests verdes, build sin advertencias, los dos greps sin resultados.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "docs: record the gauntlet rules in the TDD and wiring contract"
```

---

## Definition of Done — Plan 5

- [ ] `dotnet test` verde, `dotnet build` sin advertencias
- [ ] `grep -rn "^using Godot" src/Sonar.Core/ src/Sonar.Controller/` sin resultados
- [ ] `grep -rn "new Dictionary\|new HashSet" src/Sonar.Core/` sin resultados
- [ ] Las oleadas doblan y esperan a que el tablero se limpie
- [ ] Todo entra por el borde, con separación angular mínima
- [ ] Un contacto que llega encima mata de una, y el pecio no
- [ ] `RETRY` reinicia con spawns distintos y la sesión sigue siendo reproducible
- [ ] Cada mutación indicada produce fallos; ninguna produce cero
- [ ] Diez commits, uno por tarea

> `src/Sonar.Game` **no compila** al terminar este plan: `Main.cs` todavía llama a
> `OpeningScenario`. Lo arregla la tarea 1 del plan 6. La suite corre sobre `Sonar.sln`, que
> no incluye el proyecto de Godot, así que el rojo no se ve en `dotnet test`.

## Qué sigue

**Plan 6 — La guardia y la telemetría:** reloj de misión, historial pausado, número de oleada
en el HUD, y el registro JSONL incremental dimensionado para veinte testers externos.
