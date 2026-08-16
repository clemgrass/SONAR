# Plan 7 — La mente del enemigo (base)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que cada submarino enemigo tenga una idea propia y envejecida de dónde está el jugador, se mueva según una personalidad que es un dato, y deje de disparar todos en el mismo tick.

**Architecture:** Tres cambios en Core, uno en Controller, dos chicos en Game. El eje es que `Contact` gana dos campos —`LastFixTick` y `Personality?`— y que **`Personality` nulo conserva exactamente el comportamiento de hoy**. Eso hace que los ~300 tests existentes sigan valiendo y que la IA nueva sea opt-in contacto por contacto.

**Tech Stack:** C# / .NET (TFM `net9.0`), xUnit, Godot 4.7 mono. Sin dependencias nuevas.

**Spec:** [2026-08-15-enemy-ai-design.md](../specs/2026-08-15-enemy-ai-design.md)

---

## Estructura de archivos

| Archivo | Responsabilidad |
|---|---|
| `src/Sonar.Core/Entities/Personality.cs` *(nuevo)* | El record de datos. Sin lógica. |
| `src/Sonar.Core/Entities/PersonalityCatalog.cs` *(nuevo)* | Las cinco instancias con sus números. Sin lógica. |
| `src/Sonar.Core/Entities/Contact.cs` | Gana `LastFixTick` y `Personality`, ambos opcionales. |
| `src/Sonar.Core/Entities/Torpedo.cs` | Gana `CreateEnemyToward`, que apunta a un punto y no al origen. |
| `src/Sonar.Core/Simulation.cs` | Dos fases nuevas: refresco de contactos (10b) y gobierno del movimiento (4b). |
| `src/Sonar.Core/SimulationConfig.cs` | `PassiveHearingRadius`, `EnemyReloadTicks` a 600, `ReloadTicks` a 180. |
| `src/Sonar.Core/ContactSpawner.cs` | **Se borra.** Su escalonamiento se muda al `WaveDirector`. |
| `src/Sonar.Controller/WaveDirector.cs` | `NextFireTick` relativo, escalonamiento, espera aleatoria, asignación de personalidad. |
| `src/Sonar.Game/Main.cs` | El HUD de munición. |
| `config/simulation.working.json` | Los números. |

**Convención de nombres, fijada acá para que no se bifurque entre tareas:**
`Contact.LastFixTick` · `Contact.Personality` · `Personality.AimLead` · `Personality.RadiusLoaded` · `Personality.RadiusEmpty` · `Personality.OrbitRate` · `Personality.FiringRadius` · `Personality.AimJitter` · `Personality.HesitationMinTicks` · `Personality.HesitationMaxTicks` · `Personality.Rams` · `SimulationConfig.PassiveHearingRadius` · `Simulation.RefreshEnemyFixes` · `Simulation.SteerContacts` · `Simulation.EstimatedSubPosition`.

---

### Task 1: Recarga de 3 s y munición legible en el HUD

Dos arreglos chicos e independientes, primero para sacarlos del camino.

**Files:**
- Modify: `config/simulation.working.json`
- Modify: `src/Sonar.Game/Main.cs:243-249`
- Test: `tests/Sonar.Core.Tests/SimulationConfigLoaderTests.cs`

- [ ] **Step 1: Escribir el test que fija los números nuevos**

En `tests/Sonar.Core.Tests/SimulationConfigLoaderTests.cs`, agregar al final de la clase:

```csharp
    [Fact]
    public void WorkingConfig_HasThePlayerReloadAtThreeSeconds()
    {
        var json = System.IO.File.ReadAllText(
            System.IO.Path.Combine(RepoRoot(), "config", "simulation.working.json"));
        var config = SimulationConfigLoader.LoadFromJson(json);

        Assert.Equal(180, config.ReloadTicks);        // 3.0 s a 60 Hz
        Assert.Equal(600, config.EnemyReloadTicks);   // 10.0 s
        Assert.True(config.EnemyReloadTicks > config.ReloadTicks * 3,
            "el enemigo tiene que seguir ciclando mucho más lento que el jugador");
    }
```

`RepoRoot()` **ya existe** en esa clase — lo usa `WorkingConfigOnDisk_IsValid`. Reusarlo, no
escribir otro: dos métodos con la misma firma en la misma clase no compilan.

- [ ] **Step 2: Correrlo y verlo fallar**

Run: `dotnet test tests/Sonar.Core.Tests --filter "FullyQualifiedName~HasThePlayerReloadAtThreeSeconds"`
Expected: FAIL — `Assert.Equal() Failure: Expected: 180, Actual: 420`

- [ ] **Step 3: Cambiar los números**

En `config/simulation.working.json`:

```json
  "reloadTicks": 180,
  "enemyReloadTicks": 600
```

- [ ] **Step 4: Correr toda la suite**

Run: `dotnet test`
Expected: PASS, 302 tests. Si algún test de combate falla por el cambio de recarga, **no ajustar el número: leer el test.** Los tests que fijan su propia recarga con `with { ReloadTicks = ... }` no deberían verse afectados.

- [ ] **Step 5: Arreglar el HUD de munición**

En `src/Sonar.Game/Main.cs`, reemplazar la línea `$"ammo {snap.AmmoLoaded}/{snap.AmmoReserve}    " +` por:

```csharp
            $"{Tubes(snap.AmmoLoaded)}    reserve {snap.AmmoReserve}    " +
```

Y agregar el método a la clase, junto a `MissionClock`:

```csharp
    /// <summary>
    /// `ammo 3/12` se leía como "te quedan 3 de 12". No es eso: son tres en los tubos y doce
    /// en la santabárbara, dos números sin relación separados por una barra que inventaba una.
    /// Los puntos dicen cuántos tubos hay listos sin obligar a leer un número.
    /// </summary>
    private string Tubes(int loaded)
    {
        var full = new string('*', loaded);
        var empty = new string('.', System.Math.Max(0, _magazineSize - loaded));
        return $"tubes {full}{empty}";
    }
```

La capacidad **se lee de la configuración, no se escribe a mano**. `config/simulation.working.json`
existe justamente para que ese número se pueda retocar sin tocar código; una constante local
volvería a producir el mismo bug que esta tarea viene a arreglar —dígitos que no significan lo
que parecen— pero en un sitio donde ningún test lo vería, porque hoy los dos valores coinciden.

`_Ready()` ya tiene el patrón: copia `config.TickDurationSeconds` a un campo para que sobreviva
al ámbito local. Hacer lo mismo aquí:

```csharp
    private int _magazineSize;
```

y en `_Ready()`, junto a la línea de `_tickDuration`:

```csharp
        _magazineSize = config.MagazineSize;
```

No plumbear la capacidad por `GameController` ni `WorldSnapshot`: ninguno la expone hoy y sería
un cambio mucho más grande que esta tarea.

- [ ] **Step 6: Verificar que el juego compila y arranca**

Run: `dotnet build src/Sonar.Game -v q --nologo`
Expected: `Build succeeded. 0 Error(s)`

Run: `timeout 180 godot-mono --headless --path src/Sonar.Game -- --m1-selftest`
Expected: `[SELFTEST] PASA`

- [ ] **Step 7: Commit**

```bash
git add config/simulation.working.json src/Sonar.Game/Main.cs tests/Sonar.Core.Tests/SimulationConfigLoaderTests.cs
git commit -m "feat(game): drop the player reload to three seconds and make the ammo readable"
```

---

### Task 2: `NextFireTick` relativo a la aparición, con escalonamiento

El defecto medido en el §1.1 del spec. Sin esto, cualquier IA hereda la andanada simultánea.

**Files:**
- Modify: `src/Sonar.Controller/WaveDirector.cs:107-142`
- Test: `tests/Sonar.Controller.Tests/WaveDirectorTests.cs`

- [ ] **Step 1: Escribir los dos tests que fallan**

Agregar a `tests/Sonar.Controller.Tests/WaveDirectorTests.cs`:

```csharp
    [Fact]
    public void NoWaveIsEverBornAlreadyArmed()
    {
        // El defecto original: NextFireTick era un tick absoluto, así que de la oleada 4 en
        // adelante el plazo ya había vencido y los contactos disparaban al aparecer.
        var log = RunClearingInstantly(New(), 20000);

        foreach (var (tick, spawned) in log)
        foreach (var contact in spawned)
            Assert.True(contact.NextFireTick > tick,
                $"El contacto {contact.Id.Value} de la oleada que entra en {tick} " +
                $"nace armado (NextFireTick={contact.NextFireTick}).");
    }

    [Fact]
    public void ContactsInAWaveDoNotAllFireOnTheSameTick()
    {
        var log = RunClearingInstantly(New(), 20000);

        foreach (var (tick, spawned) in log)
        {
            if (spawned.Count < 2) continue;
            var distinct = spawned.Select(c => c.NextFireTick).Distinct().Count();
            Assert.True(distinct == spawned.Count,
                $"La oleada que entra en {tick} tiene {spawned.Count} contactos pero sólo " +
                $"{distinct} tiempos de disparo distintos.");
        }
    }
```

- [ ] **Step 2: Correrlos y verlos fallar**

Run: `dotnet test tests/Sonar.Controller.Tests --filter "FullyQualifiedName~NoWaveIsEverBornAlreadyArmed|FullyQualifiedName~ContactsInAWaveDoNotAllFire"`
Expected: FAIL los dos. El primero con "nace armado (NextFireTick=1500)".

- [ ] **Step 3: Arreglar el `WaveDirector`**

En `src/Sonar.Controller/WaveDirector.cs`, cambiar la firma de `Spawn` para que reciba el tick, y la llamada:

```csharp
        var exponent = Math.Min(Wave - 1, 30);
        var count = Math.Min(1 << exponent, MaxWaveSize);
        return Spawn(count, tick);
```

```csharp
    private IReadOnlyList<Contact> Spawn(int count, int spawnTick)
```

Y dentro del bucle, reemplazar `NextFireTick = _config.EnemyFireIntervalTicks,` por:

```csharp
                // Relativo a la aparición, no absoluto. Con el valor absoluto que había
                // antes, el plazo se consumía oleada a oleada y de la cuarta en adelante
                // los contactos nacían armados y disparaban en el tick en que entraban.
                //
                // El escalonamiento es determinista y no toca el generador de azar: sin él
                // toda la oleada dispara en el mismo tick y la presión llega sincronizada
                // en vez de sostenida.
                NextFireTick = spawnTick
                               + _config.EnemyFireIntervalTicks
                               + i * _config.EnemyFireStaggerTicks,
```

- [ ] **Step 4: Correr los tests**

Run: `dotnet test tests/Sonar.Controller.Tests`
Expected: PASS, todos.

- [ ] **Step 5: Mutación — confirmar que los tests muerden**

```bash
mkdir -p /tmp/mut && cp src/Sonar.Controller/WaveDirector.cs /tmp/mut/
perl -pi -e 's/NextFireTick = spawnTick/NextFireTick = 0 * spawnTick/' src/Sonar.Controller/WaveDirector.cs
dotnet test tests/Sonar.Controller.Tests --filter "FullyQualifiedName~NoWaveIsEverBornAlreadyArmed"
```
Expected: FAIL. Después restaurar y confirmar:
```bash
cp /tmp/mut/WaveDirector.cs src/Sonar.Controller/WaveDirector.cs && diff -q /tmp/mut/WaveDirector.cs src/Sonar.Controller/WaveDirector.cs
```

- [ ] **Step 6: Commit**

```bash
git add src/Sonar.Controller/WaveDirector.cs tests/Sonar.Controller.Tests/WaveDirectorTests.cs
git commit -m "fix(controller): stop waves from being born already armed"
```

- [ ] **Step 7: Acotar el reparto y prohibir el stagger cero**

Salió de la revisión de calidad de esta tarea, y las dos mitades son el mismo bug —una oleada
disparando como una sola andanada— por dos caminos distintos.

`i * _config.EnemyFireStaggerTicks` escala con el tamaño de la oleada: con `MaxWaveSize = 512`
el último contacto de una oleada máxima queda armado 153.300 ticks —unos 43 minutos a 60 Hz—
después de entrar, o sea nunca. El escalonamiento existe para **garantizar separación en el
tick de entrada**, no para marcar el ritmo, así que lo que se acota es el reparto completo:

```csharp
    /// <summary>
    /// Techo del reparto de un escalonamiento completo. El valor está elegido para que las
    /// oleadas 1 a 4 queden idénticas: la 4 son ocho contactos y su reparto son 7 x 300 =
    /// 2100 ticks, justo el techo. Sólo recorta de la oleada 5 en adelante, que es
    /// territorio sin medir — y así no perturba el baseline que mide la tarea 11.
    /// </summary>
    private const int MaxStaggerSpanTicks = 2100;
```

```csharp
        // Reparto acotado: separación garantizada, costo acotado.
        var step = count > 1
            ? Math.Max(1, Math.Min((count - 1) * _config.EnemyFireStaggerTicks, MaxStaggerSpanTicks) / (count - 1))
            : 0;
```

usando `i * step`. El piso `Math.Max(1, ...)` mantiene distintos los tiempos de disparo incluso
en el caso degenerado de 512, que es lo que afirma `ContactsInAWaveDoNotAllFireOnTheSameTick`.

Y en `src/Sonar.Core/SimulationConfig.cs`, `Validate()` rechazaba stagger negativo pero **no
cero**, y cero colapsa el escalonamiento entero desde el JSON sin que ningún test lo vea:

```csharp
        // Cero no es sólo inútil, es peligroso: recrea en silencio el defecto de la andanada
        // simultánea (§1.1) que WaveDirector arregla escalonando NextFireTick.
        if (EnemyFireStaggerTicks <= 0) throw Bad(nameof(EnemyFireStaggerTicks), "debe ser mayor a cero.");
```

con su test en `SimulationConfigTests.cs`, y un test de reparto que afirme que la oleada 4 da
exactamente 2100 y que la 5 no lo supera.

---

### Task 3: Borrar `ContactSpawner`

Tener dos dueños del spawn es la causa del defecto de la tarea 2. La lógica ya vive en el `WaveDirector`.

**Files:**
- Delete: `src/Sonar.Core/ContactSpawner.cs`
- Delete: `tests/Sonar.Core.Tests/Combat/StaggerTests.cs`
- Modify: `tests/Sonar.Core.Tests/DeterminismTests.cs:84`
- Modify: `src/Sonar.Core/Simulation.cs` (el comentario que lo menciona)

- [ ] **Step 1: Ver quién lo usa**

Run: `grep -rn "ContactSpawner" --include=*.cs src tests | grep -v "/obj/\|/bin/"`
Expected: `src/Sonar.Core/ContactSpawner.cs`, un comentario en `Simulation.cs`, `DeterminismTests.cs:84` y cuatro usos en `StaggerTests.cs`.

- [ ] **Step 2: Reemplazar `CombatContacts()` en `DeterminismTests.cs`**

Ese método (líneas 82-89) sólo usa el spawner para armar tres enemigos con tiempos de disparo
escalonados. Lo que el test comprueba es otra cosa —que dos corridas con la misma semilla dan
el mismo hash tick a tick— así que los contactos se escriben a mano con los **mismos** valores
que producía el spawner: `spawnTick 0 + EnemyFireIntervalTicks 420 + i * EnemyFireStaggerTicks 70`.

```csharp
    private static IEnumerable<Contact> CombatContacts() => new[]
    {
        new Contact
        {
            Id = new ContactId(200), Kind = ContactKind.Enemy,
            Position = new Vec2(0.0, -0.75), Velocity = new Vec2(0.02, 0.05),
            Alive = true, NextFireTick = 420,
        },
        new Contact
        {
            Id = new ContactId(201), Kind = ContactKind.Enemy,
            Position = new Vec2(0.70, 0.20), Velocity = new Vec2(-0.06, 0.01),
            Alive = true, NextFireTick = 490,
        },
        new Contact
        {
            Id = new ContactId(202), Kind = ContactKind.Enemy,
            Position = new Vec2(-0.60, 0.40), Velocity = new Vec2(0.04, -0.03),
            Alive = true, NextFireTick = 560,
        },
    };
```

Conservar los valores exactos importa: si el escenario cambia, el test sigue pasando pero deja
de cubrir el caso que cubría.

- [ ] **Step 3: Borrar los archivos**

```bash
git rm src/Sonar.Core/ContactSpawner.cs tests/Sonar.Core.Tests/Combat/StaggerTests.cs
```

`StaggerTests` se borra y no se muda: el escalonamiento ya quedó cubierto por `ContactsInAWaveDoNotAllFireOnTheSameTick` de la tarea 2, que lo prueba **donde de verdad ocurre**.

- [ ] **Step 4: Arreglar el comentario de `Simulation.cs`**

En el bloque de documentación de `FireEnemyTorpedoes`, reemplazar la línea que dice
`` `EnemyFireIntervalTicks` ya no gobierna la cadencia: sólo lo usa ContactSpawner para ``
y la siguiente, por:

```csharp
    /// `EnemyFireIntervalTicks` no gobierna la cadencia: sólo lo usa el WaveDirector para
    /// decidir cuánto tarda en armarse un contacto recién aparecido.
```

- [ ] **Step 5: Compilar y correr todo**

Run: `dotnet test`
Expected: PASS. El total baja en 4 tests (los de `StaggerTests`).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor(core): delete ContactSpawner, the spawn path that was never called"
```

---

### Task 4: `Contact` gana memoria y personalidad, sin romper nada

**Files:**
- Create: `src/Sonar.Core/Entities/Personality.cs`
- Modify: `src/Sonar.Core/Entities/Contact.cs`
- Test: `tests/Sonar.Core.Tests/Entities/ContactTests.cs`

- [ ] **Step 1: Escribir el test de compatibilidad**

Agregar a `tests/Sonar.Core.Tests/Entities/ContactTests.cs`:

```csharp
    [Fact]
    public void AContactWithoutPersonalityKeepsTheOldShape()
    {
        // Los campos nuevos son opcionales a propósito: un contacto sin personalidad se
        // comporta exactamente como antes, y por eso los tests que ya existían siguen
        // valiendo sin tocarlos.
        var contact = new Contact
        {
            Id = new ContactId(1),
            Kind = ContactKind.Enemy,
            Position = new Vec2(0.0, -0.5),
            Velocity = Vec2.Zero,
            Alive = true,
            NextFireTick = 100,
        };

        Assert.Null(contact.Personality);
        Assert.Equal(0, contact.LastFixTick);
    }
```

- [ ] **Step 2: Correrlo y verlo fallar**

Run: `dotnet test tests/Sonar.Core.Tests --filter "FullyQualifiedName~KeepsTheOldShape"`
Expected: FAIL a la compilación — `'Contact' does not contain a definition for 'Personality'`.

- [ ] **Step 3: Crear el record de personalidad**

Crear `src/Sonar.Core/Entities/Personality.cs`:

```csharp
namespace Sonar.Core.Entities;

/// <summary>
/// El carácter de un submarino enemigo, como datos y no como clase. No hay herencia, ni
/// polimorfismo, ni un método por tipo: son números que las reglas leen.
///
/// Es deliberado y es lo que hace que agregar la sexta personalidad para el juego final sea
/// una entrada de configuración en vez de código nuevo. Ver el spec del 2026-08-15, §4.
/// </summary>
public sealed record Personality
{
    public required string Name { get; init; }

    /// <summary>A qué distancia de su estimación quiere estar cuando tiene torpedo.</summary>
    public required double RadiusLoaded { get; init; }

    /// <summary>
    /// Y cuando está vacío. Que estos dos números difieran <b>es</b> el ciclo de acecho:
    /// se acerca a disparar, y al quedar vacío se abre solo a recargar. No hace falta
    /// ninguna máquina de estados.
    /// </summary>
    public required double RadiusEmpty { get; init; }

    /// <summary>Componente tangencial del movimiento. Cero va derecho, alto rodea.</summary>
    public required double OrbitRate { get; init; }

    /// <summary>Distancia desde la que se decide a disparar. Cero significa que nunca dispara.</summary>
    public required double FiringRadius { get; init; }

    /// <summary>
    /// 0 le dispara a dónde estabas; 1 compensa la deriva entera y te apunta a ti. Es la
    /// única diferencia entre el HUNTER y el resto, y se siente como otra criatura.
    /// </summary>
    public required double AimLead { get; init; }

    /// <summary>
    /// Error extra, en unidades de arena, independiente de la antigüedad. Arranca en cero
    /// para todas menos el GOAD: el error de la deriva es legible —falla siempre hacia atrás
    /// en el rumbo del jugador— y el jitter es ruido puro.
    /// </summary>
    public required double AimJitter { get; init; }

    /// <summary>
    /// Cuánto tarda en decidirse, sorteado uniforme en cada disparo. El ancho de esta
    /// ventana dice más del carácter que casi cualquier otro número: un impulsivo y un
    /// paciente se sienten distintos aunque disparen desde el mismo sitio.
    /// </summary>
    public required int HesitationMinTicks { get; init; }
    public required int HesitationMaxTicks { get; init; }

    /// <summary>
    /// Si mata por contacto en vez de por torpedo. <b>Una embestida siempre suena</b>: no hay
    /// un parámetro aparte para eso porque en el roster entero valdría siempre lo mismo que
    /// éste, y un campo que nunca difiere de otro es una mentira sobre lo que el sistema
    /// puede hacer. Que el kamikaze avise no es una perilla — es la regla que impide que mate
    /// con algo que no se pudo anticipar.
    /// </summary>
    public required bool Rams { get; init; }
}
```

- [ ] **Step 4: Agregar los campos a `Contact`**

En `src/Sonar.Core/Entities/Contact.cs`, agregar después de `NextFireTick`:

```csharp
    /// <summary>
    /// Tick en el que este enemigo escuchó al jugador por última vez. Es <b>todo</b> lo que
    /// sabe de él: como el submarino está clavado en el origen y el mundo deriva alrededor,
    /// una posición vieja envejece sola hacia atrás en el rumbo del jugador. No hace falta
    /// simular una creencia; la deriva ya la calcula.
    /// </summary>
    public int LastFixTick { get; init; }

    /// <summary>
    /// Su carácter, o null. <b>Null significa el comportamiento de siempre</b>: velocidad
    /// fija y puntería perfecta al origen. Es lo que permite que la IA nueva sea opt-in
    /// contacto por contacto y que los tests anteriores sigan valiendo sin tocarlos.
    /// </summary>
    public Personality? Personality { get; init; }
```

- [ ] **Step 5: Correr toda la suite**

Run: `dotnet test`
Expected: PASS. Ni un solo test anterior debería necesitar cambios — ése es el punto de que los campos sean opcionales.

- [ ] **Step 6: Commit**

```bash
git add src/Sonar.Core/Entities/Personality.cs src/Sonar.Core/Entities/Contact.cs tests/Sonar.Core.Tests/Entities/ContactTests.cs
git commit -m "feat(core): give contacts a memory and an optional personality"
```

---

### Task 5: El catálogo de las cinco personalidades

**Files:**
- Create: `src/Sonar.Core/Entities/PersonalityCatalog.cs`
- Test: `tests/Sonar.Core.Tests/Entities/PersonalityCatalogTests.cs`

- [ ] **Step 1: Escribir el test**

Crear `tests/Sonar.Core.Tests/Entities/PersonalityCatalogTests.cs`:

```csharp
using System.Linq;
using Sonar.Core.Entities;
using Xunit;

namespace Sonar.Core.Tests.Entities;

public class PersonalityCatalogTests
{
    [Fact]
    public void TheRosterIsFiveAndTheyAreDistinguishable()
    {
        Assert.Equal(5, PersonalityCatalog.All.Count);
        Assert.Equal(5, PersonalityCatalog.All.Select(p => p.Name).Distinct().Count());
    }

    [Fact]
    public void OnlyTheStalkerAndTheHunterWithdrawToReload()
    {
        // El ciclo de acecho es que los dos radios difieran. Que PICKET y GOAD los tengan
        // iguales es lo que los deja clavados en su sitio.
        Assert.NotEqual(PersonalityCatalog.Stalker.RadiusLoaded, PersonalityCatalog.Stalker.RadiusEmpty);
        Assert.NotEqual(PersonalityCatalog.Hunter.RadiusLoaded, PersonalityCatalog.Hunter.RadiusEmpty);
        Assert.Equal(PersonalityCatalog.Picket.RadiusLoaded, PersonalityCatalog.Picket.RadiusEmpty);
        Assert.Equal(PersonalityCatalog.Goad.RadiusLoaded, PersonalityCatalog.Goad.RadiusEmpty);
    }

    [Fact]
    public void OnlyTheHunterCompensatesForDrift()
    {
        // Es la única diferencia que lo hace el enemigo del que hay que preocuparse: mientras
        // haya uno en el tablero, las estadísticas de fallo no protegen a nadie.
        Assert.Equal(1.0, PersonalityCatalog.Hunter.AimLead);
        foreach (var other in PersonalityCatalog.All.Where(p => p.Name != "HUNTER"))
            Assert.Equal(0.0, other.AimLead);
    }

    [Fact]
    public void OnlyTheRamRamsAndItIsTheOnlyOneThatNeverFires()
    {
        Assert.True(PersonalityCatalog.Ram.Rams);
        Assert.Equal(0.0, PersonalityCatalog.Ram.FiringRadius);

        foreach (var other in PersonalityCatalog.All.Where(p => p.Name != "RAM"))
        {
            Assert.False(other.Rams);
            Assert.True(other.FiringRadius > 0.0);
        }
    }

    [Fact]
    public void EveryHesitationWindowIsWellFormed()
    {
        foreach (var p in PersonalityCatalog.All.Where(p => p.FiringRadius > 0.0))
            Assert.True(p.HesitationMaxTicks >= p.HesitationMinTicks && p.HesitationMinTicks >= 0,
                $"{p.Name} tiene una ventana de duda inválida.");
    }
}
```

- [ ] **Step 2: Correrlo y verlo fallar**

Run: `dotnet test tests/Sonar.Core.Tests --filter "FullyQualifiedName~PersonalityCatalogTests"`
Expected: FAIL a la compilación — `The name 'PersonalityCatalog' does not exist`.

- [ ] **Step 3: Escribir el catálogo**

Crear `src/Sonar.Core/Entities/PersonalityCatalog.cs`:

```csharp
using System.Collections.Generic;

namespace Sonar.Core.Entities;

/// <summary>
/// Las cinco personalidades de M1, con sus números. Los radios están elegidos contra el
/// `bandOuterRadii` de la configuración de trabajo:
/// A 0.10–0.28 · B 0.28–0.46 · C 0.46–0.64 · D 0.64–0.82 · E 0.82–1.00.
///
/// Es una lista de datos a propósito. Ver el spec del 2026-08-15, §4.
/// </summary>
public static class PersonalityCatalog
{
    /// <summary>No entra nunca. Orbita la banda E y tira de lejos, con paciencia y sin
    /// puntería: su torpedo tarda unos 30 s en llegar desde ahí y se ve venir. Peligroso
    /// por acumulación. Orbita porque está buscando un carril limpio entre sus compañeros.</summary>
    public static readonly Personality Picket = new()
    {
        Name = "PICKET",
        RadiusLoaded = 0.90, RadiusEmpty = 0.90,
        OrbitRate = 0.010, FiringRadius = 0.90,
        AimLead = 0.0, AimJitter = 0.0,
        HesitationMinTicks = 240, HesitationMaxTicks = 720,   // 4–12 s
        Rams = false,
    };

    /// <summary>La columna vertebral del ritmo. Cierra a banda C, dispara, y al quedar vacío
    /// se abre solo hasta la E. El alejarse es el aviso de que el jugador tiene una ventana.</summary>
    public static readonly Personality Stalker = new()
    {
        Name = "STALKER",
        RadiusLoaded = 0.55, RadiusEmpty = 0.85,
        OrbitRate = 0.003, FiringRadius = 0.55,
        AimLead = 0.0, AimJitter = 0.0,
        HesitationMinTicks = 120, HesitationMaxTicks = 360,   // 2–6 s
        Rams = false,
    };

    /// <summary>No dispara jamás: te busca y se te tira encima. La única amenaza que se mide
    /// en radio y no en torpedos, y por eso hace ruido al acercarse.</summary>
    public static readonly Personality Ram = new()
    {
        Name = "RAM",
        RadiusLoaded = 0.00, RadiusEmpty = 0.00,
        OrbitRate = 0.0, FiringRadius = 0.0,
        AimLead = 0.0, AimJitter = 0.0,
        HesitationMinTicks = 0, HesitationMaxTicks = 0,
        Rams = true,
    };

    /// <summary>Tira seguido y mal desde media distancia. No espera matar: quiere que gastes
    /// defensas y que hagas ruido, y el ruido es lo que le afina la puntería al PICKET. Es
    /// coordinación de manada sin escribir coordinación.</summary>
    public static readonly Personality Goad = new()
    {
        Name = "GOAD",
        RadiusLoaded = 0.60, RadiusEmpty = 0.60,
        OrbitRate = 0.006, FiringRadius = 0.60,
        AimLead = 0.0, AimJitter = 0.055,
        HesitationMinTicks = 30, HesitationMaxTicks = 120,    // 0.5–2 s
        Rams = false,
    };

    /// <summary>El único que compensa la deriva. Aparece tarde y su llegada dice "esto se
    /// puso serio" sin una línea de texto.</summary>
    public static readonly Personality Hunter = new()
    {
        Name = "HUNTER",
        RadiusLoaded = 0.50, RadiusEmpty = 0.70,
        OrbitRate = 0.003, FiringRadius = 0.60,
        AimLead = 1.0, AimJitter = 0.0,
        HesitationMinTicks = 120, HesitationMaxTicks = 240,   // 2–4 s
        Rams = false,
    };

    public static readonly IReadOnlyList<Personality> All =
        new[] { Picket, Stalker, Ram, Goad, Hunter };
}
```

- [ ] **Step 4: Correr los tests**

Run: `dotnet test tests/Sonar.Core.Tests --filter "FullyQualifiedName~PersonalityCatalogTests"`
Expected: PASS, 5 tests.

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/Entities/PersonalityCatalog.cs tests/Sonar.Core.Tests/Entities/PersonalityCatalogTests.cs
git commit -m "feat(core): add the five enemy personalities as data"
```

---

### Task 6: El refresco de contactos — qué te delata

**Files:**
- Modify: `src/Sonar.Core/SimulationConfig.cs`
- Modify: `src/Sonar.Core/Simulation.cs`
- Modify: `config/simulation.working.json`
- Test: `tests/Sonar.Core.Tests/Combat/NoiseTests.cs` *(nuevo)*

- [ ] **Step 1: Escribir los tests**

Crear `tests/Sonar.Core.Tests/Combat/NoiseTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Combat;

public class NoiseTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    /// Lejos del radio de escucha pasiva, para que sólo el ruido lo refresque.
    private static Contact FarEnemy(int id) => new()
    {
        Id = new ContactId(id),
        Kind = ContactKind.Enemy,
        Position = new Vec2(0.0, -0.90),
        Velocity = Vec2.Zero,
        Alive = true,
        NextFireTick = int.MaxValue,
        LastFixTick = 0,
        Personality = PersonalityCatalog.Picket,
    };

    [Fact]
    public void SilenceLetsTheFixGoStale()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { FarEnemy(1) });

        for (var i = 0; i < 30; i++) sim.Tick(NoCommands);

        // El sonar arranca corriendo, así que hay que apagarlo para que haya silencio real.
        sim.Tick(new Command[] { new ToggleSonarCommand() });
        var before = sim.State.Contacts[0].LastFixTick;
        for (var i = 0; i < 300; i++) sim.Tick(NoCommands);

        Assert.Equal(before, sim.State.Contacts[0].LastFixTick);
    }

    [Fact]
    public void FiringATorpedoGivesEveryEnemyAFreshFix()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { FarEnemy(1) });
        sim.Tick(new Command[] { new ToggleSonarCommand() });   // apagar el barrido
        for (var i = 0; i < 200; i++) sim.Tick(NoCommands);

        var stale = sim.State.Contacts[0].LastFixTick;

        sim.Tick(new Command[]
        {
            new FireCommand(new TargetCoordinate(new Bearing(90), new Band(3))),
        });
        sim.Tick(NoCommands);

        Assert.True(sim.State.Contacts[0].LastFixTick > stale,
            "disparar tiene que delatar la posición");
    }

    [Fact]
    public void ASweepRevolutionGivesEveryEnemyAFreshFix()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { FarEnemy(1) });

        // Una vuelta completa del haz, más un tick de margen.
        var ticksPerRevolution = (int)Math.Ceiling(config.SweepPeriodSeconds / config.TickDurationSeconds);
        for (var i = 0; i < ticksPerRevolution + 2; i++) sim.Tick(NoCommands);

        Assert.True(sim.State.Contacts[0].LastFixTick > 0,
            "cada vuelta del haz tiene que delatar la posición");
    }

    [Fact]
    public void ProximityRefreshesEvenInTotalSilence()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        // Dentro del radio de escucha pasiva.
        sim.SetContacts(new[]
        {
            FarEnemy(1) with { Position = new Vec2(0.0, -config.PassiveHearingRadius * 0.5) },
        });
        sim.Tick(new Command[] { new ToggleSonarCommand() });

        for (var i = 0; i < 300; i++) sim.Tick(NoCommands);

        Assert.True(sim.State.Contacts[0].LastFixTick >= sim.State.Tick - 2,
            "un enemigo pegado te oye aunque estés callado");
    }
}
```

- [ ] **Step 2: Correrlos y verlos fallar**

Run: `dotnet test tests/Sonar.Core.Tests --filter "FullyQualifiedName~NoiseTests"`
Expected: FAIL a la compilación — `'SimulationConfig' does not contain a definition for 'PassiveHearingRadius'`.

- [ ] **Step 3: Agregar el radio de escucha pasiva a la configuración**

En `src/Sonar.Core/SimulationConfig.cs`, junto a los otros radios:

```csharp
    /// <summary>
    /// Dentro de este radio un enemigo oye al jugador aunque esté en silencio absoluto: el
    /// sonido se atenúa con la distancia. Es lo que hace que callarse proteja de los lejanos
    /// y nunca de los cercanos, y lo que hace que acercarse sea la forma de apuntar.
    /// </summary>
    public double PassiveHearingRadius { get; init; }
```

En `Validate()`, junto a los otros positivos:

```csharp
        Positive(PassiveHearingRadius, nameof(PassiveHearingRadius));
```

En `config/simulation.working.json`, después de `"subHitRadius"`:

```json
  "passiveHearingRadius": 0.46,
```

Y en **todos** los constructores de config de test, agregar `PassiveHearingRadius = 0.46,`:
`tests/Sonar.Core.Tests/SimulationConfigTests.cs`, `tests/Sonar.Core.Tests/WorldStateTests.cs`,
`tests/Sonar.Core.Tests/Support/TestWorld.cs`, `tests/Sonar.Controller.Tests/SnapshotBuilderTests.cs`,
`tests/Sonar.Controller.Tests/WaveDirectorTests.cs`, `tests/Sonar.Controller.Tests/GameControllerTests.cs`.
Y a los dos JSON literales de `tests/Sonar.Core.Tests/SimulationConfigLoaderTests.cs`.

- [ ] **Step 4: Escribir la fase de refresco**

En `src/Sonar.Core/Simulation.cs`, agregar el método junto a las otras fases:

```csharp
    /// <summary>
    /// Fase 10b · Qué delata al jugador. Cada enemigo guarda un solo dato —el tick en que lo
    /// escuchó por última vez— y esta fase es lo único que lo escribe.
    ///
    /// Corre después del barrido para que `SweepCompleted` de este tick ya esté en la lista.
    /// El disparo enemigo (fase 5) usa entonces los contactos del tick anterior: un tick de
    /// desfase, imperceptible, y a cambio el orden queda sin ambigüedad.
    /// </summary>
    private List<Contact> RefreshEnemyFixes(List<Contact> contacts, List<GameEvent> events, int tick)
    {
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

        return contacts;
    }
```

Y llamarla en `Tick`, justo después de la fase 10:

```csharp
        // 10 · Barrido y registro de lecturas
        var sonar = AdvanceSweepAndRecord(state.Sonar, contacts, state.Tick, dt, events);

        // 10b · Qué delata al jugador
        contacts = RefreshEnemyFixes(contacts, events, state.Tick);
```

- [ ] **Step 5: Correr los tests**

Run: `dotnet test tests/Sonar.Core.Tests --filter "FullyQualifiedName~NoiseTests"`
Expected: PASS, 4 tests.

Run: `dotnet test`
Expected: PASS, toda la suite.

- [ ] **Step 6: Mutación — que el refresco por cercanía muerda**

```bash
cp src/Sonar.Core/Simulation.cs /tmp/mut/
perl -pi -e 's/if \(loud \|\| close\)/if (loud)/' src/Sonar.Core/Simulation.cs
dotnet test tests/Sonar.Core.Tests --filter "FullyQualifiedName~ProximityRefreshes"
```
Expected: FAIL. Restaurar con `cp /tmp/mut/Simulation.cs src/Sonar.Core/Simulation.cs` y confirmar con `diff -q`.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(core): let enemies hear the player instead of always knowing"
```

---

### Task 7: La puntería envejecida

**Files:**
- Modify: `src/Sonar.Core/Entities/Torpedo.cs`
- Modify: `src/Sonar.Core/Simulation.cs` (`FireEnemyTorpedoes`)
- Test: `tests/Sonar.Core.Tests/Combat/StaleAimTests.cs` *(nuevo)*

- [ ] **Step 1: Escribir los tests**

Crear `tests/Sonar.Core.Tests/Combat/StaleAimTests.cs`:

```csharp
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

    private static Contact Enemy(Personality personality, int lastFixTick) => new()
    {
        Id = new ContactId(1),
        Kind = ContactKind.Enemy,
        Position = new Vec2(0.0, -0.90),
        Velocity = Vec2.Zero,
        Alive = true,
        NextFireTick = 0,
        LastFixTick = lastFixTick,
        Personality = personality,
    };

    [Fact]
    public void AContactWithoutPersonalityStillAimsStraightAtTheOrigin()
    {
        // El comportamiento de siempre tiene que seguir intacto: es lo que sostiene a los
        // tests anteriores.
        var sim = TestWorld.Moving();
        sim.SetContacts(new[] { Enemy(null!, 0) with { Personality = null } });
        sim.Tick(NoCommands);

        var torpedo = sim.State.Torpedoes.Single(t => t.Side == TorpedoSide.Enemy);
        var towardOrigin = (Vec2.Zero - new Vec2(0.0, -0.90)).Normalized();
        Assert.Equal(towardOrigin.X, torpedo.Direction.X, precision: 6);
        Assert.Equal(towardOrigin.Y, torpedo.Direction.Y, precision: 6);
    }

    [Fact]
    public void AStaleFixMakesTheTorpedoMissBehindThePlayer()
    {
        // Con el submarino en movimiento, un contacto viejo envejece hacia atrás en el rumbo.
        var sim = TestWorld.Moving();
        sim.SetContacts(new[] { Enemy(PersonalityCatalog.Picket, lastFixTick: -600) });
        sim.Tick(NoCommands);

        var torpedo = sim.State.Torpedoes.Single(t => t.Side == TorpedoSide.Enemy);
        var towardOrigin = (Vec2.Zero - new Vec2(0.0, -0.90)).Normalized();

        // Apunta a otro lado que el origen.
        var dot = torpedo.Direction.X * towardOrigin.X + torpedo.Direction.Y * towardOrigin.Y;
        Assert.True(dot < 0.9999, $"el torpedo apuntó al origen igual (dot={dot:F6})");
    }

    [Fact]
    public void TheHunterCompensatesAndStillAimsAtThePlayer()
    {
        // Misma antigüedad que el test anterior, pero AimLead = 1.
        var sim = TestWorld.Moving();
        sim.SetContacts(new[] { Enemy(PersonalityCatalog.Hunter, lastFixTick: -600) });
        sim.Tick(NoCommands);

        var torpedo = sim.State.Torpedoes.Single(t => t.Side == TorpedoSide.Enemy);
        var towardOrigin = (Vec2.Zero - new Vec2(0.0, -0.90)).Normalized();
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
        var towardOrigin = (Vec2.Zero - new Vec2(0.0, -0.90)).Normalized();
        Assert.Equal(towardOrigin.X, torpedo.Direction.X, precision: 6);
        Assert.Equal(towardOrigin.Y, torpedo.Direction.Y, precision: 6);
    }
}
```

- [ ] **Step 2: Agregar `TestWorld.Moving()` si no existe**

Leer `tests/Sonar.Core.Tests/Support/TestWorld.cs`. Si sólo tiene `Still()`, agregar:

```csharp
    /// <summary>
    /// Como Still() pero con el submarino avanzando, que es lo que hace que la deriva del
    /// mundo sea distinta de cero. Sin deriva no hay envejecimiento que medir.
    /// </summary>
    public static Simulation Moving(SimulationConfig? config = null)
    {
        var c = config ?? Config();
        return new Simulation(c, new SeededRandomSource(1),
            WorldState.CreateInitial(c, new Bearing(0), speed: 0.012));
    }
```

- [ ] **Step 3: Correr los tests y verlos fallar**

Run: `dotnet test tests/Sonar.Core.Tests --filter "FullyQualifiedName~StaleAimTests"`
Expected: FAIL — `AStaleFixMakesTheTorpedoMissBehindThePlayer` falla porque hoy todos apuntan al origen.

- [ ] **Step 4: Agregar el constructor de torpedo dirigido**

En `src/Sonar.Core/Entities/Torpedo.cs`, junto a `CreateEnemy`:

```csharp
    /// <summary>
    /// Torpedo enemigo dirigido a un punto que no es necesariamente el origen: la
    /// estimación envejecida que el enemigo tiene del jugador. Si el punto coincide con el
    /// origen, es idéntico a CreateEnemy.
    /// </summary>
    public static Torpedo CreateEnemyToward(
        TorpedoId id, Vec2 origin, Vec2 target, double speed, int sourceContactId = -1) => new()
    {
        Id = id,
        Side = TorpedoSide.Enemy,
        Position = origin,
        Direction = (target - origin).Normalized(),
        Speed = speed,
        Bearing = new Bearing(0),
        ArmedFromRadius = 0,
        RunToRadius = 0,
        SourceContactId = sourceContactId,
    };
```

- [ ] **Step 5: Usar la estimación al disparar**

En `src/Sonar.Core/Simulation.cs`, agregar **los dos** helpers. Van separados porque la
tarea 8 necesita la estimación **sin jitter**: llamar a la versión con jitter desde dos fases
distintas consumiría el generador de azar dos veces por tick y rompería la reproducibilidad.
El jitter es error de *tiro*, no de navegación.

```csharp
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
```

En `FireEnemyTorpedoes`, cambiar la firma para recibir la deriva y el paso:

```csharp
    private WorldState FireEnemyTorpedoes(
        WorldState state, List<Contact> contacts, List<Torpedo> torpedoes,
        List<GameEvent> events, Vec2 worldDrift, double dt)
```

y reemplazar la creación del torpedo por:

```csharp
            var target = EstimatedSubPosition(contact, state.Tick, worldDrift, dt);
            torpedoes.Add(Torpedo.CreateEnemyToward(new TorpedoId(id), contact.Position,
                target, _config.EnemyTorpedoSpeed, contact.Id.Value));
```

Y en `Tick`, la llamada de la fase 5:

```csharp
        // 5 · Disparo enemigo
        state = FireEnemyTorpedoes(state, contacts, torpedoes, events, worldDrift, dt);
```

- [ ] **Step 6: Correr los tests**

Run: `dotnet test`
Expected: PASS, toda la suite. Los tests de combate anteriores usan contactos sin personalidad, así que siguen apuntando al origen.

- [ ] **Step 7: Mutación — que la compensación del HUNTER muerda**

```bash
cp src/Sonar.Core/Simulation.cs /tmp/mut/
perl -pi -e 's/\* \(1\.0 - contact\.Personality\.AimLead\)/* 1.0/' src/Sonar.Core/Simulation.cs
dotnet test tests/Sonar.Core.Tests --filter "FullyQualifiedName~TheHunterCompensates"
```
Expected: FAIL. Restaurar y confirmar con `diff -q`.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(core): make enemies shoot at where the player was"
```

---

### Task 8: El gobierno del movimiento

**Files:**
- Modify: `src/Sonar.Core/Simulation.cs`
- Test: `tests/Sonar.Core.Tests/Combat/SteeringTests.cs` *(nuevo)*

- [ ] **Step 1: Escribir los tests**

Crear `tests/Sonar.Core.Tests/Combat/SteeringTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Combat;

public class SteeringTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Contact At(double radius, Personality personality, int ammo = 1) => new()
    {
        Id = new ContactId(1),
        Kind = ContactKind.Enemy,
        Position = new Vec2(0.0, -radius),
        Velocity = Vec2.Zero,
        Alive = true,
        NextFireTick = int.MaxValue,
        LastFixTick = 0,
        Personality = personality,
    };

    [Fact]
    public void AContactWithoutPersonalityKeepsItsFixedVelocity()
    {
        // Esto es lo que sostiene todos los tests anteriores: sin personalidad, balístico.
        var sim = TestWorld.Still();
        var velocity = new Vec2(0.01, 0.02);
        sim.SetContacts(new[] { At(0.9, null!) with { Personality = null, Velocity = velocity } });

        for (var i = 0; i < 60; i++) sim.Tick(NoCommands);

        Assert.Equal(velocity.X, sim.State.Contacts[0].Velocity.X, precision: 9);
        Assert.Equal(velocity.Y, sim.State.Contacts[0].Velocity.Y, precision: 9);
    }

    [Fact]
    public void ThePicketHoldsTheOuterBand()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { At(0.99, PersonalityCatalog.Picket) });

        for (var i = 0; i < 1800; i++) sim.Tick(NoCommands);

        var radius = sim.State.Contacts[0].Position.Length;
        Assert.InRange(radius, 0.84, 0.96);   // se quedó en la banda E
    }

    [Fact]
    public void TheRamClosesAllTheWayIn()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { At(0.99, PersonalityCatalog.Ram) });

        var before = sim.State.Contacts[0].Position.Length;
        for (var i = 0; i < 600; i++)
        {
            sim.Tick(NoCommands);
            if (sim.State.Contacts.Count == 0) break;   // llegó y mató
        }

        if (sim.State.Contacts.Count > 0)
            Assert.True(sim.State.Contacts[0].Position.Length < before - 0.05,
                "el RAM tiene que estar cerrando distancia");
        else
            Assert.True(sim.State.Sub.Destroyed, "si desapareció fue porque llegó");
    }

    [Fact]
    public void TheStalkerWithdrawsWhenItHasNoTorpedoLeft()
    {
        // El ciclo de acecho: cargado se acerca, vacío se abre. Se simula "vacío" poniéndole
        // un torpedo suyo en el agua, que es lo que la regla mira.
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { At(0.55, PersonalityCatalog.Stalker) });
        sim.SetTorpedoes(new[]
        {
            Torpedo.CreateEnemy(new TorpedoId(900), new Vec2(0.0, -0.30), 0.03, sourceContactId: 1),
        });

        var before = sim.State.Contacts[0].Position.Length;
        for (var i = 0; i < 600; i++) sim.Tick(NoCommands);

        Assert.True(sim.State.Contacts[0].Position.Length > before,
            "con su torpedo en el agua el STALKER tiene que estar abriéndose");
    }

    [Fact]
    public void NoSteeredContactEverOutrunsHalfATorpedo()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { At(0.99, PersonalityCatalog.Ram) });

        for (var i = 0; i < 300 && sim.State.Contacts.Count > 0; i++)
        {
            sim.Tick(NoCommands);
            if (sim.State.Contacts.Count == 0) break;
            Assert.True(sim.State.Contacts[0].Velocity.Length <= config.EnemyTorpedoSpeed / 2.0 + 1e-9,
                "el techo de velocidad se rompió");
        }
    }
}
```

- [ ] **Step 2: Correrlos y verlos fallar**

Run: `dotnet test tests/Sonar.Core.Tests --filter "FullyQualifiedName~SteeringTests"`
Expected: FAIL — `ThePicketHoldsTheOuterBand` y los demás fallan porque nadie gobierna la velocidad.

- [ ] **Step 3: Escribir la fase de gobierno**

En `src/Sonar.Core/Simulation.cs`:

```csharp
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

            var centre = EstimatedSubPosition(contact, tick, worldDrift, dt);
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
```

Llamarla en `Tick`, **antes** de mover los contactos, reordenando la fase 4:

```csharp
        // 4 · Gobernar y mover contactos. El gobierno va primero para que la velocidad que
        //     se integra sea la de este tick y no la del anterior.
        SteerContacts(contacts, torpedoes, state.Tick, worldDrift, dt);

        for (var i = 0; i < contacts.Count; i++)
        {
            var c = contacts[i];
            if (!c.Alive) continue;
            contacts[i] = c with { Position = c.Position + (c.Velocity + worldDrift) * dt };
        }
```

**Ojo con el generador de azar:** `SteerContacts` usa `EstimatedCentre` y **no**
`EstimatedSubPosition`. Las dos existen desde la tarea 7 justamente por esto — la segunda
sortea el jitter del GOAD, y llamarla desde el gobierno *y* desde el disparo lo consumiría
dos veces por tick y rompería la reproducibilidad.

- [ ] **Step 4: Correr los tests**

Run: `dotnet test`
Expected: PASS, toda la suite. Especial atención a `WaveDirectorTests` y `GauntletDeterminismTests`: si alguno falla, es porque el gobierno cambió trayectorias que ese test daba por fijas — **leer el test antes de tocarlo**.

- [ ] **Step 5: Mutación — que el techo de velocidad muerda**

```bash
cp src/Sonar.Core/Simulation.cs /tmp/mut/
perl -pi -e 's/if \(desired\.Length > ceiling\) desired = desired\.Normalized\(\) \* ceiling;//' src/Sonar.Core/Simulation.cs
dotnet test tests/Sonar.Core.Tests --filter "FullyQualifiedName~NoSteeredContactEverOutruns"
```
Expected: FAIL. Restaurar y confirmar con `diff -q`.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(core): steer enemies toward where they think you are"
```

---

### Task 9: La espera aleatoria

**Files:**
- Modify: `src/Sonar.Core/Simulation.cs` (`FireEnemyTorpedoes`)
- Test: `tests/Sonar.Core.Tests/Combat/HesitationTests.cs` *(nuevo)*

- [ ] **Step 1: Escribir los tests**

Crear `tests/Sonar.Core.Tests/Combat/HesitationTests.cs`:

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

public class HesitationTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Contact Enemy(int id, Personality personality) => new()
    {
        Id = new ContactId(id),
        Kind = ContactKind.Enemy,
        Position = new Vec2(0.0, -0.90),
        Velocity = Vec2.Zero,
        Alive = true,
        NextFireTick = 0,
        LastFixTick = 0,
        Personality = personality,
    };

    private static List<int> FireTicks(Personality personality, int seed, int ticks = 4000)
    {
        var config = TestWorld.Config();
        var sim = new Simulation(config, new SeededRandomSource(seed),
            WorldState.CreateInitial(config, new Bearing(0), 0.0));
        sim.SetContacts(new[] { Enemy(1, personality) });

        var fired = new List<int>();
        for (var i = 0; i < ticks; i++)
        {
            foreach (var e in sim.Tick(NoCommands))
                if (e is EnemyTorpedoFired f) fired.Add(f.Tick);
            // El torpedo en el agua bloquearía el segundo disparo; se limpia para medir
            // sólo el reloj de recarga más la duda.
            sim.SetTorpedoes(Array.Empty<Torpedo>());
        }
        return fired;
    }

    [Fact]
    public void TheGapBetweenShotsIsNotAlwaysTheSame()
    {
        var ticks = FireTicks(PersonalityCatalog.Picket, seed: 7);
        Assert.True(ticks.Count >= 4, "hacen falta varios disparos para medir la variación");

        var gaps = ticks.Zip(ticks.Skip(1), (a, b) => b - a).ToList();
        Assert.True(gaps.Distinct().Count() > 1,
            $"todos los intervalos son iguales ({gaps[0]}): no hay duda ninguna");
    }

    [Fact]
    public void EveryGapStaysInsideReloadPlusTheWindow()
    {
        var config = TestWorld.Config();
        var picket = PersonalityCatalog.Picket;
        var ticks = FireTicks(picket, seed: 11);
        var gaps = ticks.Zip(ticks.Skip(1), (a, b) => b - a);

        foreach (var gap in gaps)
            Assert.InRange(gap,
                config.EnemyReloadTicks + picket.HesitationMinTicks,
                config.EnemyReloadTicks + picket.HesitationMaxTicks);
    }

    [Fact]
    public void TheGoadHesitatesLessThanThePicket()
    {
        // El ancho de la ventana es carácter: el impulsivo tira apenas puede.
        var goad = FireTicks(PersonalityCatalog.Goad, seed: 3);
        var picket = FireTicks(PersonalityCatalog.Picket, seed: 3);

        Assert.True(goad.Count > picket.Count,
            $"el GOAD disparó {goad.Count} veces y el PICKET {picket.Count}");
    }

    [Fact]
    public void TheSameSeedProducesTheSameHesitations()
    {
        Assert.Equal(FireTicks(PersonalityCatalog.Stalker, seed: 42),
                     FireTicks(PersonalityCatalog.Stalker, seed: 42));
    }

    [Fact]
    public void DifferentSeedsProduceDifferentHesitations()
    {
        Assert.NotEqual(FireTicks(PersonalityCatalog.Stalker, seed: 1),
                        FireTicks(PersonalityCatalog.Stalker, seed: 2));
    }
}
```

- [ ] **Step 2: Correrlos y verlos fallar**

Run: `dotnet test tests/Sonar.Core.Tests --filter "FullyQualifiedName~HesitationTests"`
Expected: FAIL — `TheGapBetweenShotsIsNotAlwaysTheSame` con "todos los intervalos son iguales (600)".

- [ ] **Step 3: Sortear la espera al recargar**

En `src/Sonar.Core/Simulation.cs`, dentro de `FireEnemyTorpedoes`, reemplazar la línea que fija `NextFireTick` por:

```csharp
            contacts[i] = contact with
            {
                NextFireTick = state.Tick + _config.EnemyReloadTicks + Hesitation(contact),
            };
```

Y agregar el helper junto a las otras fases:

```csharp
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
```

- [ ] **Step 4: Correr los tests**

Run: `dotnet test`
Expected: PASS, toda la suite. Los contactos sin personalidad devuelven 0 y conservan su cadencia exacta, así que `EnemyFireTests` no cambia.

- [ ] **Step 5: Mutación — que la duda muerda**

```bash
cp src/Sonar.Core/Simulation.cs /tmp/mut/
perl -pi -e 's/\+ Hesitation\(contact\),/+ 0,/' src/Sonar.Core/Simulation.cs
dotnet test tests/Sonar.Core.Tests --filter "FullyQualifiedName~HesitationTests"
```
Expected: FAIL al menos `TheGapBetweenShotsIsNotAlwaysTheSame` y `TheGoadHesitatesLessThanThePicket`. Restaurar y confirmar con `diff -q`.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(core): give each personality its own hesitation before firing"
```

---

### Task 10: El `WaveDirector` reparte personalidades

**Files:**
- Modify: `src/Sonar.Controller/WaveDirector.cs`
- Test: `tests/Sonar.Controller.Tests/WaveDirectorTests.cs`

- [ ] **Step 1: Escribir los tests**

Agregar a `tests/Sonar.Controller.Tests/WaveDirectorTests.cs`:

```csharp
    [Fact]
    public void EverySpawnedContactHasAPersonality()
    {
        var all = RunClearingInstantly(New(), 20000).SelectMany(e => e.Spawned);
        Assert.All(all, c => Assert.NotNull(c.Personality));
    }

    [Fact]
    public void TheFirstWaveIsASingleStalker()
    {
        // La oleada 1 enseña el ciclo acercarse-disparar-abrirse. Si fuera otra cosa, el
        // jugador aprende otra lección.
        var log = RunClearingInstantly(New(), 20000);
        var first = Assert.Single(log[0].Spawned);
        Assert.Equal("STALKER", first.Personality!.Name);
    }

    [Fact]
    public void TheHunterDoesNotAppearBeforeWaveFive()
    {
        var log = RunClearingInstantly(New(), 20000);

        for (var wave = 0; wave < Math.Min(4, log.Count); wave++)
            Assert.DoesNotContain(log[wave].Spawned, c => c.Personality!.Name == "HUNTER");

        // Control positivo: si nunca apareciera, el test de arriba sería vacuo.
        var everything = log.SelectMany(e => e.Spawned);
        Assert.Contains(everything, c => c.Personality!.Name == "HUNTER");
    }

    [Fact]
    public void EveryContactStartsWithAFreshFixSoItDoesNotFireBlindOnArrival()
    {
        // Sin esto, un contacto recién aparecido tendría LastFixTick = 0 y una antigüedad
        // enorme desde el primer tick: nacería apuntando a un punto absurdo.
        var log = RunClearingInstantly(New(), 20000);
        foreach (var (tick, spawned) in log)
        foreach (var contact in spawned)
            Assert.Equal(tick, contact.LastFixTick);
    }
```

- [ ] **Step 2: Correrlos y verlos fallar**

Run: `dotnet test tests/Sonar.Controller.Tests --filter "FullyQualifiedName~WaveDirectorTests"`
Expected: FAIL los cuatro nuevos.

- [ ] **Step 3: Escribir la receta**

En `src/Sonar.Controller/WaveDirector.cs`, agregar el `using Sonar.Core.Entities;` si falta, y el método:

```csharp
    /// <summary>
    /// La composición de una oleada. La dificultad deja de subir duplicando la cantidad
    /// —que es lo que convertía la oleada 4 en una pared— y pasa a subir cambiando la mezcla,
    /// que es lo que gobierna cuántos torpedos pueden estar en el agua a la vez.
    ///
    /// El contacto `i` de una oleada de `count` recibe la personalidad `i % receta.Length`.
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
```

Y dentro del bucle de `Spawn`, en el inicializador del `Contact`:

```csharp
                Personality = recipe[i % recipe.Count],
                LastFixTick = spawnTick,
```

con `var recipe = RecipeFor(Wave);` antes del bucle.

- [ ] **Step 4: Correr los tests**

Run: `dotnet test`
Expected: PASS, toda la suite.

- [ ] **Step 5: Verificar en el juego real**

Run: `dotnet build src/Sonar.Game -v q --nologo && timeout 180 godot-mono --headless --path src/Sonar.Game -- --m1-selftest`
Expected: `[SELFTEST] PASA`

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(controller): compose waves out of personalities instead of copies"
```

---

### Task 11: Cerrar el plan

- [ ] **Step 1: Verificación completa**

```bash
dotnet build -v q --nologo
dotnet test
dotnet build src/Sonar.Game -v q --nologo
timeout 180 godot-mono --headless --path src/Sonar.Game -- --m1-selftest
grep -rn "using Godot" src/Sonar.Core src/Sonar.Controller --include=*.cs | grep -v "/obj/\|/bin/" | wc -l
grep -rnE "\bDictionary<|\bHashSet<" src/Sonar.Core --include=*.cs | grep -v "/obj/\|/bin/" | wc -l
```
Expected: build limpio, toda la suite verde, selftest verde, y **0** en los dos greps.

- [ ] **Step 2: Medir el baseline nuevo**

Escribir un andamio temporal en `tests/Sonar.Controller.Tests/TempBaseline.cs` que corra el `GameController` sin input desde `START` hasta la muerte, e imprima tick de muerte, causa, oleada alcanzada y personalidad culpable. Correrlo, anotar el número, **borrar el archivo** y confirmar con `git status --short` que no quedó.

- [ ] **Step 3: Anotar los hallazgos**

Agregar una entrada a `docs/development/m1-findings.md` con la fecha, los números de tuning nuevos y el baseline medido. Registrar en particular si la letalidad quedó dentro de lo previsto por el `[RISK]` del spec §12.

- [ ] **Step 4: Actualizar `project-state.yaml`**

```yaml
  plan_7_enemy_mind: done          # 11/11 tareas
```
y cambiar `enemy_threat: done  # IA mínima: dispara recto al origen` por
`enemy_threat: done  # personalidades como datos, puntería por ruido, movimiento gobernado`.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "docs: close plan 7 with the measured baseline"
```

---

## Lo que queda para el plan 8

Fuego amigo (§7 del spec) y su chequeo de carril limpio, la embestida del RAM con su ruido de aproximación, y la telemetría por personalidad (§10). Se separan a propósito: este plan produce software jugable y medible por sí solo, y las decisiones de tuning del plan 8 se toman mejor con el baseline de la tarea 11 en la mano.
