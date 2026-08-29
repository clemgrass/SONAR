# Plan 9 — Piloto automático y la pared

> **Para agentes ejecutores:** SUB-SKILL REQUERIDA: usa `superpowers:subagent-driven-development`
> (recomendado) o `superpowers:executing-plans` para implementar este plan tarea por tarea.
> Los pasos usan casillas (`- [ ]`) para seguimiento.

**Objetivo:** un piloto automático que juega el gauntlet leyendo lo mismo que lee un jugador,
que se puede mirar corriendo en Godot y afirmar en un test, para descubrir dónde está la pared
de dificultad y validar por primera vez las oleadas 2 en adelante.

**Arquitectura:** proyecto nuevo `Sonar.Autopilot` entre Godot y el Controller. Cuatro piezas:
un rastreador de lecturas, un piloto que decide comandos a partir de un `WorldSnapshot` y nada
más, un medidor de saturación, y un bucle por tick que comparten el test y la escena. La firma
de `Decide` es el enforcement: sin acceso al `GameController`, hacer trampa no compila.

**Stack:** .NET SDK 10.0.110 con target `net9.0`, xUnit, Godot 4.7 mono (`godot-mono`).

**Spec:** [`docs/superpowers/specs/2026-08-28-autopilot-and-the-wall-design.md`](../specs/2026-08-28-autopilot-and-the-wall-design.md)

---

## Antes de empezar

Lee el contrato: [`docs/development/AGENT_CONTRACT.md`](../../development/AGENT_CONTRACT.md).
Lo que más pesa en este plan:

- **Español** en comentarios, documentación y conversación. **Inglés** en todo string que vea
  el jugador.
- **TDD de verdad:** escribe el test, **córrelo para verlo fallar**, implementa, corre la
  suite, commit. Un test que nunca se vio fallar no prueba nada.
- **Nada de `Dictionary` ni `HashSet`** en nada que se itere durante la corrida. El piloto
  tiene que ser tan determinista como la simulación: listas ordenadas por id ascendente y
  búsqueda lineal. Con un puñado de contactos no cuesta nada.
- **Los planes se cuestionan.** Si un paso pide algo que no calza con el código real,
  repórtalo. No ajustes el test hasta que pase.

Todos los comandos se corren desde la raíz del repositorio.

**Las tareas van en orden.** Las 1 a 9 se pueden ejecutar de corrido; de la 10 en adelante
cada una depende de números que sólo existen después de correr la anterior — la 11 elige una
semilla mirando el reporte de la 10, y la 12 compara conteos contra la suite que dejó la 11.

## Estructura de archivos

| Archivo | Responsabilidad |
|---|---|
| `src/Sonar.Autopilot/ContactTracker.cs` | Recuerda las dos últimas lecturas por contacto y estima velocidad |
| `src/Sonar.Autopilot/FiringSolver.cs` | Resuelve el encuentro: qué banda y qué marcación |
| `src/Sonar.Autopilot/Pilot.cs` | Decide el comando del turno: defensa, tiro o recarga |
| `src/Sonar.Autopilot/SaturationMeter.cs` | Cuenta amenazas y comandos, calcula el índice |
| `src/Sonar.Autopilot/AutopilotRun.cs` | El bucle por tick y el reporte de la corrida |
| `src/Sonar.Autopilot/RunReport.cs` | La tabla que se imprime al terminar |
| `tests/Sonar.Autopilot.Tests/Support/TestConfig.cs` | Config fija para los unitarios |
| `tests/Sonar.Autopilot.Tests/Support/TestSnapshot.cs` | Constructor de snapshots con defaults |
| `tests/Sonar.Autopilot.Tests/*.cs` | Unitarios de las cuatro piezas |
| `tests/Sonar.Controller.Tests/TheWallTests.cs` | El test de la pared, con la config real de disco |
| `src/Sonar.Game/Main.cs` | Cableado del modo demo |

Los unitarios usan una **config fija en código**, no la de disco. Un unitario que dependa de
`config/simulation.working.json` se pone rojo cada vez que se ajusta el tuning, que es
exactamente el trabajo que M1 pide hacer. Sólo el test de la pared usa la config real, porque
lo que mide es esa configuración.

---

### Tarea 1: andamiaje de los dos proyectos

**Archivos:**
- Crear: `src/Sonar.Autopilot/Sonar.Autopilot.csproj`
- Crear: `tests/Sonar.Autopilot.Tests/Sonar.Autopilot.Tests.csproj`
- Modificar: `Sonar.sln`
- Modificar: `src/Sonar.Game/Sonar.Game.csproj`

- [ ] **Paso 1: crear los dos proyectos**

```bash
dotnet new classlib -o src/Sonar.Autopilot -n Sonar.Autopilot
dotnet new xunit -o tests/Sonar.Autopilot.Tests -n Sonar.Autopilot.Tests
rm src/Sonar.Autopilot/Class1.cs tests/Sonar.Autopilot.Tests/UnitTest1.cs
```

- [ ] **Paso 2: corregir el target framework**

`dotnet new` genera `net10.0` en el SDK 10 y el juego necesita `net9.0`. Es la trampa
documentada en el §8 del contrato; hay que corregirla a mano en los dos.

```bash
sed -i 's|<TargetFramework>net10.0</TargetFramework>|<TargetFramework>net9.0</TargetFramework>|' \
  src/Sonar.Autopilot/Sonar.Autopilot.csproj tests/Sonar.Autopilot.Tests/Sonar.Autopilot.Tests.csproj
grep -n TargetFramework src/Sonar.Autopilot/Sonar.Autopilot.csproj tests/Sonar.Autopilot.Tests/Sonar.Autopilot.Tests.csproj
```

Esperado: las dos líneas dicen `net9.0`.

- [ ] **Paso 3: alinear el csproj de la biblioteca con el del Controller**

Reemplaza el contenido de `src/Sonar.Autopilot/Sonar.Autopilot.csproj` por:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <!-- El piloto conoce al Controller; el Controller no lo conoce a él. El bot es un
       instrumento de medición: la capa medida no debe poder nombrarlo. Y como toda la
       cadena, este proyecto NO referencia Godot. -->
  <ItemGroup>
    <ProjectReference Include="..\Sonar.Controller\Sonar.Controller.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Paso 4: referencias del proyecto de tests**

Agrega a `tests/Sonar.Autopilot.Tests/Sonar.Autopilot.Tests.csproj`, dentro del último
`ItemGroup` de `ProjectReference` (o crea uno):

```xml
  <ItemGroup>
    <ProjectReference Include="..\..\src\Sonar.Autopilot\Sonar.Autopilot.csproj" />
  </ItemGroup>
```

- [ ] **Paso 5: agregar los dos a la solución**

```bash
dotnet sln Sonar.sln add src/Sonar.Autopilot/Sonar.Autopilot.csproj --solution-folder src
dotnet sln Sonar.sln add tests/Sonar.Autopilot.Tests/Sonar.Autopilot.Tests.csproj --solution-folder tests
dotnet sln Sonar.sln list
```

Esperado: seis proyectos, incluidos los dos nuevos.

- [ ] **Paso 6: que el juego pueda usar el piloto**

En `src/Sonar.Game/Sonar.Game.csproj`, agrega la referencia junto a la que ya existe:

```xml
  <ItemGroup>
    <ProjectReference Include="..\Sonar.Controller\Sonar.Controller.csproj" />
    <ProjectReference Include="..\Sonar.Autopilot\Sonar.Autopilot.csproj" />
  </ItemGroup>
```

- [ ] **Paso 7: verificar que compila y que la suite sigue verde**

```bash
dotnet test Sonar.sln
```

Esperado: 352 tests pasando, cero fallidos. El proyecto nuevo compila vacío.

- [ ] **Paso 8: commit**

```bash
git add src/Sonar.Autopilot tests/Sonar.Autopilot.Tests Sonar.sln src/Sonar.Game/Sonar.Game.csproj
git commit -m "chore(autopilot): add the project that will host the measuring pilot"
```

---

### Tarea 2: el rastreador registra lecturas

`ContactTracker` es lo único que recuerda entre ticks. Guarda las **dos últimas lecturas
distintas** de cada contacto, que es lo mínimo para estimar una velocidad.

Una lectura es nueva cuando su tick de observación cambia. El snapshot no lo trae directo,
pero sí `AgeTicks`, y `SnapshotBuilder` lo calcula como `state.Tick - reading.ObservedAtTick`:
o sea que `snapshot.Tick - contact.AgeTicks` **es** el tick de observación, exacto. Comparar
ese número es más robusto que mirar si `AgeTicks` bajó.

**Archivos:**
- Crear: `tests/Sonar.Autopilot.Tests/Support/TestConfig.cs`
- Crear: `tests/Sonar.Autopilot.Tests/Support/TestSnapshot.cs`
- Crear: `tests/Sonar.Autopilot.Tests/ContactTrackerTests.cs`
- Crear: `src/Sonar.Autopilot/ContactTracker.cs`

- [ ] **Paso 1: los dos helpers de test**

`tests/Sonar.Autopilot.Tests/Support/TestConfig.cs`:

```csharp
using Sonar.Core;

namespace Sonar.Autopilot.Tests.Support;

/// <summary>
/// Config fija para los unitarios. A propósito NO se lee de disco: un unitario que dependa
/// de config/simulation.working.json se pone rojo cada vez que se ajusta el tuning, que es
/// justo el trabajo que M1 pide hacer. Los números son los de agosto de 2026, congelados.
/// </summary>
internal static class TestConfig
{
    public static SimulationConfig Build() => new()
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
        EnemyReloadTicks = 600,
        SubHitRadius = 0.045,
        PassiveHearingRadius = 0.46,
        FriendlyLaneRadius = 0.040,
        DefenseRadius = 0.070,
        DefenseCapacity = 1,
        MagazineSize = 3,
        AmmoReserve = 12,
        ReloadTicks = 180,
    };
}
```

`tests/Sonar.Autopilot.Tests/Support/TestSnapshot.cs`:

```csharp
using System;
using System.Collections.Generic;
using Sonar.Controller.Presentation;

namespace Sonar.Autopilot.Tests.Support;

/// <summary>
/// Arma un WorldSnapshot con defaults sanos. Existe porque el record tiene trece campos
/// posicionales y un test que los liste todos esconde lo que de verdad está probando.
/// </summary>
internal static class TestSnapshot
{
    public static WorldSnapshot Build(
        int tick,
        IReadOnlyList<ContactView>? contacts = null,
        IReadOnlyList<TorpedoView>? torpedoes = null,
        IReadOnlyList<EnemyTorpedoView>? enemyTorpedoes = null,
        IReadOnlyList<DefenseView>? defenses = null,
        int ammoLoaded = 3,
        int ammoReserve = 12,
        int defensesAvailable = 1,
        bool reloadInProgress = false,
        int hitsTaken = 0,
        bool destroyed = false) =>
        new(tick, 0.0, true,
            contacts ?? Array.Empty<ContactView>(),
            torpedoes ?? Array.Empty<TorpedoView>(),
            enemyTorpedoes ?? Array.Empty<EnemyTorpedoView>(),
            defenses ?? Array.Empty<DefenseView>(),
            ammoLoaded, ammoReserve, defensesAvailable,
            reloadInProgress, hitsTaken, destroyed);
}
```

- [ ] **Paso 2: escribir los tests que fallan**

`tests/Sonar.Autopilot.Tests/ContactTrackerTests.cs`:

```csharp
using Sonar.Autopilot.Tests.Support;
using Sonar.Controller.Presentation;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Autopilot.Tests;

public class ContactTrackerTests
{
    private static ContactView Contact(int id, Vec2 position, int ageTicks) =>
        new(id, position, Bearing.FromVector(position).Degrees, 'E', ageTicks);

    [Fact]
    public void OneReading_IsNotEnoughToEstimate()
    {
        var tracker = new ContactTracker();
        tracker.Observe(TestSnapshot.Build(0, new[] { Contact(100, new Vec2(0, -0.90), 0) }));

        Assert.False(tracker.TryEstimate(100, out _));
    }

    [Fact]
    public void TwoReadings_GiveTheVelocityBetweenThem()
    {
        var tracker = new ContactTracker();
        tracker.Observe(TestSnapshot.Build(0, new[] { Contact(100, new Vec2(0, -0.90), 0) }));
        tracker.Observe(TestSnapshot.Build(420, new[] { Contact(100, new Vec2(0, -0.80), 0) }));

        Assert.True(tracker.TryEstimate(100, out var estimate));
        Assert.Equal(new Vec2(0, -0.80), estimate.Position);
        Assert.Equal(420, estimate.ObservedAtTick);
        Assert.Equal(0.10 / 420.0, estimate.VelocityPerTick.Y, 12);
        Assert.Equal(0.0, estimate.VelocityPerTick.X, 12);
    }

    [Fact]
    public void TheSameReadingSeenTwice_DoesNotCountAsTwo()
    {
        // Observe corre cada tick, pero el barrido sólo refresca la lectura al cruzarla.
        // Si la misma lectura contara dos veces, la velocidad saldría cero y el bot
        // dispararía a donde el blanco estaba.
        var tracker = new ContactTracker();
        tracker.Observe(TestSnapshot.Build(0, new[] { Contact(100, new Vec2(0, -0.90), 0) }));
        tracker.Observe(TestSnapshot.Build(1, new[] { Contact(100, new Vec2(0, -0.90), 1) }));
        tracker.Observe(TestSnapshot.Build(2, new[] { Contact(100, new Vec2(0, -0.90), 2) }));

        Assert.False(tracker.TryEstimate(100, out _));

        // Control positivo: una lectura de verdad nueva sí produce estimación.
        tracker.Observe(TestSnapshot.Build(420, new[] { Contact(100, new Vec2(0, -0.80), 0) }));
        Assert.True(tracker.TryEstimate(100, out _));
    }

    [Fact]
    public void Extrapolate_ProjectsForwardFromTheLastReading()
    {
        var tracker = new ContactTracker();
        tracker.Observe(TestSnapshot.Build(0, new[] { Contact(100, new Vec2(0, -0.90), 0) }));
        tracker.Observe(TestSnapshot.Build(420, new[] { Contact(100, new Vec2(0, -0.80), 0) }));

        Assert.True(tracker.TryEstimate(100, out var estimate));
        var projected = ContactTracker.Extrapolate(estimate, 840);

        Assert.Equal(-0.70, projected.Y, 10);
    }

    [Fact]
    public void ContactsAreTrackedIndependently()
    {
        var tracker = new ContactTracker();
        tracker.Observe(TestSnapshot.Build(0, new[]
        {
            Contact(100, new Vec2(0, -0.90), 0),
            Contact(101, new Vec2(0.90, 0), 0),
        }));
        tracker.Observe(TestSnapshot.Build(420, new[]
        {
            Contact(100, new Vec2(0, -0.80), 0),
            Contact(101, new Vec2(0.85, 0), 0),
        }));

        Assert.True(tracker.TryEstimate(100, out var first));
        Assert.True(tracker.TryEstimate(101, out var second));
        Assert.Equal(0.0, first.VelocityPerTick.X, 12);
        Assert.Equal(-0.05 / 420.0, second.VelocityPerTick.X, 12);
    }
}
```

- [ ] **Paso 3: correr los tests para verlos fallar**

```bash
dotnet test tests/Sonar.Autopilot.Tests/Sonar.Autopilot.Tests.csproj
```

Esperado: no compila — `ContactTracker` no existe. Ese es el fallo que buscamos.

- [ ] **Paso 4: implementar el rastreador**

`src/Sonar.Autopilot/ContactTracker.cs`:

```csharp
using System.Collections.Generic;
using Sonar.Controller.Presentation;
using Sonar.Core.Spatial;

namespace Sonar.Autopilot;

/// <summary>Lo que el piloto sabe de un contacto: dónde estaba, cuándo, y hacia dónde va.</summary>
public readonly record struct ReadingEstimate(Vec2 Position, Vec2 VelocityPerTick, int ObservedAtTick);

/// <summary>
/// Recuerda las dos últimas lecturas distintas de cada contacto. Es lo único que el piloto
/// arrastra entre ticks, y es exactamente lo que arrastra un jugador en la cabeza: dónde vi
/// esto la vez pasada, y dónde lo veo ahora.
///
/// Lista ordenada por id y búsqueda lineal, nunca Dictionary: el orden de enumeración de un
/// Dictionary no está garantizado y el piloto tiene que ser tan determinista como la
/// simulación. Con un puñado de contactos el costo es irrelevante.
/// </summary>
public sealed class ContactTracker
{
    private sealed class Tracked
    {
        public int Id;
        public Vec2 Previous;
        public int PreviousTick;
        public Vec2 Latest;
        public int LatestTick;
        public bool HasPrevious;
    }

    private readonly List<Tracked> _tracked = new();

    /// <summary>
    /// Se llama cada tick. Una lectura es nueva cuando cambia su tick de observación, que
    /// sale de `snapshot.Tick - contact.AgeTicks` — SnapshotBuilder calcula AgeTicks como
    /// esa resta, así que la cuenta es exacta y no una heurística.
    /// </summary>
    public void Observe(WorldSnapshot snapshot)
    {
        foreach (var contact in snapshot.Contacts)
        {
            var observedAt = snapshot.Tick - contact.AgeTicks;
            var tracked = Find(contact.Id);

            if (tracked is null)
            {
                Insert(new Tracked
                {
                    Id = contact.Id,
                    Latest = contact.Position,
                    LatestTick = observedAt,
                    HasPrevious = false,
                });
                continue;
            }

            if (observedAt == tracked.LatestTick) continue;

            tracked.Previous = tracked.Latest;
            tracked.PreviousTick = tracked.LatestTick;
            tracked.Latest = contact.Position;
            tracked.LatestTick = observedAt;
            tracked.HasPrevious = true;
        }
    }

    /// <summary>Falso mientras haya una sola lectura: sin dos no hay velocidad que estimar.</summary>
    public bool TryEstimate(int contactId, out ReadingEstimate estimate)
    {
        var tracked = Find(contactId);
        if (tracked is null || !tracked.HasPrevious)
        {
            estimate = default;
            return false;
        }

        var span = tracked.LatestTick - tracked.PreviousTick;
        var delta = tracked.Latest - tracked.Previous;
        estimate = new ReadingEstimate(tracked.Latest, delta * (1.0 / span), tracked.LatestTick);
        return true;
    }

    public static Vec2 Extrapolate(ReadingEstimate estimate, double tick) =>
        estimate.Position + estimate.VelocityPerTick * (tick - estimate.ObservedAtTick);

    private Tracked? Find(int id)
    {
        foreach (var tracked in _tracked)
            if (tracked.Id == id) return tracked;
        return null;
    }

    /// <summary>Inserta manteniendo el orden por id ascendente, que es el orden de desempate.</summary>
    private void Insert(Tracked tracked)
    {
        for (var i = 0; i < _tracked.Count; i++)
        {
            if (_tracked[i].Id <= tracked.Id) continue;
            _tracked.Insert(i, tracked);
            return;
        }
        _tracked.Add(tracked);
    }
}
```

- [ ] **Paso 5: correr los tests para verlos pasar**

```bash
dotnet test tests/Sonar.Autopilot.Tests/Sonar.Autopilot.Tests.csproj
```

Esperado: 5 tests pasando.

- [ ] **Paso 6: correr la suite completa**

```bash
dotnet test Sonar.sln
```

Esperado: 357 tests pasando.

- [ ] **Paso 7: commit**

```bash
git add src/Sonar.Autopilot/ContactTracker.cs tests/Sonar.Autopilot.Tests
git commit -m "feat(autopilot): remember two readings per contact and estimate velocity"
```

---

### Tarea 3: la solución de tiro

Un disparo es `(marcación, banda)`. El torpedo sale del borde de la zona muerta, vuela recto
por esa marcación y **detona en el centro de la banda** — no donde esté el blanco. Apuntar es
resolver un encuentro: se prueban las cinco bandas, cada una con su tiempo de vuelo, y gana la
que deje al blanco más cerca del punto de detonación.

Con la config de hoy los vuelos son 2,1 / 6,3 / 10,5 / 14,7 / 18,8 s para A…E.

⚠️ **Costura de unidades.** `ReadingEstimate.VelocityPerTick` es por **tick**; todas las
velocidades de `SimulationConfig` son por **segundo**. De ahí el
`config.TorpedoSpeed * config.TickDurationSeconds` en la implementación. Un factor de 60 que
se cuele acá no rompe nada visiblemente: produce soluciones que compilan, pasan y apuntan mal.
`AClosingTarget_IsShotWhereItWillBe` es el test que lo atrapa — si la conversión estuviera
mal, elegiría otra banda.

**Archivos:**
- Crear: `tests/Sonar.Autopilot.Tests/FiringSolverTests.cs`
- Crear: `src/Sonar.Autopilot/FiringSolver.cs`

- [ ] **Paso 1: escribir los tests que fallan**

```csharp
using Sonar.Autopilot.Tests.Support;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Autopilot.Tests;

public class FiringSolverTests
{
    private static readonly Sonar.Core.SimulationConfig Config = TestConfig.Build();

    [Fact]
    public void AStationaryTarget_IsShotWhereItSits()
    {
        // En el centro de la banda C, marcación 000, quieto. El error debe ser cero.
        var estimate = new ReadingEstimate(new Vec2(0, -0.55), Vec2.Zero, 0);

        Assert.True(FiringSolver.TrySolve(Config, Config.BuildBandLayout(), estimate, 0, out var solution));
        Assert.Equal('C', solution.Band.Letter);
        Assert.Equal(0, solution.Bearing.Degrees);
        Assert.True(solution.ErrorRadius < 1e-9, $"error {solution.ErrorRadius}");
    }

    [Fact]
    public void AClosingTarget_IsShotWhereItWillBe()
    {
        // El corazón del juego. El blanco está a 0.90 — banda E — y cierra al techo de
        // 0.015 por segundo. El disparo correcto es a la banda D, porque en los 14,7 s que
        // tarda el torpedo en llegar el blanco ya no está donde se lo leyó.
        var estimate = new ReadingEstimate(new Vec2(0, -0.90), new Vec2(0, 0.015 / 60.0), 0);

        Assert.True(FiringSolver.TrySolve(Config, Config.BuildBandLayout(), estimate, 0, out var solution));
        Assert.Equal('D', solution.Band.Letter);
        Assert.Equal(0, solution.Bearing.Degrees);

        // Control positivo de que esto NO es la banda donde está el blanco ahora.
        Assert.True(Config.BuildBandLayout().TryBandAt(0.90, out var current));
        Assert.Equal('E', current.Letter);
    }

    [Fact]
    public void NoSolution_WhenEveryBandMissesByMoreThanTheBlast()
    {
        // Dentro de la zona muerta no hay banda que sirva: el centro de la banda A está a
        // 0.19 y el blanco a 0.02, así que el error mínimo supera el radio de explosión.
        var estimate = new ReadingEstimate(new Vec2(0, -0.02), Vec2.Zero, 0);

        Assert.False(FiringSolver.TrySolve(Config, Config.BuildBandLayout(), estimate, 0, out _));
    }

    [Fact]
    public void TheBearingFollowsTheTargetAround()
    {
        // Mismo caso que el primero pero al este: la marcación tiene que ser 090, no 000.
        var estimate = new ReadingEstimate(new Vec2(0.55, 0), Vec2.Zero, 0);

        Assert.True(FiringSolver.TrySolve(Config, Config.BuildBandLayout(), estimate, 0, out var solution));
        Assert.Equal(90, solution.Bearing.Degrees);
        Assert.Equal('C', solution.Band.Letter);
    }

    [Fact]
    public void TheAgeOfTheReadingIsPaidFor()
    {
        // La misma lectura, resuelta 600 ticks después de haberse tomado, tiene que apuntar
        // más adentro: el blanco siguió cerrando mientras la lectura envejecía.
        var estimate = new ReadingEstimate(new Vec2(0, -0.90), new Vec2(0, 0.015 / 60.0), 0);

        Assert.True(FiringSolver.TrySolve(Config, Config.BuildBandLayout(), estimate, 0, out var fresh));
        Assert.True(FiringSolver.TrySolve(Config, Config.BuildBandLayout(), estimate, 600, out var stale));

        Assert.True(stale.Band.Index < fresh.Band.Index,
            $"fresca {fresh.Band.Letter}, vieja {stale.Band.Letter}");
    }
}
```

- [ ] **Paso 2: correr los tests para verlos fallar**

```bash
dotnet test tests/Sonar.Autopilot.Tests/Sonar.Autopilot.Tests.csproj --filter FiringSolverTests
```

Esperado: no compila — `FiringSolver` no existe.

- [ ] **Paso 3: implementar el solucionador**

`src/Sonar.Autopilot/FiringSolver.cs`:

```csharp
using Sonar.Core;
using Sonar.Core.Spatial;

namespace Sonar.Autopilot;

/// <summary>Un disparo tecleable: la banda y la marcación en grados enteros.</summary>
public readonly record struct FiringSolution(Bearing Bearing, Band Band, double ErrorRadius);

/// <summary>
/// Resuelve el encuentro entre el torpedo y el blanco. El torpedo no persigue: sale del borde
/// de la zona muerta, vuela recto por una marcación y detona en el CENTRO de la banda pedida.
/// Así que apuntar no es "hacia dónde está" sino "qué par (marcación, banda) hace que el punto
/// de detonación y el blanco coincidan en el mismo instante".
/// </summary>
public static class FiringSolver
{
    public static bool TrySolve(
        SimulationConfig config, BandLayout layout,
        ReadingEstimate estimate, int nowTick, out FiringSolution solution)
    {
        var perTick = config.TorpedoSpeed * config.TickDurationSeconds;
        var best = default(FiringSolution);
        var bestError = double.MaxValue;

        for (var i = 0; i < layout.Count; i++)
        {
            var band = new Band(i);
            var center = layout.CenterRadius(band);

            // El torpedo nace en el borde de la zona muerta, no en el origen: por eso se resta.
            var flightTicks = (center - layout.DeadZoneRadius) / perTick;
            var predicted = ContactTracker.Extrapolate(estimate, nowTick + flightTicks);

            // La marcación se redondea a grado entero porque eso es lo que se puede teclear.
            var bearing = Bearing.FromVector(predicted);
            var detonation = bearing.ToUnitVector() * center;
            var error = Vec2.Distance(predicted, detonation);

            if (error >= bestError) continue;
            bestError = error;
            best = new FiringSolution(bearing, band, error);
        }

        solution = best;

        // Fuera del radio de explosión el disparo es una esperanza, no una solución. El
        // cargador tiene tres tubos y recargar cuesta un comando: no se gasta en esperanzas.
        return bestError < config.BlastRadius;
    }
}
```

- [ ] **Paso 4: correr los tests para verlos pasar**

```bash
dotnet test tests/Sonar.Autopilot.Tests/Sonar.Autopilot.Tests.csproj
```

Esperado: 10 tests pasando.

- [ ] **Paso 5: commit**

```bash
git add src/Sonar.Autopilot/FiringSolver.cs tests/Sonar.Autopilot.Tests/FiringSolverTests.cs
git commit -m "feat(autopilot): solve the encounter instead of aiming at the reading"
```

---

### Tarea 4: el piloto dispara

Primera decisión del piloto. La firma es lo que importa: `Decide` recibe un `WorldSnapshot` y
nada más. No recibe el `GameController`, así que no alcanza `LiveEnemyCount` ni ningún otro
dato verdadero del mundo. **Si alguien cambia esta firma, el bot puede hacer trampa.**

`Observe` va aparte de `Decide` porque se llaman con ritmos distintos: el rastreador mira cada
tick, el piloto decide sólo cuando el presupuesto se lo permite. Sin esa separación, el
rastreador se perdería lecturas cada vez que el presupuesto estuviera agotado.

**Dos desviaciones del spec, declaradas.** El §4 escribe la firma como
`Decide(WorldSnapshot, tick)`; acá es `Decide(WorldSnapshot)` a secas, porque el tick ya viene
dentro del snapshot y pasarlo dos veces sólo abre la puerta a que los dos no coincidan. Y el
§5 dice que el rastreador detecta una lectura nueva "cuando `AgeTicks` baja"; acá compara el
tick de observación, que es equivalente y exacto — ver el preámbulo de la tarea 2.

**Archivos:**
- Crear: `tests/Sonar.Autopilot.Tests/AutopilotFireTests.cs`
- Crear: `src/Sonar.Autopilot/Pilot.cs`

- [ ] **Paso 1: escribir los tests que fallan**

```csharp
using System.Collections.Generic;
using Sonar.Autopilot.Tests.Support;
using Sonar.Controller.Presentation;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Autopilot.Tests;

public class AutopilotFireTests
{
    private static ContactView Contact(int id, Vec2 position) =>
        new(id, position, Bearing.FromVector(position).Degrees, 'E', 0);

    /// <summary>
    /// Piloto que ya vio dos lecturas de cada contacto, que es el mínimo para tener
    /// velocidad. La segunda observación va en el tick 420 — una vuelta del haz a 7 s.
    /// </summary>
    private static Pilot Tracking(IReadOnlyList<ContactView> first, IReadOnlyList<ContactView> second)
    {
        var pilot = new Pilot(TestConfig.Build());
        pilot.Observe(TestSnapshot.Build(0, first));
        pilot.Observe(TestSnapshot.Build(420, second));
        return pilot;
    }

    [Fact]
    public void WithOneReadingOnly_ItHoldsFire()
    {
        // Sin velocidad estimada no hay solución posible: disparar sería tirarle a donde
        // el blanco estaba. Un jugador está en la misma situación tras un solo barrido.
        var pilot = new Pilot(TestConfig.Build());
        var snapshot = TestSnapshot.Build(0, new[] { Contact(100, new Vec2(0, -0.90)) });
        pilot.Observe(snapshot);

        Assert.Null(pilot.Decide(snapshot));
    }

    [Fact]
    public void WithASolution_ItTypesTheCoordinate()
    {
        // El blanco cierra de 0.90 a 0.795 en una vuelta del haz. En los 10,5 s que tarda
        // el torpedo en llegar a la banda C el blanco ya estará ahí: el disparo es C000
        // aunque en este instante el contacto esté en la banda D.
        var first = new[] { Contact(100, new Vec2(0, -0.90)) };
        var second = new[] { Contact(100, new Vec2(0, -0.795)) };
        var pilot = Tracking(first, second);

        Assert.Equal("C000", pilot.Decide(TestSnapshot.Build(420, second)));

        // Control positivo de que la banda elegida no es donde está el contacto ahora.
        Assert.True(TestConfig.Build().BuildBandLayout().TryBandAt(0.795, out var current));
        Assert.Equal('D', current.Letter);
    }

    [Fact]
    public void TheNearestContactGoesFirst()
    {
        // Dos blancos con solución. Gana el de menor radio: el que ya está encima.
        var first = new[] { Contact(100, new Vec2(0, -0.90)), Contact(101, new Vec2(0.55, 0)) };
        var second = new[] { Contact(100, new Vec2(0, -0.795)), Contact(101, new Vec2(0.55, 0)) };
        var pilot = Tracking(first, second);

        Assert.Equal("C090", pilot.Decide(TestSnapshot.Build(420, second)));
    }

    [Fact]
    public void EmptyTubesAndEmptyReserve_MeanNoCommandAtAll()
    {
        var first = new[] { Contact(100, new Vec2(0, -0.90)) };
        var second = new[] { Contact(100, new Vec2(0, -0.795)) };
        var pilot = Tracking(first, second);

        Assert.Null(pilot.Decide(TestSnapshot.Build(420, second, ammoLoaded: 0, ammoReserve: 0)));
    }

    [Fact]
    public void WhileReloading_ItDoesNotFire()
    {
        // Un FIRE con la recarga en curso lo rechaza la simulación. Un comando rechazado no
        // es una respuesta: emitirlo inflaría el denominador del índice de saturación.
        var first = new[] { Contact(100, new Vec2(0, -0.90)) };
        var second = new[] { Contact(100, new Vec2(0, -0.795)) };
        var pilot = Tracking(first, second);

        Assert.Null(pilot.Decide(TestSnapshot.Build(420, second, reloadInProgress: true)));
    }

    [Fact]
    public void AStaleReadingIsNotWorthShootingAt()
    {
        // Un contacto sale de `snapshot.Contacts` sólo al morir, nunca por irse del alcance,
        // y el barrido no refresca nada más allá del radio 1.0. O sea que una lectura puede
        // envejecer sin cota, y extrapolar linealmente desde ella produce una solución que
        // el solucionador acepta con toda confianza: el error que mide es contra la posición
        // PREDICHA, y una predicción rancia es internamente consistente y falsa.
        var first = new[] { Contact(100, new Vec2(0, -0.90)) };
        var second = new[] { Contact(100, new Vec2(0, -0.795)) };
        var pilot = Tracking(first, second);

        // Tres barridos de antigüedad: 1580 ticks contra un tope de 1260.
        var stale = new[] { new ContactView(100, new Vec2(0, -0.795), 0, 'D', 1580) };
        Assert.Null(pilot.Decide(TestSnapshot.Build(2000, stale)));

        // Control positivo: la misma lectura, aún vigente, sí produce un disparo. En el tick
        // 2000 el blanco ya cerró hasta 0.40, así que la banda correcta es la B y no la C.
        var fresh = new[] { new ContactView(100, new Vec2(0, -0.795), 0, 'D', 100) };
        Assert.Equal("B000", pilot.Decide(TestSnapshot.Build(2000, fresh)));
    }

    [Fact]
    public void ItDoesNotFireTwiceAtTheSameSolution()
    {
        // Con tres tubos y un vuelo de diez segundos, sin esta regla el piloto vacía el
        // cargador entero contra un solo contacto y se pasa nueve segundos recargando.
        var first = new[] { Contact(100, new Vec2(0, -0.90)) };
        var second = new[] { Contact(100, new Vec2(0, -0.795)) };
        var pilot = Tracking(first, second);

        var inFlight = new[] { new TorpedoView(1, new Vec2(0, -0.30), 0, 0.55) };
        Assert.Null(pilot.Decide(TestSnapshot.Build(420, second, torpedoes: inFlight)));

        // Control positivo: el mismo torpedo por otra marcación no bloquea nada.
        var elsewhere = new[] { new TorpedoView(1, new Vec2(0, 0.30), 180, 0.55) };
        Assert.Equal("C000", pilot.Decide(TestSnapshot.Build(420, second, torpedoes: elsewhere)));
    }
}
```

- [ ] **Paso 2: correr los tests para verlos fallar**

```bash
dotnet test tests/Sonar.Autopilot.Tests/Sonar.Autopilot.Tests.csproj --filter AutopilotFireTests
```

Esperado: no compila — `Pilot` no existe.

- [ ] **Paso 3: implementar el piloto, por ahora sólo el disparo**

`src/Sonar.Autopilot/Pilot.cs`:

```csharp
using System;
using Sonar.Controller.Presentation;
using Sonar.Core;
using Sonar.Core.Spatial;

namespace Sonar.Autopilot;

/// <summary>
/// El piloto. Decide un comando por turno leyendo <b>únicamente</b> el snapshot: lecturas
/// envejecidas y con gating, lo mismo que ve el jugador.
///
/// Se llama Pilot y no Autopilot porque `Autopilot` ya es un segmento del namespace
/// `Sonar.Autopilot`: desde `Sonar.Autopilot.Tests` o desde `Sonar.Game`, el nombre a secas
/// resuelve al namespace y no al tipo, y no compila (CS0118).
///
/// Que no reciba el GameController no es estilo: es el enforcement. Con él a mano alcanzaría
/// LiveEnemyCount, que cuenta enemigos que el jugador quizá nunca leyó, y la pared que midiera
/// este bot sería la de otro juego. Con esta firma, hacer trampa no compila — el mismo
/// mecanismo por el que ContactView impide que la presentación dibuje la verdad.
/// </summary>
public sealed class Pilot
{
    /// <summary>
    /// Separación por debajo de la cual dos disparos se consideran el mismo. Tres grados es
    /// más que el redondeo a grado entero y menos que la separación entre dos contactos que
    /// valga la pena atacar por separado.
    /// </summary>
    private const double EngagedSeparationDegrees = 3.0;

    /// <summary>
    /// Barridos de antigüedad tras los cuales una lectura deja de servir para apuntar.
    ///
    /// Hace falta porque el solucionador no puede detectar el problema solo: mide el error
    /// contra la posición PREDICHA, y una predicción hecha desde una lectura rancia es
    /// internamente consistente y falsa. Un contacto sale de `snapshot.Contacts` sólo al
    /// morir —nunca por salirse del alcance— y el barrido no refresca nada más allá del
    /// radio 1.0, así que sin esta compuerta el piloto le dispara con confianza a un
    /// fantasma de hace un minuto.
    ///
    /// Tres barridos: dos son el mínimo para tener velocidad, así que con menos se
    /// rechazarían blancos recién adquiridos.
    /// </summary>
    private const double MaxReadingAgeSweeps = 3.0;

    private readonly SimulationConfig _config;
    private readonly BandLayout _layout;
    private readonly ContactTracker _tracker = new();
    private readonly double _maxReadingAgeTicks;

    public Pilot(SimulationConfig config)
    {
        _config = config;
        _layout = config.BuildBandLayout();
        _maxReadingAgeTicks =
            MaxReadingAgeSweeps * config.SweepPeriodSeconds / config.TickDurationSeconds;
    }

    /// <summary>
    /// Se llama CADA tick. Va aparte de Decide porque los dos tienen ritmos distintos: el
    /// rastreador no puede perderse lecturas por estar el presupuesto agotado.
    /// </summary>
    public void Observe(WorldSnapshot snapshot) => _tracker.Observe(snapshot);

    /// <summary>Comando del turno, o null si no hay nada que valga la pena teclear.</summary>
    public string? Decide(WorldSnapshot snapshot) => DecideFire(snapshot);

    private string? DecideFire(WorldSnapshot snapshot)
    {
        // Comandos que la simulación rechazaría no se emiten. Un rechazo no es una respuesta,
        // y contarlo inflaría el denominador del índice de saturación hasta hacerlo mentir.
        if (snapshot.ReloadInProgress || snapshot.AmmoLoaded <= 0) return null;

        var bestRadius = double.MaxValue;
        var best = default(FiringSolution);
        var found = false;

        // snapshot.Contacts viene ordenada por id ascendente —SonarState garantiza el orden
        // de las lecturas— y la comparación de abajo es estricta. Con eso el desempate queda
        // por id ascendente, como pide el §3 del contrato.
        foreach (var contact in snapshot.Contacts)
        {
            if (contact.AgeTicks > _maxReadingAgeTicks) continue;
            if (!_tracker.TryEstimate(contact.Id, out var estimate)) continue;
            if (!FiringSolver.TrySolve(_config, _layout, estimate, snapshot.Tick, out var solution)) continue;
            if (AlreadyEngaged(snapshot, solution)) continue;

            var radius = ContactTracker.Extrapolate(estimate, snapshot.Tick).Length;
            if (radius >= bestRadius) continue;

            bestRadius = radius;
            best = solution;
            found = true;
        }

        return found ? $"{best.Band.Letter}{best.Bearing.Degrees:D3}" : null;
    }

    /// <summary>¿Ya hay un torpedo propio volando a esta misma solución?</summary>
    private bool AlreadyEngaged(WorldSnapshot snapshot, FiringSolution solution)
    {
        var run = _layout.CenterRadius(solution.Band);
        foreach (var torpedo in snapshot.Torpedoes)
        {
            if (Math.Abs(torpedo.RunToRadius - run) > 1e-9) continue;
            if (Bearing.Separation(torpedo.BearingDegrees, solution.Bearing.Degrees)
                <= EngagedSeparationDegrees) return true;
        }
        return false;
    }
}
```

- [ ] **Paso 4: correr los tests para verlos pasar**

```bash
dotnet test tests/Sonar.Autopilot.Tests/Sonar.Autopilot.Tests.csproj
```

Esperado: 17 tests pasando.

- [ ] **Paso 5: commit**

```bash
git add src/Sonar.Autopilot/Pilot.cs tests/Sonar.Autopilot.Tests/AutopilotFireTests.cs
git commit -m "feat(autopilot): fire at the extrapolated target, one tube at a time"
```

---

### Tarea 5: el piloto recarga

`RELOAD` mueve **un solo proyectil** (`room = Math.Min(1, MagazineSize - AmmoLoaded)` en
`Simulation.AdvanceReload`): llenar tres tubos cuesta tres comandos y nueve segundos. Con
presupuesto, recargar compite con disparar, y ese costo tiene que quedar medido y no escondido
— es la pregunta 7 de M1.

Por eso la recarga es **oportunista**: si en un turno no hay nada que disparar y falta munición
en los tubos, se recarga. Es la jugada correcta y además hace aparecer el costo real.

**Archivos:**
- Crear: `tests/Sonar.Autopilot.Tests/AutopilotReloadTests.cs`
- Modificar: `src/Sonar.Autopilot/Pilot.cs`

- [ ] **Paso 1: escribir los tests que fallan**

```csharp
using Sonar.Autopilot.Tests.Support;
using Sonar.Controller.Presentation;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Autopilot.Tests;

public class AutopilotReloadTests
{
    private static Pilot Idle() => new(TestConfig.Build());

    [Fact]
    public void EmptyTubes_MeanReload()
    {
        var pilot = Idle();
        Assert.Equal("RELOAD", pilot.Decide(TestSnapshot.Build(0, ammoLoaded: 0)));
    }

    [Fact]
    public void AHalfFullMagazine_IsToppedUpWhenThereIsNothingToShoot()
    {
        var pilot = Idle();
        Assert.Equal("RELOAD", pilot.Decide(TestSnapshot.Build(0, ammoLoaded: 2)));
    }

    [Fact]
    public void FullTubes_NeedNothing()
    {
        var pilot = Idle();
        Assert.Null(pilot.Decide(TestSnapshot.Build(0, ammoLoaded: 3)));
    }

    [Fact]
    public void ASecondReloadIsNotTypedWhileOneIsRunning()
    {
        // La simulación lo rechazaría en silencio: `if (ReloadInProgress) return state`.
        var pilot = Idle();
        Assert.Null(pilot.Decide(TestSnapshot.Build(0, ammoLoaded: 0, reloadInProgress: true)));
    }

    [Fact]
    public void WithNoReserve_ThereIsNothingToLoad()
    {
        var pilot = Idle();
        Assert.Null(pilot.Decide(TestSnapshot.Build(0, ammoLoaded: 0, ammoReserve: 0)));
    }

    [Fact]
    public void ShootingComesBeforeToppingUp()
    {
        // La recarga es oportunista, no codiciosa: con un blanco resuelto y tubos a medias,
        // el turno es para el disparo.
        var pilot = new Pilot(TestConfig.Build());
        var first = new[] { new ContactView(100, new Vec2(0, -0.90), 0, 'E', 0) };
        var second = new[] { new ContactView(100, new Vec2(0, -0.795), 0, 'D', 0) };
        pilot.Observe(TestSnapshot.Build(0, first));
        pilot.Observe(TestSnapshot.Build(420, second));

        Assert.Equal("C000", pilot.Decide(TestSnapshot.Build(420, second, ammoLoaded: 2)));
    }
}
```

- [ ] **Paso 2: correr los tests para verlos fallar**

```bash
dotnet test tests/Sonar.Autopilot.Tests/Sonar.Autopilot.Tests.csproj --filter AutopilotReloadTests
```

Esperado: fallan los dos primeros y el sexto pasa; los que esperan `null` pasan por vacuidad
porque `Decide` todavía sólo dispara. **Eso es normal y es la razón por la que existen los dos
primeros:** son el control positivo de los otros tres.

- [ ] **Paso 3: agregar la recarga**

En `src/Sonar.Autopilot/Pilot.cs`, cambia `Decide` y agrega el método:

```csharp
    public string? Decide(WorldSnapshot snapshot) =>
        DecideFire(snapshot) ?? DecideReload(snapshot);

    /// <summary>
    /// Oportunista: rellena cuando no hay nada mejor que hacer con el turno. RELOAD mueve un
    /// solo proyectil, así que llenar el cargador son tres comandos — y esos tres comandos
    /// compitiendo con los disparos es exactamente lo que la pregunta 7 de M1 quiere medir.
    /// </summary>
    private string? DecideReload(WorldSnapshot snapshot)
    {
        if (snapshot.ReloadInProgress) return null;
        if (snapshot.AmmoReserve <= 0) return null;
        if (snapshot.AmmoLoaded >= _config.MagazineSize) return null;
        return "RELOAD";
    }
```

- [ ] **Paso 4: correr los tests para verlos pasar**

```bash
dotnet test Sonar.sln
```

Esperado: 23 tests en total en el proyecto del piloto, suite completa en verde.

- [ ] **Paso 5: commit**

```bash
git add src/Sonar.Autopilot/Pilot.cs tests/Sonar.Autopilot.Tests/AutopilotReloadTests.cs
git commit -m "feat(autopilot): top up the tubes when the turn has nothing better to do"
```

---

### Tarea 6: el piloto se defiende

Los torpedos enemigos se ven completos en el snapshot: se oyen por ruido, no por barrido. Van
rectos al origen con marcación constante y **no reciben la deriva del mundo** — sólo los
contactos la reciben —, así que una defensa quieta sobre esa marcación intercepta seguro.

Dos reglas que no son obvias:

- **La defensa va primero que el disparo.** Un torpedo que va a llegar en diez segundos vale
  más que un tiro que tarda diez en llegar.
- **Nunca en el carril propio.** Una defensa mata los torpedos propios (§5 del contrato).
  Cambiar un tiro propio por uno enemigo es mal negocio cuando recargar cuesta un comando.

**Archivos:**
- Crear: `tests/Sonar.Autopilot.Tests/AutopilotDefenseTests.cs`
- Modificar: `src/Sonar.Autopilot/Pilot.cs`

- [ ] **Paso 1: escribir los tests que fallan**

```csharp
using Sonar.Autopilot.Tests.Support;
using Sonar.Controller.Presentation;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Autopilot.Tests;

public class AutopilotDefenseTests
{
    // A 0.40 de radio y 0.03 por segundo, el impacto llega en 13,3 s: dentro de la ventana.
    private static readonly EnemyTorpedoView[] Inbound =
        { new(1, new Vec2(0.40, 0)) };

    // A 0.90 el impacto está a 30 s: todavía no se gasta la única defensa.
    private static readonly EnemyTorpedoView[] FarInbound =
        { new(1, new Vec2(0.90, 0)) };

    [Fact]
    public void AnInboundInsideTheWindow_GetsTheDefense()
    {
        // Banda B: es la más externa cuyo centro (0.37) el torpedo todavía no cruzó.
        var pilot = new Pilot(TestConfig.Build());
        Assert.Equal("DEF B090", pilot.Decide(TestSnapshot.Build(0, enemyTorpedoes: Inbound)));
    }

    [Fact]
    public void AFarInbound_IsLeftAlone()
    {
        var pilot = new Pilot(TestConfig.Build());
        Assert.Null(pilot.Decide(TestSnapshot.Build(0, enemyTorpedoes: FarInbound)));

        // Control positivo: el mismo torpedo, ya cerca, sí produce una defensa.
        Assert.Equal("DEF B090", pilot.Decide(TestSnapshot.Build(0, enemyTorpedoes: Inbound)));
    }

    [Fact]
    public void WithOneAlreadyInTheWater_ThereIsNothingToDeploy()
    {
        var pilot = new Pilot(TestConfig.Build());
        Assert.Null(pilot.Decide(
            TestSnapshot.Build(0, enemyTorpedoes: Inbound, defensesAvailable: 0)));
    }

    [Fact]
    public void ItDoesNotPutTheDefenseInItsOwnLane()
    {
        // Una defensa mata los torpedos propios. Cambiar un tiro propio por uno enemigo es
        // mal negocio cuando recargar cuesta un comando y tres segundos.
        var pilot = new Pilot(TestConfig.Build());
        var ownLane = new[] { new TorpedoView(2, new Vec2(0.30, 0), 90, 0.55) };

        Assert.Null(pilot.Decide(
            TestSnapshot.Build(0, torpedoes: ownLane, enemyTorpedoes: Inbound)));

        // Control positivo: el mismo torpedo propio por la marcación opuesta no estorba.
        var otherLane = new[] { new TorpedoView(2, new Vec2(-0.30, 0), 270, 0.55) };
        Assert.Equal("DEF B090", pilot.Decide(
            TestSnapshot.Build(0, torpedoes: otherLane, enemyTorpedoes: Inbound)));
    }

    [Fact]
    public void DefendingComesBeforeShooting()
    {
        // Un blanco resuelto y un torpedo encima: el turno es para la defensa.
        var pilot = new Pilot(TestConfig.Build());
        var first = new[] { new ContactView(100, new Vec2(0, -0.90), 0, 'E', 0) };
        var second = new[] { new ContactView(100, new Vec2(0, -0.795), 0, 'D', 0) };
        pilot.Observe(TestSnapshot.Build(0, first));
        pilot.Observe(TestSnapshot.Build(420, second));

        Assert.Equal("DEF B090", pilot.Decide(
            TestSnapshot.Build(420, second, enemyTorpedoes: Inbound)));

        // Control positivo: sin el entrante, ese mismo turno era un disparo.
        Assert.Equal("C000", pilot.Decide(TestSnapshot.Build(420, second)));
    }
}
```

- [ ] **Paso 2: correr los tests para verlos fallar**

```bash
dotnet test tests/Sonar.Autopilot.Tests/Sonar.Autopilot.Tests.csproj --filter AutopilotDefenseTests
```

Esperado: fallan los que esperan `"DEF B090"`.

- [ ] **Paso 3: agregar la defensa**

En `src/Sonar.Autopilot/Pilot.cs`, agrega la constante, cambia `Decide` y agrega los tres
métodos:

```csharp
    /// <summary>
    /// Ventana de despliegue. Con una sola defensa a la vez, gastarla contra un entrante
    /// lejano deja al submarino sin red durante todo el tiempo que ese torpedo tarde en
    /// llegar. Quince segundos es corto para no malgastarla y largo para que todavía quede
    /// un centro de banda por delante del torpedo donde ponerla.
    /// </summary>
    private const double DeployWithinSeconds = 15.0;
```

```csharp
    public string? Decide(WorldSnapshot snapshot) =>
        DecideDefense(snapshot) ?? DecideFire(snapshot) ?? DecideReload(snapshot);
```

```csharp
    /// <summary>
    /// Va antes que el disparo: un torpedo que llega en diez segundos vale más que un tiro
    /// que tarda diez en llegar.
    /// </summary>
    private string? DecideDefense(WorldSnapshot snapshot)
    {
        if (snapshot.DefensesAvailable <= 0) return null;

        // El más cercano es el que llega primero. Empate por id ascendente: la lista viene
        // ordenada y la comparación es estricta.
        var nearest = default(Vec2);
        var nearestRadius = double.MaxValue;
        foreach (var torpedo in snapshot.EnemyTorpedoes)
        {
            var radius = torpedo.Position.Length;
            if (radius >= nearestRadius) continue;
            nearestRadius = radius;
            nearest = torpedo.Position;
        }

        if (nearestRadius == double.MaxValue) return null;

        var secondsToImpact = nearestRadius / _config.EnemyTorpedoSpeed;
        if (secondsToImpact > DeployWithinSeconds) return null;

        if (!TryInterceptBand(nearestRadius, out var band)) return null;

        var bearing = Bearing.FromVector(nearest);
        if (LaneIsOccupiedByOwnTorpedo(snapshot, bearing, band)) return null;

        return $"DEF {band.Letter}{bearing.Degrees:D3}";
    }

    /// <summary>
    /// La banda más externa cuyo centro el torpedo todavía no cruzó. Poner la defensa más
    /// afuera sería ponerla detrás del torpedo, donde nunca va a pasar.
    /// </summary>
    private bool TryInterceptBand(double radius, out Band band)
    {
        band = default;
        var found = false;
        for (var i = 0; i < _layout.Count; i++)
        {
            var candidate = new Band(i);
            if (_layout.CenterRadius(candidate) >= radius) break;
            band = candidate;
            found = true;
        }
        return found;
    }

    /// <summary>
    /// Una defensa mata los torpedos propios (§5 del contrato). Se compara el punto de la
    /// defensa contra el punto donde el carril de cada torpedo propio cruza ese mismo radio.
    /// El margen es el doble del radio de defensa: con menos, se roza.
    /// </summary>
    private bool LaneIsOccupiedByOwnTorpedo(WorldSnapshot snapshot, Bearing bearing, Band band)
    {
        var center = _layout.CenterRadius(band);
        var defense = bearing.ToUnitVector() * center;

        foreach (var torpedo in snapshot.Torpedoes)
        {
            // Si detona antes de ese radio nunca se cruzan.
            if (torpedo.RunToRadius < center - _config.DefenseRadius) continue;

            var lane = new Bearing(torpedo.BearingDegrees).ToUnitVector() * center;
            if (Vec2.Distance(lane, defense) < _config.DefenseRadius * 2) return true;
        }
        return false;
    }
```

- [ ] **Paso 4: correr los tests para verlos pasar**

```bash
dotnet test Sonar.sln
```

Esperado: suite completa en verde.

- [ ] **Paso 5: commit**

```bash
git add src/Sonar.Autopilot/Pilot.cs tests/Sonar.Autopilot.Tests/AutopilotDefenseTests.cs
git commit -m "feat(autopilot): intercept the nearest inbound, never in its own lane"
```

---

### Tarea 7: el medidor de saturación

> **S = amenazas nuevas en los últimos 30 s ÷ comandos emitidos en los últimos 30 s**

Dos conteos sobre la misma ventana y ningún modelo en medio. El denominador ya lleva adentro
las dos esperas que importan —el presupuesto y el tiempo que el barrido hace esperar por una
lectura utilizable—, así que no hay que modelarlas: quedan medidas.

Detalle de implementación que importa: los ids de entidad **ascienden siempre**
(`WorldState.TakeEntityId`), así que "amenaza nueva" es "id mayor que el mayor visto". Un solo
entero, sin listas de ids que crezcan durante veinte mil ticks.

**Archivos:**
- Crear: `tests/Sonar.Autopilot.Tests/SaturationMeterTests.cs`
- Crear: `src/Sonar.Autopilot/SaturationMeter.cs`

- [ ] **Paso 1: escribir los tests que fallan**

```csharp
using Sonar.Autopilot.Tests.Support;
using Sonar.Controller.Presentation;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Autopilot.Tests;

public class SaturationMeterTests
{
    private static SaturationMeter Meter() => new(TestConfig.Build());

    private static EnemyTorpedoView[] Torpedoes(params int[] ids)
    {
        var views = new EnemyTorpedoView[ids.Length];
        for (var i = 0; i < ids.Length; i++) views[i] = new EnemyTorpedoView(ids[i], new Vec2(0.5, 0));
        return views;
    }

    [Fact]
    public void WithNoThreats_ThereIsNoPressure()
    {
        var meter = Meter();
        meter.NoteCommand(10);
        meter.Observe(TestSnapshot.Build(100));

        Assert.Equal(0.0, meter.IndexAt(100));
    }

    [Fact]
    public void MoreThreatsThanCommands_PushTheIndexAboveOne()
    {
        var meter = Meter();
        meter.Observe(TestSnapshot.Build(100, enemyTorpedoes: Torpedoes(1)));
        meter.Observe(TestSnapshot.Build(200, enemyTorpedoes: Torpedoes(1, 2)));
        meter.Observe(TestSnapshot.Build(300, enemyTorpedoes: Torpedoes(1, 2, 3)));
        meter.NoteCommand(150);
        meter.NoteCommand(250);

        Assert.Equal(1.5, meter.IndexAt(300), 10);
    }

    [Fact]
    public void MoreCommandsThanThreats_KeepItBelowOne()
    {
        var meter = Meter();
        meter.Observe(TestSnapshot.Build(100, enemyTorpedoes: Torpedoes(1)));
        meter.NoteCommand(110);
        meter.NoteCommand(120);
        meter.NoteCommand(130);
        meter.NoteCommand(140);

        Assert.Equal(0.25, meter.IndexAt(200), 10);
    }

    [Fact]
    public void TheSameTorpedoSeenEveryTick_IsOneThreat()
    {
        // El torpedo sigue en el agua durante cientos de ticks. Si cada tick contara como
        // una amenaza nueva, el índice mediría cuánto dura un torpedo y no cuántos llegan.
        var meter = Meter();
        for (var tick = 100; tick < 400; tick++)
            meter.Observe(TestSnapshot.Build(tick, enemyTorpedoes: Torpedoes(1)));
        meter.NoteCommand(150);

        Assert.Equal(1.0, meter.IndexAt(400), 10);
    }

    [Fact]
    public void ThreatsWithNoAnswer_DoNotDivideByZero()
    {
        // Tres amenazas y ni un comando es el caso "me están matando y no alcanzo a teclear".
        // Se reporta como un número grande y finito, no como infinito: un infinito rompe
        // las comparaciones del test de la pared y no dice nada más que un tres.
        var meter = Meter();
        meter.Observe(TestSnapshot.Build(100, enemyTorpedoes: Torpedoes(1, 2, 3)));

        Assert.Equal(3.0, meter.IndexAt(100), 10);
    }

    [Fact]
    public void WhatHappenedBeforeTheWindow_DoesNotCount()
    {
        // La ventana son 1800 ticks. Una amenaza del tick 0 mirada desde el 2000 ya salió.
        var stale = Meter();
        stale.Observe(TestSnapshot.Build(0, enemyTorpedoes: Torpedoes(1)));
        stale.Observe(TestSnapshot.Build(2000));

        Assert.Equal(0.0, stale.IndexAt(2000));

        // Control positivo: la misma amenaza dentro de la ventana sí cuenta.
        var fresh = Meter();
        fresh.Observe(TestSnapshot.Build(500, enemyTorpedoes: Torpedoes(1)));
        fresh.Observe(TestSnapshot.Build(2000));

        Assert.Equal(1.0, fresh.IndexAt(2000), 10);
    }
}
```

- [ ] **Paso 2: correr los tests para verlos fallar**

```bash
dotnet test tests/Sonar.Autopilot.Tests/Sonar.Autopilot.Tests.csproj --filter SaturationMeterTests
```

Esperado: no compila — `SaturationMeter` no existe.

- [ ] **Paso 3: implementar el medidor**

`src/Sonar.Autopilot/SaturationMeter.cs`:

```csharp
using System;
using System.Collections.Generic;
using Sonar.Controller.Presentation;
using Sonar.Core;

namespace Sonar.Autopilot;

/// <summary>
/// Mide si el jugador puede seguirle el ritmo a lo que le llega:
///
///     S = amenazas nuevas en la ventana / comandos emitidos en la ventana
///
/// Dos conteos y ningún modelo. El denominador ya contiene las dos esperas que importan —el
/// presupuesto de composición y el tiempo que el barrido hace esperar por una lectura— así
/// que no hace falta modelarlas: quedan medidas.
///
/// Está sesgado A LA BAJA a propósito: un RELOAD cuenta como comando aunque no atienda a
/// ninguna amenaza, y eso infla el denominador. Si aun así S cruza 1, cruzó de verdad.
/// </summary>
public sealed class SaturationMeter
{
    /// <summary>
    /// Unos cuatro barridos y más de un ciclo de fuego enemigo. Más corta salta con cada
    /// torpedo; más larga borra el momento en que la cosa se rompe.
    /// </summary>
    public const double WindowSeconds = 30.0;

    private readonly int _windowTicks;
    private readonly List<int> _threatTicks = new();
    private readonly List<int> _commandTicks = new();

    /// <summary>
    /// Los ids de entidad ascienden siempre (WorldState.TakeEntityId), así que un torpedo
    /// con id mayor al mayor visto es necesariamente nuevo. Un entero en vez de una lista de
    /// ids que crecería durante toda la corrida.
    /// </summary>
    private int _highestTorpedoId = -1;

    public SaturationMeter(SimulationConfig config) =>
        _windowTicks = (int)Math.Round(WindowSeconds / config.TickDurationSeconds);

    public void Observe(WorldSnapshot snapshot)
    {
        foreach (var torpedo in snapshot.EnemyTorpedoes)
        {
            if (torpedo.Id <= _highestTorpedoId) continue;
            _highestTorpedoId = torpedo.Id;
            _threatTicks.Add(snapshot.Tick);
        }

        Prune(_threatTicks, snapshot.Tick);
        Prune(_commandTicks, snapshot.Tick);
    }

    public void NoteCommand(int tick) => _commandTicks.Add(tick);

    public double IndexAt(int tick)
    {
        var threats = CountIn(_threatTicks, tick);
        if (threats == 0) return 0.0;

        // Max(1, …) en vez de infinito: "tres amenazas y ningún comando" se reporta como 3,
        // que es un número comparable y dice lo mismo.
        return threats / (double)Math.Max(1, CountIn(_commandTicks, tick));
    }

    private int CountIn(List<int> ticks, int now)
    {
        var floor = now - _windowTicks;
        var count = 0;
        foreach (var tick in ticks)
            if (tick > floor && tick <= now) count++;
        return count;
    }

    /// <summary>Las listas se llenan en orden ascendente, así que se poda por el frente.</summary>
    private void Prune(List<int> ticks, int now)
    {
        var floor = now - _windowTicks;
        var drop = 0;
        while (drop < ticks.Count && ticks[drop] <= floor) drop++;
        if (drop > 0) ticks.RemoveRange(0, drop);
    }
}
```

- [ ] **Paso 4: correr los tests para verlos pasar**

```bash
dotnet test Sonar.sln
```

Esperado: suite completa en verde.

- [ ] **Paso 5: commit**

```bash
git add src/Sonar.Autopilot/SaturationMeter.cs tests/Sonar.Autopilot.Tests/SaturationMeterTests.cs
git commit -m "feat(autopilot): measure whether threats outrun the answers"
```

---

### Tarea 8: el bucle

La pieza que hace que **lo que se mira sea lo que el test afirma**: una sola implementación
del bucle, usada por el test y por la escena de Godot.

Dos cosas que parecen detalles y no lo son:

- **Con el submarino destruido el tick queda congelado.** `Simulation.Tick` devuelve temprano
  con `if (State.Sub.Destroyed)` y no actualiza el estado. Un bucle que espere a que el tick
  avance no termina nunca. La guarda `if (Finished) return` no es cortesía: es lo que impide
  que la suite se cuelgue para siempre.
- **`Advance` con exactamente un período ejecuta cero o un tick, nunca dos.** El acumulador no
  puede juntar dos períodos con un solo aporte de uno. Pero la deriva de punto flotante hace
  que a veces ejecute cero, y ahí el piloto decidiría dos veces sobre el mismo tick.

**Archivos:**
- Crear: `tests/Sonar.Autopilot.Tests/AutopilotRunTests.cs`
- Crear: `src/Sonar.Autopilot/AutopilotRun.cs`

- [ ] **Paso 1: escribir los tests que fallan**

```csharp
using System.Collections.Generic;
using Sonar.Autopilot.Tests.Support;
using Sonar.Controller;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Autopilot.Tests;

public class AutopilotRunTests
{
    private const int Seed = 20260828;
    private const double SubSpeed = 0.012;

    private static (GameController Controller, AutopilotRun Run) Build(double pace = 1.5)
    {
        var config = TestConfig.Build();
        var controller = new GameController(config, Seed, new Bearing(0), SubSpeed);
        return (controller, new AutopilotRun(config, controller, pace));
    }

    private static List<string> Commands(AutopilotRun run, List<int>? ticks = null)
    {
        var log = new List<string>();
        run.CommandEmitted += (tick, text) => { log.Add(text); ticks?.Add(tick); };
        return log;
    }

    [Fact]
    public void TheRunTypesStartByItself()
    {
        // Arrancar una sesión no es táctica: lo hace el arnés, no el piloto.
        var (controller, _) = Build();
        Assert.True(controller.Started);
    }

    [Fact]
    public void OneStep_IsExactlyOneTick()
    {
        var (controller, run) = Build();
        run.StepOneTick();
        Assert.Equal(1, controller.Snapshot.Tick);

        run.StepOneTick();
        Assert.Equal(2, controller.Snapshot.Tick);
    }

    [Fact]
    public void ADeadRunDoesNotStep()
    {
        // Con el submarino destruido el tick queda congelado; un bucle que espere a que
        // avance nunca termina. Esta guarda es lo que impide que la suite se cuelgue.
        var (controller, run) = Build();
        run.StepOneTick();
        var frozen = controller.Snapshot.Tick;

        controller.ForceDestroyForTests();
        run.StepOneTick();

        Assert.True(run.Finished);
        Assert.Equal(frozen, controller.Snapshot.Tick);
    }

    [Fact]
    public void TheBudgetPacesTheCommands()
    {
        var (_, run) = Build(pace: 1.5);
        var ticks = new List<int>();
        var log = Commands(run, ticks);

        for (var i = 0; i < 12000; i++) run.StepOneTick();

        // Control positivo: sin esto, un piloto que no teclea nada pasaría el test.
        Assert.True(log.Count >= 3, $"sólo {log.Count} comandos en 12000 ticks");

        for (var i = 1; i < ticks.Count; i++)
            Assert.True(ticks[i] - ticks[i - 1] >= 90,
                $"comandos en {ticks[i - 1]} y {ticks[i]}: menos de 1,5 s de separación");
    }

    [Fact]
    public void SameSeedAndPace_ProduceTheSameRun()
    {
        // Lo que hace que mirar la demo y correr el test sean la misma corrida.
        var (_, first) = Build();
        var firstLog = Commands(first);
        for (var i = 0; i < 6000; i++) first.StepOneTick();

        var (_, second) = Build();
        var secondLog = Commands(second);
        for (var i = 0; i < 6000; i++) second.StepOneTick();

        Assert.NotEmpty(firstLog);          // dos listas vacías también son iguales
        Assert.Equal(firstLog, secondLog);
    }
}
```

- [ ] **Paso 2: correr los tests para verlos fallar**

```bash
dotnet test tests/Sonar.Autopilot.Tests/Sonar.Autopilot.Tests.csproj --filter AutopilotRunTests
```

Esperado: no compila — `AutopilotRun` no existe.

- [ ] **Paso 3: implementar el bucle**

`src/Sonar.Autopilot/AutopilotRun.cs`:

```csharp
using System;
using Sonar.Controller;
using Sonar.Core;

namespace Sonar.Autopilot;

/// <summary>
/// El bucle de una corrida pilotada. Existe una sola vez y la usan los dos consumidores —el
/// test y la escena de Godot— porque es lo que garantiza que lo que se mira sea exactamente
/// lo que el test afirma.
///
/// El piloto decide por número de tick, no por frame: un frame perdido en Godot hace la
/// simulación más lenta, nunca distinta.
/// </summary>
public sealed class PilotRun
{
    /// <summary>Tick y texto de cada comando. Godot lo engancha para escribirlo en el registro.</summary>
    public event Action<int, string>? CommandEmitted;

    private readonly GameController _controller;
    private readonly Pilot _pilot;
    private readonly SaturationMeter _meter;
    private readonly double _tickDuration;
    private readonly int _budgetTicks;

    private int _nextCommandTick;

    public AutopilotRun(SimulationConfig config, GameController controller, double paceSeconds)
    {
        _controller = controller;
        _pilot = new Pilot(config);
        _meter = new SaturationMeter(config);
        _tickDuration = config.TickDurationSeconds;
        _budgetTicks = (int)Math.Round(paceSeconds / config.TickDurationSeconds);

        // Arrancar la sesión no es táctica, así que no pasa por el piloto.
        controller.SubmitInput("START");
    }

    public bool Finished => _controller.Snapshot.Destroyed;
    public int Tick => _controller.Snapshot.Tick;
    public double Saturation => _meter.IndexAt(_controller.Snapshot.Tick);
    public string? LastCommand { get; private set; }

    public void StepOneTick()
    {
        // Con el submarino destruido Simulation.Tick devuelve temprano y NO actualiza el
        // estado: el tick queda congelado. Sin esta guarda, el bucle de abajo no termina.
        if (Finished) return;

        var snapshot = _controller.Snapshot;
        _pilot.Observe(snapshot);

        if (snapshot.Tick >= _nextCommandTick)
        {
            var text = _pilot.Decide(snapshot);
            if (text is not null)
            {
                _controller.SubmitInput(text);
                _meter.NoteCommand(snapshot.Tick);
                LastCommand = text;
                CommandEmitted?.Invoke(snapshot.Tick, text);
                _nextCommandTick = snapshot.Tick + _budgetTicks;
            }
        }

        _meter.Observe(snapshot);

        // Un período exacto ejecuta cero o un tick, nunca dos. Se reintenta una vez por la
        // deriva de punto flotante; el tope evita el cuelgue si esa suposición deja de valer.
        var before = _controller.Snapshot.Tick;
        for (var attempt = 0; attempt < 2 && _controller.Snapshot.Tick == before; attempt++)
            _controller.Advance(_tickDuration);
    }

    /// <summary>
    /// Avanza hasta morir. El tope no es decoración: si el piloto sobrevive para siempre, el
    /// test tiene que fallar con un mensaje, no colgarse.
    /// </summary>
    public void RunToDeath(int maxTicks)
    {
        var steps = 0;
        while (!Finished && steps++ < maxTicks) StepOneTick();
    }
}
```

- [ ] **Paso 4: correr los tests para verlos pasar**

```bash
dotnet test Sonar.sln
```

Esperado: suite completa en verde.

Si `TheBudgetPacesTheCommands` falla por falta de comandos, **no bajes el número**: significa
que el piloto no está jugando y hay que averiguar por qué antes de seguir. Es el hallazgo del
`[RISK]` del §11 del spec llegando temprano.

- [ ] **Paso 5: commit**

```bash
git add src/Sonar.Autopilot/AutopilotRun.cs tests/Sonar.Autopilot.Tests/AutopilotRunTests.cs
git commit -m "feat(autopilot): drive the run one tick at a time, identically everywhere"
```

---

### Tarea 9: el reporte

Los números que el test **no** afirma. Van a un reporte para que sobrevivan al tuning.

Distinción que importa: el reporte **sí** puede usar verdad del mundo (`controller.Wave`,
`LiveEnemyCount`, el flujo de eventos). El que no puede es el piloto. El arnés mide; el piloto
juega a ciegas.

**Archivos:**
- Crear: `tests/Sonar.Autopilot.Tests/RunReportTests.cs`
- Crear: `src/Sonar.Autopilot/RunReport.cs`
- Modificar: `src/Sonar.Autopilot/AutopilotRun.cs`

- [ ] **Paso 1: escribir el test que falla**

```csharp
using Sonar.Autopilot.Tests.Support;
using Sonar.Controller;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Autopilot.Tests;

public class RunReportTests
{
    [Fact]
    public void TheReportKnowsWhichWavesHappened()
    {
        var config = TestConfig.Build();
        var controller = new GameController(config, 20260828, new Bearing(0), 0.012);
        var run = new AutopilotRun(config, controller, 1.5);

        for (var i = 0; i < 12000; i++) run.StepOneTick();
        var report = run.BuildReport();

        // Control positivo: sin oleadas la tabla estaría vacía y todo lo demás sería vacuo.
        Assert.NotEmpty(report.Waves);
        Assert.Equal(1, report.Waves[0].Wave);
        Assert.True(report.Waves[0].StartTick >= WaveDirector.OpeningQuietTicks,
            "la oleada 1 no puede empezar antes del silencio inicial");
    }

    [Fact]
    public void SaturationIsSampledOncePerSecond()
    {
        var config = TestConfig.Build();
        var controller = new GameController(config, 20260828, new Bearing(0), 0.012);
        var run = new AutopilotRun(config, controller, 1.5);

        for (var i = 0; i < 600; i++) run.StepOneTick();
        var report = run.BuildReport();

        // 600 ticks a 60 por segundo son diez muestras (los ticks 0, 60, … 540).
        Assert.Equal(10, report.SaturationPerSecond.Count);
    }

    [Fact]
    public void TheReportRendersWithoutBlowingUp()
    {
        var config = TestConfig.Build();
        var controller = new GameController(config, 20260828, new Bearing(0), 0.012);
        var run = new AutopilotRun(config, controller, 1.5);

        for (var i = 0; i < 12000; i++) run.StepOneTick();

        var text = run.BuildReport().Render();
        Assert.Contains("WAVE", text);

        // La semilla del reporte es la del INTENTO, no la base: GameController la deriva
        // como baseSeed + intento * 7919 para que los reintentos no repitan los spawns.
        Assert.Contains(controller.Seed.ToString(), text);
    }
}
```

- [ ] **Paso 2: correr el test para verlo fallar**

```bash
dotnet test tests/Sonar.Autopilot.Tests/Sonar.Autopilot.Tests.csproj --filter RunReportTests
```

Esperado: no compila — no existe `BuildReport`.

- [ ] **Paso 3: crear el reporte**

`src/Sonar.Autopilot/RunReport.cs`:

```csharp
using System.Collections.Generic;
using System.Text;

namespace Sonar.Autopilot;

/// <summary>Una oleada, cerrada. Los números que el test no afirma.</summary>
public sealed record WaveRow(
    int Wave, int StartTick, int Ticks, int EnemiesLeft,
    int Shots, int Kills, int Threats, int Commands,
    double PeakSaturation, double EndSaturation);

/// <summary>
/// La corrida entera, en números. Es la evidencia de las preguntas 3, 7 y 8 de M1, y va a
/// stdout — no se escribe en el repositorio.
/// </summary>
public sealed record RunReport(
    int Seed,
    double PaceSeconds,
    int DeathTick,
    string DeathCause,
    int DeathWave,
    double DeathSaturation,
    IReadOnlyList<WaveRow> Waves,
    IReadOnlyList<double> SaturationPerSecond)
{
    /// <summary>Índice de saturación en el segundo dado, o cero si la corrida no llegó ahí.</summary>
    public double SaturationAtSecond(int second) =>
        second >= 0 && second < SaturationPerSecond.Count ? SaturationPerSecond[second] : 0.0;

    public string Render()
    {
        var text = new StringBuilder();
        text.AppendLine($"seed {Seed} · pace {PaceSeconds:F1}s");
        text.AppendLine("WAVE  START   TICKS  LEFT  SHOTS  KILLS  THREATS  CMDS   PEAK-S   END-S");

        foreach (var wave in Waves)
        {
            text.AppendLine(
                $"{wave.Wave,4}  {wave.StartTick,6}  {wave.Ticks,6}  {wave.EnemiesLeft,4}  " +
                $"{wave.Shots,5}  {wave.Kills,5}  {wave.Threats,7}  {wave.Commands,4}  " +
                $"{wave.PeakSaturation,7:F2}  {wave.EndSaturation,6:F2}");
        }

        text.AppendLine(
            $"DEATH  tick {DeathTick} · {DeathCause} · wave {DeathWave} · S {DeathSaturation:F2}");
        return text.ToString();
    }
}
```

- [ ] **Paso 4: acumular en el bucle**

En `src/Sonar.Autopilot/AutopilotRun.cs`, agrega los campos, la acumulación y `BuildReport`.

Campos nuevos, junto a los que ya están:

```csharp
    private readonly int _seed;
    private readonly double _paceSeconds;
    private readonly System.Collections.Generic.List<WaveRow> _waves = new();
    private readonly System.Collections.Generic.List<double> _saturationPerSecond = new();

    private int _wave;
    private int _waveStartTick;
    private int _previousLiveEnemies;
    private int _waveShots;
    private int _waveKills;
    private int _waveThreats;
    private int _waveCommands;
    private double _wavePeak;
    private string _deathCause = "Unknown";
```

En el constructor, después de `controller.SubmitInput("START")`:

```csharp
        _seed = controller.Seed;
        _paceSeconds = paceSeconds;
```

En `StepOneTick`, dentro del bloque que emite el comando, suma `_waveCommands++`. Después de
`_meter.Observe(snapshot)`, agrega el muestreo, el cierre de oleada y la acumulación:

```csharp
        if (snapshot.Tick % 60 == 0) _saturationPerSecond.Add(_meter.IndexAt(snapshot.Tick));

        var current = _meter.IndexAt(snapshot.Tick);
        if (current > _wavePeak) _wavePeak = current;

        // El director de oleadas no emite eventos —es guion, no simulación—, así que el
        // cambio se detecta comparando, igual que hace Main.cs.
        if (_controller.Wave != _wave)
        {
            // Con el conteo del tick ANTERIOR: en el tick en que entra una oleada nueva, el
            // conteo actual ya incluye a los que acaban de aparecer, y la oleada que se está
            // cerrando quedaría reportada con el tamaño de la que la reemplaza.
            CloseWave(snapshot.Tick, _previousLiveEnemies);
            _wave = _controller.Wave;
            _waveStartTick = snapshot.Tick;
            _waveShots = _waveKills = _waveThreats = _waveCommands = 0;
            _wavePeak = 0.0;
        }

        _previousLiveEnemies = _controller.LiveEnemyCount;
```

Y reemplaza el avance del final por uno que registre los eventos:

```csharp
        var before = _controller.Snapshot.Tick;
        for (var attempt = 0; attempt < 2 && _controller.Snapshot.Tick == before; attempt++)
            Record(_controller.Advance(_tickDuration));
```

Métodos nuevos:

```csharp
    /// <summary>
    /// Cuenta desde el flujo de eventos, que es verdad del mundo. El arnés SÍ puede mirarla;
    /// el que no puede es el piloto. Por eso las amenazas del reporte se cuentan por evento y
    /// las del índice por lo que apareció en el snapshot: el índice mide lo que el jugador
    /// percibe, el reporte lo que de verdad pasó.
    /// </summary>
    private void Record(System.Collections.Generic.IReadOnlyList<Sonar.Core.Events.GameEvent> events)
    {
        foreach (var evt in events)
        {
            switch (evt)
            {
                case Sonar.Core.Events.TorpedoFired: _waveShots++; break;
                case Sonar.Core.Events.ContactDestroyed: _waveKills++; break;
                case Sonar.Core.Events.EnemyTorpedoFired: _waveThreats++; break;
                case Sonar.Core.Events.SubDestroyed e: _deathCause = e.Cause.ToString(); break;
            }
        }
    }

    /// <summary>
    /// En toda oleada que se cierra por dar paso a la siguiente, `enemiesLeft` es cero por
    /// construcción: el director no deja entrar la próxima hasta que se limpie la anterior.
    /// Un número distinto de cero en una fila que no sea la última significa que esa compuerta
    /// se rompió. Donde la columna sí dice algo es en la última fila: cuántos quedaban vivos
    /// en el momento de morir.
    /// </summary>
    private void CloseWave(int tick, int enemiesLeft)
    {
        if (_wave == 0) return;
        _waves.Add(new WaveRow(
            _wave, _waveStartTick, tick - _waveStartTick, enemiesLeft,
            _waveShots, _waveKills, _waveThreats, _waveCommands,
            _wavePeak, _meter.IndexAt(tick)));
    }

    /// <summary>Cierra la oleada en curso y devuelve la corrida en números.</summary>
    public RunReport BuildReport()
    {
        var tick = _controller.Snapshot.Tick;
        var waves = new System.Collections.Generic.List<WaveRow>(_waves);
        if (_wave != 0)
        {
            waves.Add(new WaveRow(
                _wave, _waveStartTick, tick - _waveStartTick, _controller.LiveEnemyCount,
                _waveShots, _waveKills, _waveThreats, _waveCommands,
                _wavePeak, _meter.IndexAt(tick)));
        }

        return new RunReport(_seed, _paceSeconds, tick, _deathCause, _wave,
            _meter.IndexAt(tick), waves, _saturationPerSecond);
    }
```

- [ ] **Paso 5: correr los tests para verlos pasar**

```bash
dotnet test Sonar.sln
```

Esperado: suite completa en verde.

- [ ] **Paso 6: commit**

```bash
git add src/Sonar.Autopilot tests/Sonar.Autopilot.Tests/RunReportTests.cs
git commit -m "feat(autopilot): report the numbers the test refuses to assert"
```

---

### Tarea 10: el test de la pared — las dos afirmaciones universales

**Desviación del spec, a propósito.** El §7 del spec dice que este test vive junto a
`GauntletTests`. Ponerlo ahí obliga a que `Sonar.Controller.Tests` referencie
`Sonar.Autopilot`, o sea a que el proyecto de tests del Controller dependa de una capa que
está **por encima** de él. Eso abre la puerta a que mañana alguien use tipos del piloto en un
test del Controller y la cadena de dependencias deje de significar algo. Vive en
`Sonar.Autopilot.Tests`, que ya alcanza todo lo que necesita, y se duplica el cargador de
config de disco — que ya está duplicado dos veces en el repo por la misma razón.

**Archivos:**
- Crear: `tests/Sonar.Autopilot.Tests/Support/WorkingConfig.cs`
- Crear: `tests/Sonar.Autopilot.Tests/TheWallTests.cs`

- [ ] **Paso 1: el cargador de la config real**

```csharp
using System.IO;
using Sonar.Core;
using Xunit;

namespace Sonar.Autopilot.Tests.Support;

/// <summary>
/// La configuración real de disco, la misma que carga el juego. Sólo la usa el test de la
/// pared: lo que ese test mide ES esta configuración. Los unitarios usan TestConfig.
/// </summary>
internal static class WorkingConfig
{
    public static SimulationConfig Load()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Sonar.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);

        var path = Path.Combine(dir!.FullName, "config", "simulation.working.json");
        Assert.True(File.Exists(path), $"No se encontró {path}");
        return SimulationConfigLoader.LoadFromJson(File.ReadAllText(path));
    }
}
```

- [ ] **Paso 2: escribir los tests que fallan**

`tests/Sonar.Autopilot.Tests/TheWallTests.cs`:

```csharp
using System.Linq;
using Sonar.Autopilot.Tests.Support;
using Sonar.Controller;
using Sonar.Core.Spatial;
using Xunit;
using Xunit.Abstractions;

namespace Sonar.Autopilot.Tests;

/// <summary>
/// La corrida completa, con la configuración real, jugada por el piloto hasta morir.
///
/// Ningún número exacto se afirma: ni el tick de muerte, ni la oleada, ni el valor de S. M1
/// es un milestone de tuning y un test que afirme "muere en el tick 8943" se pone rojo cada
/// vez que se hace el trabajo del milestone — y un test que se pone rojo por trabajo legítimo
/// se termina ajustando hasta que pase. Lo que se afirma es la FORMA de la muerte. Los
/// números van al reporte, que se imprime.
/// </summary>
public class TheWallTests
{
    private const double SubSpeed = 0.012;
    private const double Pace = 1.5;

    /// <summary>Mil segundos de juego. Si el piloto sobrevive a eso, el test falla con un mensaje.</summary>
    private const int MaxTicks = 60000;

    private readonly ITestOutputHelper _output;

    public TheWallTests(ITestOutputHelper output) => _output = output;

    private RunReport Play(int seed, out GameController controller)
    {
        var config = WorkingConfig.Load();
        controller = new GameController(config, seed, new Bearing(0), SubSpeed);
        var run = new AutopilotRun(config, controller, Pace);
        run.RunToDeath(MaxTicks);

        var report = run.BuildReport();
        _output.WriteLine(report.Render());
        return report;
    }

    [Theory]
    [InlineData(20260811)]
    [InlineData(11111111)]
    [InlineData(22222222)]
    [InlineData(33333333)]
    [InlineData(44444444)]
    public void ThePilotActuallyPlays(int seed)
    {
        // EL CONTROL POSITIVO. Sin él, un piloto que no teclea nada pasaría trivialmente la
        // afirmación de que murió ahogado: muere en la oleada 1 con S enorme y el test lo
        // celebraría. Es exactamente la forma de test vacuo que ya se coló dos veces acá.
        var report = Play(seed, out var controller);

        Assert.True(controller.Snapshot.Destroyed,
            $"el piloto sobrevivió {MaxTicks} ticks sin morir");
        Assert.NotEmpty(report.Waves);
        Assert.True(report.DeathWave >= 2,
            $"murió en la oleada {report.DeathWave}: no pasó de la primera");
        Assert.True(report.Waves.Sum(w => w.Kills) >= 1,
            "no destruyó ni un contacto en toda la corrida");
    }

    [Theory]
    [InlineData(20260811)]
    [InlineData(11111111)]
    [InlineData(22222222)]
    [InlineData(33333333)]
    [InlineData(44444444)]
    public void TheFirstWaveIsNotAWall(int seed)
    {
        // Iba sobrado al principio. El límite es no estricto a propósito: S = 1 es
        // exactamente "una respuesta por amenaza", o sea seguirle el ritmo justo. Quedarse
        // atrás empieza estrictamente por encima de 1.
        //
        // Este es también el control del medidor: uno que devolviera siempre un número alto
        // rompe acá.
        var report = Play(seed, out _);

        Assert.NotEmpty(report.Waves);
        Assert.Equal(1, report.Waves[0].Wave);
        Assert.True(report.Waves[0].PeakSaturation <= 1.0,
            $"S llegó a {report.Waves[0].PeakSaturation:F2} durante la oleada 1");
    }
}
```

- [ ] **Paso 3: correr los tests**

```bash
dotnet test tests/Sonar.Autopilot.Tests/Sonar.Autopilot.Tests.csproj --filter TheWallTests -v n
```

Esperado: no compila al principio (falta `WorkingConfig`); una vez que compile, diez casos.

**Si `ThePilotActuallyPlays` falla con "murió en la oleada 1":** eso es el `[RISK]` del §11 del
spec y **es el hallazgo, no un defecto del plan**. Significa que la oleada 1 ya es una pared
para un piloto perfecto. **No bajes la afirmación a `>= 1`.** Detén el plan, anota los números
en `docs/development/m1-findings.md` y repórtalo: hay que ajustar tuning antes de seguir.

- [ ] **Paso 4: guardar los reportes de las cinco semillas**

```bash
dotnet test tests/Sonar.Autopilot.Tests/Sonar.Autopilot.Tests.csproj \
  --filter ThePilotActuallyPlays -v n 2>&1 | tee /tmp/pared-cinco-semillas.txt
```

Esperado: cinco tablas, una por semilla. Se usan en la tarea siguiente.

- [ ] **Paso 5: commit**

```bash
git add tests/Sonar.Autopilot.Tests/TheWallTests.cs tests/Sonar.Autopilot.Tests/Support/WorkingConfig.cs
git commit -m "test(autopilot): assert the pilot plays and the first wave is survivable"
```

---

### Tarea 11: elegir la semilla y afirmar la asfixia

El `[OPEN]` del §11 del spec: **cuál es la semilla que se mira se decide con el reporte en la
mano, no antes.** Esta tarea es leer los cinco reportes y decidir.

- [ ] **Paso 1: leer las cinco tablas**

```bash
cat /tmp/pared-cinco-semillas.txt
```

Para cada semilla, anota: oleada de muerte, tick, causa, y `S` en la muerte.

- [ ] **Paso 2: elegir la semilla de la demo**

Elige la semilla que cumpla, en este orden de preferencia:

1. `S` en la muerte por encima de 1 **y** mayor que `S` sesenta segundos antes — murió ahogada.
2. Entre las que cumplan, la que llegue a la oleada más alta: es la que muestra más juego.

Anota la elegida. Si **ninguna** cumple el punto 1, salta al paso 5.

- [ ] **Paso 3: escribir el test que falla**

Agrega a `tests/Sonar.Autopilot.Tests/TheWallTests.cs`, reemplazando `SEMILLA_ELEGIDA` por el
número del paso 2:

```csharp
    [Fact]
    public void TheChosenSeedDiesDrowning()
    {
        // La semilla de la demo. Se eligió mirando el reporte de las cinco, no antes: cuál
        // muestra mejor la pared no es algo que se pueda decidir a priori.
        //
        // Este es el control del medidor por el otro lado: uno que devolviera siempre cero
        // rompe acá, mientras que TheFirstWaveIsNotAWall rompe si devuelve siempre alto.
        var report = Play(SEMILLA_ELEGIDA, out _);

        var deathSecond = report.DeathTick / 60;
        var before = report.SaturationAtSecond(deathSecond - 60);

        Assert.True(report.DeathSaturation > 1.0,
            $"S en la muerte fue {report.DeathSaturation:F2}: no murió ahogado");
        Assert.True(report.DeathSaturation > before,
            $"S no subía: {before:F2} un minuto antes contra {report.DeathSaturation:F2} al morir");
    }
```

- [ ] **Paso 4: correr y confirmar**

```bash
dotnet test tests/Sonar.Autopilot.Tests/Sonar.Autopilot.Tests.csproj --filter TheChosenSeedDiesDrowning -v n
```

Esperado: pasa. Si no pasa, la semilla elegida en el paso 2 no era la correcta — vuelve al
paso 2 con otra, no ajustes los umbrales.

- [ ] **Paso 5: si ninguna semilla murió ahogada**

**No fuerces el test.** Que las cinco mueran por otra causa es un hallazgo con nombre: el
piloto no muere por saturación sino por otra cosa —dos tiros afortunados en la oleada 1, una
embestida, una colisión— y eso responde la pregunta 8 de M1 con un "se sobrevive hasta que un
accidente decide".

En ese caso: **no agregues `TheChosenSeedDiesDrowning`**, escribe el hallazgo en
`docs/development/m1-findings.md` con las cinco tablas, y repórtalo al cerrar la tarea. Las
otras dos afirmaciones se quedan y siguen siendo útiles.

- [ ] **Paso 6: anotar los hallazgos**

Agrega a `docs/development/m1-findings.md`, al final de la sección "Notas de sesión":

```markdown
### 2026-08-28 — plan 9: el piloto automático y la pared

Primera vez que alguien —o algo— ve las oleadas 2 en adelante. El bot juega con el mismo
snapshot que el jugador: lecturas envejecidas y con gating, sin acceso a la verdad del mundo.

`[ASSUMPTION]` Presupuesto de un comando cada 1,5 s. La pregunta 2 de M1 sigue sin responder,
así que es una perilla declarada, no un dato.

`[ASSUMPTION]` El bot asocia lecturas por id de contacto; una persona asocia por cercanía y a
veces se equivoca de blanco. Esto lo hace una cota superior: nadie va a superar esta pared.

| Semilla | Oleada de muerte | Tick | Causa | S al morir |
|---|---|---|---|---|
<!-- una fila por semilla, del reporte -->

<!-- Aquí va lo que se observó: dónde cruza S el 1, si la bimodalidad de la oleada 1 se
     mantiene, y si alguno de los cinco contadores del plan 8 dejó por fin de dar cero. -->
```

Rellena la tabla con los números reales y escribe las observaciones. **La tabla vacía no es
una entrega.**

- [ ] **Paso 7: commit**

```bash
git add tests/Sonar.Autopilot.Tests/TheWallTests.cs docs/development/m1-findings.md
git commit -m "test(autopilot): pin the drowning assertion to the seed the report chose"
```

---

### Tarea 12: verificación por mutación

El §4 del contrato: **rompe la implementación a propósito y confirma que los tests fallan.**
Existe porque ya se colaron tests vacuos varias veces, y este plan tiene la forma de riesgo
exacta — afirmaciones sobre un bot que podría no estar jugando.

Para cada fila: aplica la mutación, corre la suite, **anota cuántos tests fallaron**, restaura.

- [ ] **Paso 1: el piloto no teclea nada**

En `Pilot.Decide`, reemplaza el cuerpo por `return null;`.

```bash
dotnet test Sonar.sln 2>&1 | tail -5
git checkout src/Sonar.Autopilot/Pilot.cs
```

Debe caer: `ThePilotActuallyPlays` (las cinco), `TheBudgetPacesTheCommands`,
`SameSeedAndPace_ProduceTheSameRun`, y los unitarios de disparo, recarga y defensa.
**Si `ThePilotActuallyPlays` sobrevive, el control positivo no sirve y hay que arreglarlo
antes de seguir.**

- [ ] **Paso 2: la extrapolación apagada**

En `FiringSolver.TrySolve`, reemplaza la línea de `predicted` por:

```csharp
            var predicted = estimate.Position;   // MUTACIÓN: apunta a la lectura, sin adelantar
```

```bash
dotnet test Sonar.sln 2>&1 | tail -5
git checkout src/Sonar.Autopilot/FiringSolver.cs
```

Debe caer: `AClosingTarget_IsShotWhereItWillBe`, `TheAgeOfTheReadingIsPaidFor`,
`WithASolution_ItTypesTheCoordinate`, y muy probablemente `ThePilotActuallyPlays` — sin
adelantar el blanco no le pega a nada y no llega a la oleada 2.

- [ ] **Paso 3: el medidor siempre alto**

En `SaturationMeter.IndexAt`, reemplaza el cuerpo por `return 99.0;`.

```bash
dotnet test Sonar.sln 2>&1 | tail -5
git checkout src/Sonar.Autopilot/SaturationMeter.cs
```

Debe caer: `TheFirstWaveIsNotAWall` (las cinco) y los unitarios del medidor.

- [ ] **Paso 4: el medidor siempre cero**

En `SaturationMeter.IndexAt`, reemplaza el cuerpo por `return 0.0;`.

```bash
dotnet test Sonar.sln 2>&1 | tail -5
git checkout src/Sonar.Autopilot/SaturationMeter.cs
```

Debe caer: `TheChosenSeedDiesDrowning` (si existe) y los unitarios del medidor.
**`TheFirstWaveIsNotAWall` debe SOBREVIVIR** — y esa es la prueba de que las dos
afirmaciones se controlan mutuamente y no son la misma cosa dicha dos veces.

- [ ] **Paso 5: la defensa desconectada — esta no es una mutación, es una medición**

En `Pilot.Decide`, quita `DecideDefense(snapshot) ??`.

```bash
dotnet test tests/Sonar.Autopilot.Tests/Sonar.Autopilot.Tests.csproj --filter ThePilotActuallyPlays -v n
git checkout src/Sonar.Autopilot/Pilot.cs
```

Caerán los unitarios de defensa, obviamente. Lo que interesa es **el reporte**: compara las
oleadas de muerte con y sin defensa. Si la pared no se movió, la defensa no está haciendo nada
y eso es un hallazgo de M1 —va a `m1-findings.md`—, no un defecto del test.

- [ ] **Paso 6: confirmar que el árbol quedó limpio**

```bash
git status --short
dotnet test Sonar.sln
```

Esperado: sin cambios pendientes, suite completa en verde.

- [ ] **Paso 7: anotar el resultado**

Agrega a la nota de sesión del plan 9 en `docs/development/m1-findings.md` una línea por
mutación con **cuántos tests cayeron**. Es lo que el contrato pide reportar.

```bash
git add docs/development/m1-findings.md
git commit -m "docs: record the mutation counts for the autopilot suite"
```

---

### Tarea 13: el modo demo en Godot

Todo lo que sigue es **cableado**: leer banderas, cambiar quién avanza el mundo, y enganchar
los comandos del bot al camino que ya recorre un comando tecleado.

**El §7 del contrato es explícito: el diseño visual es de la persona.** No se toca
`SonarView.cs` ni el layout de `Main.tscn`. Lo que esta tarea deja son tres datos accesibles
desde `Main` — `_demo.Saturation`, `_demo.LastCommand` y el reporte — para que se dibujen o no
se dibujen según decida quien diseña.

**Archivos:**
- Modificar: `src/Sonar.Game/Main.cs`
- Modificar: `src/Sonar.Game/Telemetry/SessionRecorder.cs`
- Modificar: `Makefile`

- [ ] **Paso 1: la línea `pilot` en la telemetría**

En `src/Sonar.Game/Telemetry/SessionRecorder.cs`, agrega junto a `RunStarted`:

```csharp
    /// <summary>
    /// Marca la corrida como pilotada por el bot y con qué presupuesto. Sin esta línea, las
    /// corridas del bot y las de las personas se mezclarían en el mismo corpus y nadie podría
    /// separarlas después — que es justo lo que arruinaría la pregunta 2.
    /// </summary>
    public void Pilot(double paceSeconds) =>
        _writer.Write(new JsonLine("pilot").Add("pace", paceSeconds));
```

- [ ] **Paso 2: los campos del modo demo en `Main.cs`**

Agrega junto a los campos que ya existen, después de `private string _lastDeathCause = "Unknown";`:

```csharp
    // --- Modo demo: `godot-mono --path src/Sonar.Game -- --demo` ---
    private AutopilotRun? _demo;
    private double _demoSpeed = 1.0;
    private double _demoAccumulator;
    private bool _demoReported;

    /// <summary>
    /// Tope de ticks por frame en modo demo. Diez segundos de juego: suficiente para
    /// cualquier velocidad de reproducción razonable, y bajo para que un frame trabado no
    /// congele la ventana mientras la simulación se pone al día.
    /// </summary>
    private const int MaxDemoTicksPerFrame = 600;
```

Y agrega el `using` arriba, junto a los otros:

```csharp
using Sonar.Autopilot;
```

- [ ] **Paso 3: leer las banderas antes de construir el controller**

La semilla tiene que estar decidida **antes** de crear el `GameController`. En `_Ready`,
reemplaza:

```csharp
        // El director de oleadas vive dentro del GameController: acá sólo se le da rumbo y
        // avance, y él coloca los contactos cuando toca.
        _controller = new GameController(config, Seed, Course, SubSpeed);
```

por:

```csharp
        // Las banderas se leen acá y no más abajo porque la semilla tiene que estar decidida
        // antes de construir el controller.
        var args = OS.GetCmdlineUserArgs();
        var seed = (int)ArgValue(args, "--seed=", Seed);

        // El director de oleadas vive dentro del GameController: acá sólo se le da rumbo y
        // avance, y él coloca los contactos cuando toca.
        _controller = new GameController(config, seed, Course, SubSpeed);
```

- [ ] **Paso 4: armar el piloto después del grabador**

En `_Ready`, justo después de `_recordedAttempt = _controller.Attempt;`, agrega:

```csharp
        // El modo demo y el autotest del terminal son excluyentes: el primero teclea por el
        // piloto y el segundo inyecta teclas para comprobar que el LineEdit las recibe.
        if (System.Array.IndexOf(args, "--demo") >= 0)
        {
            var pace = ArgValue(args, "--pace=", 1.5);
            _demoSpeed = ArgValue(args, "--speed=", 1.0);
            _demo = new AutopilotRun(config, _controller, pace);
            _demo.CommandEmitted += OnAutopilotCommand;
            _recorder.Pilot(pace);
            _eventLog.WriteNow(Dim,
                $"AUTOPILOT · SEED {seed} · PACE {pace:F1}S · SPEED X{_demoSpeed:F0}");
        }
```

- [ ] **Paso 5: reemplazar quién avanza el mundo**

En `_Process`, reemplaza:

```csharp
        foreach (var evt in _controller.Advance(delta)) Report(evt);
```

por:

```csharp
        if (_demo is not null) AdvanceDemo(delta);
        else foreach (var evt in _controller.Advance(delta)) Report(evt);
```

- [ ] **Paso 6: los cuatro métodos nuevos**

Agrégalos en `Main.cs`, antes de `_Notification`:

```csharp
    /// <summary>
    /// Valor numérico de una bandera `--clave=valor`, o el default. En cultura invariante:
    /// con la configuración regional de Chile el separador decimal es la coma y `--pace=1.5`
    /// se leería como 15.
    /// </summary>
    private static double ArgValue(string[] args, string prefix, double fallback)
    {
        foreach (var arg in args)
        {
            if (!arg.StartsWith(prefix)) continue;
            if (double.TryParse(arg[prefix.Length..],
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var value))
                return value;
        }
        return fallback;
    }

    /// <summary>
    /// Avanza la corrida pilotada tick a tick, no con el delta del frame. El piloto decide por
    /// número de tick, así que un frame perdido hace la simulación más lenta pero nunca
    /// distinta: lo que se mira acá es exactamente la corrida que afirma TheWallTests.
    /// </summary>
    private void AdvanceDemo(double delta)
    {
        if (_demo!.Finished)
        {
            ReportDemoOnce();
            return;
        }

        _demoAccumulator += delta * _demoSpeed;

        var executed = 0;
        while (_demoAccumulator >= _tickDuration && executed < MaxDemoTicksPerFrame)
        {
            _demoAccumulator -= _tickDuration;
            executed++;

            _demo.StepOneTick();

            // Los eventos del tick que acaba de correr. StepOneTick llama a Advance por
            // dentro, así que hay que leerlos de LastEvents en vez de recibirlos.
            foreach (var evt in _controller.LastEvents) Report(evt);

            if (_demo.Finished) break;
        }

        if (_demoAccumulator >= _tickDuration) _demoAccumulator = 0;
    }

    /// <summary>
    /// El comando del bot entra por el mismo camino que uno tecleado: el registro y la
    /// telemetría no distinguen quién lo escribió, y eso es lo que hace que las corridas
    /// pilotadas entren al mismo corpus que las de los testers.
    ///
    /// NO se llama a _metrics.NoteSubmit(): SessionMetrics mide tiempo de composición humano,
    /// y un comando de cero segundos envenenaría el promedio de la pregunta 2 de M1. Los
    /// disparos y las muertes sí se cuentan, porque esos sí son reales — pasan por Report.
    /// </summary>
    private void OnAutopilotCommand(int tick, string text)
    {
        _eventLog.Queue(Typed, $"> {text}");
        _recorder.Command(tick, text, true, 0.0);
    }

    /// <summary>El reporte va a consola una sola vez, con el scope congelado en la muerte.</summary>
    private void ReportDemoOnce()
    {
        if (_demoReported) return;
        _demoReported = true;
        GD.Print(_demo!.BuildReport().Render());
    }
```

- [ ] **Paso 7: compilar el juego**

`Sonar.Game` no está en `Sonar.sln` a propósito, así que se compila aparte:

```bash
dotnet build src/Sonar.Game/Sonar.Game.csproj
```

Esperado: compila sin errores.

- [ ] **Paso 8: el objetivo del Makefile**

⚠️ **Las líneas de receta van con TAB, no con espacios.** Reemplaza el contenido de `Makefile`:

```makefile
GODOT = godot-mono

SEED  ?= 20260811
PACE  ?= 1.5
SPEED ?= 1

.PHONY: run demo
run:
	$(GODOT) --path src/Sonar.Game

demo:
	$(GODOT) --path src/Sonar.Game -- --demo --seed=$(SEED) --pace=$(PACE) --speed=$(SPEED)
```

Verifica los tabs antes de seguir:

```bash
cat -A Makefile | grep -n '\$(GODOT)'
```

Esperado: cada línea empieza con `^I` (el tab), no con espacios.

- [ ] **Paso 9: mirarlo correr**

```bash
make demo SPEED=8
```

Esperado, en la ventana: el instructivo, la línea `AUTOPILOT · SEED …`, diez segundos de
silencio (a x8, poco más de uno), la entrada de la oleada 1, y los comandos del bot
apareciendo en amarillo en el registro como si alguien los tecleara. Al morir, el scope queda
congelado y el reporte sale por consola.

Si el bot no teclea nada, no toques el modo demo: el problema está en el piloto y los tests de
la tarea 10 tendrían que haberlo dicho.

- [ ] **Paso 10: commit**

```bash
git add src/Sonar.Game/Main.cs src/Sonar.Game/Telemetry/SessionRecorder.cs Makefile
git commit -m "feat(game): let the autopilot drive the real scope, tick by tick"
```

---

### Tarea 14: cerrar el plan

- [ ] **Paso 1: el estado del proyecto**

En `project-state.yaml`, bajo `progress:`, agrega después de `plan_8_held_fire_and_telemetry`:

```yaml
  plan_9_autopilot_and_the_wall: done   # 14/14. Primera vez que se ven las oleadas 2 en adelante
```

Y bajo `systems:`, agrega:

```yaml
  autopilot: done                  # piloto que juega leyendo sólo el snapshot, e índice de saturación
```

Y bajo `architecture:`, corrige la cadena:

```yaml
  chain: Sonar.Game (Godot) -> Sonar.Autopilot -> Sonar.Controller -> Sonar.Core
  rule: ni Core ni Controller ni Autopilot referencian Godot; esa ausencia es el enforcement
```

- [ ] **Paso 2: documentar el contrato del modo demo**

Agrega al final de `docs/development/godot-wiring.md`:

```markdown
## Modo demo

`make demo` corre la escena con el piloto automático al mando:

    godot-mono --path src/Sonar.Game -- --demo --seed=20260811 --pace=1.5 --speed=1

| Bandera | Qué hace | Default |
|---|---|---|
| `--demo` | Enciende el piloto. Sin ella el juego es idéntico | apagado |
| `--seed=N` | Semilla de la corrida | `20260811` |
| `--pace=X` | Segundos por comando del bot | `1.5` |
| `--speed=K` | Multiplicador de reproducción | `1` |

Excluyente con `--m1-selftest`: uno teclea por el piloto y el otro inyecta teclas para
comprobar que el terminal las recibe.

**Lo que la presentación puede dibujar y hoy no dibuja.** `Main` tiene accesibles tres datos
del piloto; ninguno se pinta, porque el diseño visual es de la persona (§7 del contrato):

| Dato | Tipo | Qué mostraría |
|---|---|---|
| `_demo.Saturation` | `double`, cada tick | El índice cruzando 1 antes de la muerte |
| `_demo.LastCommand` | `string?` | Lo que el bot "teclea", en el campo de comandos |
| `_demo.BuildReport()` | tabla por oleada | Un panel de cierre al morir |

**Determinismo.** El piloto decide por número de tick, no por frame: un frame perdido hace la
simulación más lenta, nunca distinta. Por eso lo que se mira acá es exactamente la corrida que
afirma `TheWallTests`.
```

- [ ] **Paso 3: la verificación de capas**

```bash
grep -rn "^using Godot" src/Sonar.Core/ src/Sonar.Controller/ src/Sonar.Autopilot/
```

Esperado: sin resultados. Si aparece algo, la cadena se rompió y hay que arreglarlo antes de
cerrar.

- [ ] **Paso 4: la suite completa**

```bash
dotnet test Sonar.sln
dotnet build src/Sonar.Game/Sonar.Game.csproj
```

Esperado: todo verde, el juego compila.

- [ ] **Paso 5: commit**

```bash
git add project-state.yaml docs/development/godot-wiring.md
git commit -m "docs: close plan 9 with the demo contract and the new layer"
```

---

## Definition of Done

Del §12 del spec, más lo que agregó este plan:

- [ ] `project-state.yaml` actualizado
- [ ] `grep -rn "^using Godot" src/Sonar.Core/ src/Sonar.Controller/ src/Sonar.Autopilot/` vacío
- [ ] `dotnet test Sonar.sln` en verde
- [ ] `dotnet build src/Sonar.Game/Sonar.Game.csproj` en verde
- [ ] Verificación por mutación corrida, con el conteo de tests caídos anotado
- [ ] `make demo` corre y la corrida termina en muerte
- [ ] Las cinco semillas anotadas en `m1-findings.md`, con `X` declarado
- [ ] `godot-wiring.md` documenta el contrato del modo demo

## Lo que este plan puede descubrir y hay que reportar, no esconder

| Si pasa esto | Significa | Qué hacer |
|---|---|---|
| El piloto no pasa de la oleada 1 | La pared está mucho antes de lo que supone el diseño | Detener, anotar, ajustar tuning |
| Ninguna semilla muere ahogada | Se muere por accidente, no por saturación — respuesta a la pregunta 8 | No agregar la tercera afirmación; anotar |
| Quitar la defensa no mueve la pared | La defensa no está haciendo nada | Anotar en `m1-findings.md` |
| Los contadores del plan 8 siguen en cero | Ni con bot se llega a donde esas reglas viven | Anotar; es la segunda vez que ese plan no se puede medir |
