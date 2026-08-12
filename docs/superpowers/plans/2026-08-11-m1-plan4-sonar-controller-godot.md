# M1 · Plan 4 — Sonar, Controller y Godot Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cerrar M1 — registro de lecturas con gating por clase, la capa Controller con parser y snapshot, y una escena de Godot deliberadamente fea que permita jugarlo con las manos y medir las ocho preguntas del milestone.

**Architecture:** El gating se blinda en el contrato de datos: `ContactView.Position` es la posición de **la lectura**, no la real, así que la capa de presentación no puede dibujar la verdad aunque quisiera. `Sonar.Controller` no referencia Godot; solo `Sonar.Game` lo hace.

**Tech Stack:** C# / .NET, xUnit, Godot con soporte C#.

**Prerrequisito:** Planes 1, 2 y 3 completos. 167 tests pasando.

**Documentos de referencia:**
- TDD: `docs/tdd/M1-core-simulation.md` — §5 sonar, §6 comandos, §7 snapshot y eventos
- Spec: `docs/superpowers/specs/2026-08-11-sonar-preproduction-design.md` — §4.1 clases de información, §5 arquitectura

> **Nota sobre la API de Godot.** Las tareas 8 a 10 usan nombres de la API de Godot 4. Si alguno
> no compila con la versión que fijaste en el Plan 1, consulta la documentación de *esa* versión
> en lugar de improvisar: la estructura del plan no cambia, solo el nombre del método.

---

## Estructura de archivos

| Archivo | Responsabilidad |
|---|---|
| `src/Sonar.Core/Simulation.cs` | Modificar: registrar lecturas con gating |
| `src/Sonar.Controller/Sonar.Controller.csproj` | Librería de frontera. Referencia a Core, **no** a Godot |
| `src/Sonar.Controller/CommandParser.cs` | Texto crudo a `Command` |
| `src/Sonar.Controller/Presentation/Views.cs` | DTOs de presentación, sin tipos de dominio |
| `src/Sonar.Controller/Presentation/SnapshotBuilder.cs` | `WorldState` a `WorldSnapshot` |
| `src/Sonar.Controller/GameController.cs` | Acumulador de paso fijo y superficie pública |
| `src/Sonar.Game/` | Proyecto Godot: escena, dibujo del sonar, terminal |
| `tests/Sonar.Controller.Tests/` | Tests de parser, snapshot y acumulador |

---

## Task 1: Detección de cruce del barrido

**Files:**
- Create: `src/Sonar.Core/Sensing/SweepCrossing.cs`
- Test: `tests/Sonar.Core.Tests/Sensing/SweepCrossingTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Sensing/SweepCrossingTests.cs`:

```csharp
using Sonar.Core.Sensing;
using Xunit;

namespace Sonar.Core.Tests.Sensing;

public class SweepCrossingTests
{
    [Fact]
    public void Crossed_WhenTargetIsBetweenPreviousAndCurrent()
    {
        Assert.True(SweepCrossing.Crossed(previous: 40, current: 60, target: 50));
    }

    [Fact]
    public void NotCrossed_WhenTargetIsAhead()
    {
        Assert.False(SweepCrossing.Crossed(previous: 40, current: 60, target: 90));
    }

    [Fact]
    public void NotCrossed_WhenTargetIsBehind()
    {
        Assert.False(SweepCrossing.Crossed(previous: 40, current: 60, target: 10));
    }

    [Fact]
    public void Crossed_AcrossTheWrapPoint()
    {
        // El haz pasó de 350 a 5 grados. Un contacto en 358 fue barrido.
        Assert.True(SweepCrossing.Crossed(previous: 350, current: 5, target: 358));
    }

    [Fact]
    public void Crossed_AcrossTheWrapPointOnTheLowSide()
    {
        Assert.True(SweepCrossing.Crossed(previous: 350, current: 5, target: 2));
    }

    [Fact]
    public void NotCrossed_AcrossTheWrapPointWhenOutside()
    {
        Assert.False(SweepCrossing.Crossed(previous: 350, current: 5, target: 180));
    }

    [Fact]
    public void Crossed_ExactlyOnCurrent_CountsAsCrossed()
    {
        Assert.True(SweepCrossing.Crossed(previous: 40, current: 60, target: 60));
    }

    [Fact]
    public void NotCrossed_ExactlyOnPrevious()
    {
        // Ya se registró en el tick anterior; no debe registrarse dos veces.
        Assert.False(SweepCrossing.Crossed(previous: 40, current: 60, target: 40));
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'SweepCrossing' could not be found`.

- [ ] **Step 3: Implementar la detección de cruce**

Crea `src/Sonar.Core/Sensing/SweepCrossing.cs`:

```csharp
namespace Sonar.Core.Sensing;

public static class SweepCrossing
{
    /// <summary>
    /// ¿El haz cruzó la marcación dada al pasar de previous a current? Maneja la vuelta
    /// completa: cuando current es menor que previous, el haz cruzó el punto cero.
    /// Los tres valores en grados, [0, 360).
    /// </summary>
    public static bool Crossed(double previous, double current, double target) =>
        current >= previous
            ? target > previous && target <= current
            : target > previous || target <= current;
}
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 175`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/Sensing/SweepCrossing.cs tests/Sonar.Core.Tests/Sensing/SweepCrossingTests.cs
git commit -m "feat(core): add sweep crossing detection with wraparound"
```

---

## Task 2: Registro de lecturas con gating

**Files:**
- Modify: `src/Sonar.Core/Simulation.cs`
- Test: `tests/Sonar.Core.Tests/Sensing/GatingTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Sensing/GatingTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Sensing;

public class GatingTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Contact EnemyAt(int id, Vec2 position, Vec2 velocity) => new()
    {
        Id = new ContactId(id),
        Kind = ContactKind.Enemy,
        Position = position,
        Velocity = velocity,
        Alive = true,
        NextFireTick = int.MaxValue,
    };

    private static int TicksPerSweep(SimulationConfig config) =>
        (int)Math.Round(config.SweepPeriodSeconds / config.TickDurationSeconds);

    [Fact]
    public void NoReading_BeforeTheBeamPasses()
    {
        var sim = TestWorld.Still();
        // Contacto al oeste (marcación 270). El haz arranca en 0 y gira en sentido horario.
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(-0.60, 0.0), Vec2.Zero) });

        for (var i = 0; i < 10; i++) sim.Tick(NoCommands);

        Assert.Empty(sim.State.Sonar.Readings);
    }

    [Fact]
    public void ReadingAppears_AfterAFullSweep()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(-0.60, 0.0), Vec2.Zero) });

        for (var i = 0; i < TicksPerSweep(config) + 2; i++) sim.Tick(NoCommands);

        var reading = Assert.Single(sim.State.Sonar.Readings);
        Assert.Equal(1, reading.ContactId.Value);
    }

    [Fact]
    public void ReadingDoesNotFollowTheContact_BetweenSweeps()
    {
        // Esta es la propiedad central del juego: la etiqueta que ves es del último
        // barrido, no de dónde está el contacto ahora.
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.0, -0.60), new Vec2(0.20, 0.0)) });

        // Marcación 0: el haz lo barre casi de inmediato.
        for (var i = 0; i < 5; i++) sim.Tick(NoCommands);
        var justRead = sim.State.Sonar.Readings.Single().Position;

        // Avanza medio barrido sin que el haz vuelva a pasar.
        for (var i = 0; i < TicksPerSweep(config) / 2; i++) sim.Tick(NoCommands);

        var stillTheSame = sim.State.Sonar.Readings.Single().Position;
        Assert.Equal(justRead, stillTheSame);

        // Pero el contacto sí se movió.
        Assert.NotEqual(justRead.X, sim.State.Contacts.Single().Position.X, precision: 3);
    }

    [Fact]
    public void ReadingRefreshes_OnTheNextPass()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.0, -0.60), new Vec2(0.10, 0.0)) });

        for (var i = 0; i < 5; i++) sim.Tick(NoCommands);
        var firstTick = sim.State.Sonar.Readings.Single().ObservedAtTick;

        for (var i = 0; i < TicksPerSweep(config) + 5; i++) sim.Tick(NoCommands);
        var secondTick = sim.State.Sonar.Readings.Single().ObservedAtTick;

        Assert.True(secondTick > firstTick);
    }

    [Fact]
    public void StoppedSweep_RecordsNothingNew()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.Tick(new Command[] { new ToggleSonarCommand() });
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(-0.60, 0.0), Vec2.Zero) });

        for (var i = 0; i < TicksPerSweep(config) * 2; i++) sim.Tick(NoCommands);

        Assert.Empty(sim.State.Sonar.Readings);
    }

    [Fact]
    public void DestroyedContact_LosesItsReadingImmediately()
    {
        // La confirmación de destrucción es ley del mundo y no depende del barrido
        // (spec §4.6). Dejar la lectura sería mentirle al jugador.
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.0, -0.55), Vec2.Zero) });

        for (var i = 0; i < 5; i++) sim.Tick(NoCommands);
        Assert.Single(sim.State.Sonar.Readings);

        sim.Tick(new Command[] { new FireCommand(new TargetCoordinate(new Bearing(0), new Band(2))) });
        for (var i = 0; i < 120; i++) sim.Tick(NoCommands);

        Assert.Empty(sim.State.Contacts);
        Assert.Empty(sim.State.Sonar.Readings);
    }

    [Fact]
    public void Readings_StaySortedByContactId()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[]
        {
            EnemyAt(9, new Vec2(0.0, -0.5), Vec2.Zero),
            EnemyAt(2, new Vec2(0.5, 0.0), Vec2.Zero),
            EnemyAt(5, new Vec2(0.0, 0.5), Vec2.Zero),
        });

        for (var i = 0; i < TicksPerSweep(config) + 5; i++) sim.Tick(NoCommands);

        var ids = sim.State.Sonar.Readings.Select(r => r.ContactId.Value).ToArray();
        Assert.Equal(ids.OrderBy(v => v).ToArray(), ids);
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL. `Readings` siempre está vacío.

- [ ] **Step 3: Implementar el registro de lecturas**

En `src/Sonar.Core/Simulation.cs`, reemplaza `AdvanceSweep` por esta versión, que además registra:

```csharp
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
        Sensing.SonarState sonar, List<Contact> contacts, int tick, double dt)
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

        return sonar with
        {
            SweepDegrees = current,
            Readings = readings.OrderBy(r => r.ContactId.Value).ToArray(),
        };
    }
```

Y en `Tick`, reemplaza la línea del paso 10:

```csharp
        // 10 · Barrido y registro de lecturas
        var sonar = AdvanceSweepAndRecord(state.Sonar, contacts, state.Tick, dt);
```

Borra el método `AdvanceSweep` anterior, que queda sin uso.

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 182`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/Simulation.cs tests/Sonar.Core.Tests/Sensing/GatingTests.cs
git commit -m "feat(core): record sweep-gated contact readings"
```

---

## Task 3: Proyecto Sonar.Controller

**Files:**
- Create: `src/Sonar.Controller/Sonar.Controller.csproj`
- Create: `tests/Sonar.Controller.Tests/Sonar.Controller.Tests.csproj`

- [ ] **Step 1: Crear los proyectos**

```bash
dotnet new classlib --output src/Sonar.Controller --name Sonar.Controller
dotnet new xunit --output tests/Sonar.Controller.Tests --name Sonar.Controller.Tests
dotnet sln add src/Sonar.Controller/Sonar.Controller.csproj
dotnet sln add tests/Sonar.Controller.Tests/Sonar.Controller.Tests.csproj
dotnet add src/Sonar.Controller/Sonar.Controller.csproj reference src/Sonar.Core/Sonar.Core.csproj
dotnet add tests/Sonar.Controller.Tests/Sonar.Controller.Tests.csproj reference src/Sonar.Controller/Sonar.Controller.csproj
rm -f src/Sonar.Controller/Class1.cs tests/Sonar.Controller.Tests/UnitTest1.cs
```

- [ ] **Step 2: Endurecer el csproj**

Reemplaza el contenido de `src/Sonar.Controller/Sonar.Controller.csproj`. El TFM es `net9.0`,
el mismo que `Sonar.Core` y el que exige Godot 4.7 mono.

> Ojo: en el SDK 10, `dotnet new classlib` y `dotnet new xunit` generan `net10.0` por defecto.
> Hay que corregirlo a mano en ambos proyectos.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Sonar.Core\Sonar.Core.csproj" />
  </ItemGroup>

</Project>
```

> **Sonar.Controller no debe referenciar Godot.** Es el punto contraintuitivo del diseño:
> si lo hiciera, tipos de Godot podrían filtrarse hacia Core a través de él. Godot conoce
> al Controller; el Controller no conoce a Godot.

- [ ] **Step 3: Verificar**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 182`

- [ ] **Step 4: Commit**

```bash
git add src/Sonar.Controller tests/Sonar.Controller.Tests Sonar.sln
git commit -m "chore: add Sonar.Controller project"
```

---

## Task 4: Parser de comandos

**Files:**
- Create: `src/Sonar.Controller/CommandParser.cs`
- Test: `tests/Sonar.Controller.Tests/CommandParserTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Controller.Tests/CommandParserTests.cs`:

```csharp
using Sonar.Controller;
using Sonar.Core.Commands;
using Xunit;

namespace Sonar.Controller.Tests;

public class CommandParserTests
{
    private static readonly CommandParser Parser = new(bandCount: 5);

    [Theory]
    [InlineData("C047", 'C', 47)]
    [InlineData("c47", 'C', 47)]
    [InlineData("FIRE C047", 'C', 47)]
    [InlineData("fire c047", 'C', 47)]
    [InlineData("F C047", 'C', 47)]
    [InlineData("  f   c 47  ", 'C', 47)]
    [InlineData("A000", 'A', 0)]
    [InlineData("E359", 'E', 359)]
    public void ParsesFireCommands(string input, char band, int bearing)
    {
        var command = Assert.IsType<FireCommand>(Parser.Parse(input));
        Assert.Equal(band, command.Target.Band.Letter);
        Assert.Equal(bearing, command.Target.Bearing.Degrees);
    }

    [Theory]
    [InlineData("DEF C047", 'C', 47)]
    [InlineData("DEFEND C047", 'C', 47)]
    [InlineData("def c47", 'C', 47)]
    public void ParsesDefenseCommands(string input, char band, int bearing)
    {
        var command = Assert.IsType<DeployDefenseCommand>(Parser.Parse(input));
        Assert.Equal(band, command.Target.Band.Letter);
        Assert.Equal(bearing, command.Target.Bearing.Degrees);
    }

    [Theory]
    [InlineData("RELOAD")]
    [InlineData("reload")]
    [InlineData("  Reload  ")]
    public void ParsesReload(string input) => Assert.IsType<ReloadCommand>(Parser.Parse(input));

    [Fact]
    public void ParsesSonar() => Assert.IsType<ToggleSonarCommand>(Parser.Parse("SONAR"));

    [Fact]
    public void ParsesStatus() => Assert.IsType<StatusCommand>(Parser.Parse("STATUS"));

    [Theory]
    [InlineData("C400")]   // marcación fuera de rango: no se toma módulo
    [InlineData("C360")]
    [InlineData("Z047")]   // letra fuera del conteo de bandas
    [InlineData("F047")]   // con cinco bandas, F no existe
    [InlineData("hello")]
    [InlineData("C")]
    [InlineData("047")]
    [InlineData("C0470")]
    [InlineData("FIRE")]
    public void UnknownOrInvalidInput_BecomesUnrecognized(string input)
    {
        var command = Assert.IsType<UnrecognizedInput>(Parser.Parse(input));
        Assert.Equal(input, command.RawText);
    }

    [Fact]
    public void UnrecognizedInput_PreservesTheOriginalTextNotTheNormalized()
    {
        // El historial debe mostrar lo que el jugador escribió, no una versión limpia.
        var command = Assert.IsType<UnrecognizedInput>(Parser.Parse("  Hello There  "));
        Assert.Equal("  Hello There  ", command.RawText);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyInput_ReturnsNull(string input) => Assert.Null(Parser.Parse(input));

    [Fact]
    public void BandCountIsRespected()
    {
        var threeBands = new CommandParser(bandCount: 3);
        Assert.IsType<FireCommand>(threeBands.Parse("C047"));
        Assert.IsType<UnrecognizedInput>(threeBands.Parse("D047"));
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'CommandParser' could not be found`.

- [ ] **Step 3: Implementar el parser**

Crea `src/Sonar.Controller/CommandParser.cs`:

```csharp
using System.Text.RegularExpressions;
using Sonar.Core.Commands;
using Sonar.Core.Spatial;

namespace Sonar.Controller;

/// <summary>
/// Texto crudo a comando. Vive en el Controller, no en Core: Core recibe comandos ya
/// construidos. Gramática en TDD §6.2.
/// </summary>
public sealed class CommandParser
{
    private static readonly Regex CoordinatePattern =
        new(@"^(?:(?:FIRE|F)\s+)?([A-Z])\s*(\d{1,3})$", RegexOptions.Compiled);

    private static readonly Regex DefensePattern =
        new(@"^(?:DEF|DEFEND)\s+([A-Z])\s*(\d{1,3})$", RegexOptions.Compiled);

    private readonly int _bandCount;

    public CommandParser(int bandCount) => _bandCount = bandCount;

    /// <summary>
    /// Devuelve null si el texto está vacío — no hay nada que registrar. Cualquier otro
    /// texto que no corresponda a un comando devuelve UnrecognizedInput, que no es un
    /// error: el §10 del GDD pide que todo input quede registrado y que lo desconocido
    /// simplemente no haga nada.
    /// </summary>
    public Command? Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var normalized = Regex.Replace(raw.Trim(), @"\s+", " ").ToUpperInvariant();

        switch (normalized)
        {
            case "RELOAD": return new ReloadCommand();
            case "SONAR": return new ToggleSonarCommand();
            case "STATUS": return new StatusCommand();
        }

        var defense = DefensePattern.Match(normalized);
        if (defense.Success && TryTarget(defense, out var defenseTarget))
            return new DeployDefenseCommand(defenseTarget);

        var fire = CoordinatePattern.Match(normalized);
        if (fire.Success && TryTarget(fire, out var fireTarget))
            return new FireCommand(fireTarget);

        return new UnrecognizedInput(raw);
    }

    private bool TryTarget(Match match, out TargetCoordinate target)
    {
        target = default;

        if (!Band.TryParseLetter(match.Groups[1].Value[0], _bandCount, out var band))
            return false;

        if (!int.TryParse(match.Groups[2].Value, out var degrees)) return false;

        // Una marcación fuera de rango no se toma módulo: tomarlo haría que C400
        // disparara silenciosamente a C040. El jugador escribió algo que no existe y
        // el sistema debe no hacer nada (TDD §6.2).
        if (degrees is < 0 or > 359) return false;

        target = new TargetCoordinate(new Bearing(degrees), band);
        return true;
    }
}
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 209`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Controller/CommandParser.cs tests/Sonar.Controller.Tests/CommandParserTests.cs
git commit -m "feat(controller): add command parser"
```

---

## Task 5: Vistas y constructor de snapshot

**Files:**
- Create: `src/Sonar.Controller/Presentation/Views.cs`
- Create: `src/Sonar.Controller/Presentation/SnapshotBuilder.cs`
- Test: `tests/Sonar.Controller.Tests/SnapshotBuilderTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Controller.Tests/SnapshotBuilderTests.cs`:

```csharp
using System;
using System.Linq;
using Sonar.Controller.Presentation;
using Sonar.Core;
using Sonar.Core.Entities;
using Sonar.Core.Sensing;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Controller.Tests;

public class SnapshotBuilderTests
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

    private static WorldState BaseState() =>
        WorldState.CreateInitial(Config(), new Bearing(0), 0.05);

    [Fact]
    public void ContactView_UsesTheReadingPositionNotTheRealOne()
    {
        // Esta es la línea de defensa más fuerte del diseño: la capa de presentación
        // no puede dibujar la verdad porque no la recibe.
        var config = Config();
        var state = BaseState()
            .WithContacts(new[]
            {
                new Contact
                {
                    Id = new ContactId(1), Kind = ContactKind.Enemy,
                    Position = new Vec2(0.90, 0.0),      // dónde está ahora
                    Velocity = Vec2.Zero, Alive = true, NextFireTick = int.MaxValue,
                },
            });

        state = state with
        {
            Tick = 100,
            Sonar = state.Sonar with
            {
                Readings = new[]
                {
                    new Reading
                    {
                        ContactId = new ContactId(1),
                        Position = new Vec2(0.50, 0.0),  // dónde el barrido lo vio
                        ObservedAtTick = 60,
                    },
                },
            },
        };

        var snapshot = SnapshotBuilder.Build(state, config.BuildBandLayout());

        var view = Assert.Single(snapshot.Contacts);
        Assert.Equal(0.50, view.Position.X, precision: 10);
        Assert.Equal(40, view.AgeTicks);
    }

    [Fact]
    public void ContactWithoutReading_IsNotInTheSnapshot()
    {
        var config = Config();
        var state = BaseState().WithContacts(new[]
        {
            Contact.CreateInert(new ContactId(1), new Vec2(0.5, 0.0)),
        });

        var snapshot = SnapshotBuilder.Build(state, config.BuildBandLayout());

        Assert.Empty(snapshot.Contacts);
    }

    [Fact]
    public void ContactView_CarriesBearingAndBandOfTheReading()
    {
        var config = Config();
        var state = BaseState().WithContacts(new[]
        {
            Contact.CreateInert(new ContactId(1), new Vec2(0.0, 0.0)),
        });
        state = state with
        {
            Sonar = state.Sonar with
            {
                Readings = new[]
                {
                    new Reading
                    {
                        ContactId = new ContactId(1),
                        Position = new Vec2(0.55, 0.0),   // este, banda C
                        ObservedAtTick = 0,
                    },
                },
            },
        };

        var view = Assert.Single(SnapshotBuilder.Build(state, config.BuildBandLayout()).Contacts);

        Assert.Equal(90, view.BearingDegrees);
        Assert.Equal('C', view.BandLetter);
    }

    [Fact]
    public void OwnTorpedoes_AppearWithTheirRealPosition()
    {
        var config = Config();
        var layout = config.BuildBandLayout();
        var state = BaseState().WithTorpedoes(new[]
        {
            Torpedo.CreateOwn(new TorpedoId(1),
                new TargetCoordinate(new Bearing(90), new Band(2)), layout, 0.7),
        });

        var snapshot = SnapshotBuilder.Build(state, layout);

        var view = Assert.Single(snapshot.Torpedoes);
        Assert.Equal(0.10, view.Position.X, precision: 10);
        Assert.Empty(snapshot.EnemyTorpedoes);
    }

    [Fact]
    public void EnemyTorpedoes_AppearInTheirOwnList()
    {
        var config = Config();
        var state = BaseState().WithTorpedoes(new[]
        {
            Torpedo.CreateEnemy(new TorpedoId(2), new Vec2(0.0, -0.8), 0.45),
        });

        var snapshot = SnapshotBuilder.Build(state, config.BuildBandLayout());

        Assert.Single(snapshot.EnemyTorpedoes);
        Assert.Empty(snapshot.Torpedoes);
    }

    [Fact]
    public void ResourcesAreCarriedThrough()
    {
        var config = Config();
        var snapshot = SnapshotBuilder.Build(BaseState(), config.BuildBandLayout());

        Assert.Equal(4, snapshot.AmmoLoaded);
        Assert.Equal(12, snapshot.AmmoReserve);
        Assert.Equal(3, snapshot.DefensesAvailable);
        Assert.Equal(0, snapshot.HitsTaken);
        Assert.False(snapshot.Destroyed);
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'SnapshotBuilder' could not be found`.

- [ ] **Step 3: Implementar vistas y constructor**

Crea `src/Sonar.Controller/Presentation/Views.cs`:

```csharp
using System.Collections.Generic;
using Sonar.Core.Spatial;

namespace Sonar.Controller.Presentation;

/// <summary>
/// Contacto tal como el jugador puede verlo: la posición es la de la <b>lectura</b> del
/// último barrido, no la real. El gating vive en el contrato de datos, no en una
/// convención — la capa de presentación no puede dibujar la verdad porque no la recibe.
/// </summary>
public sealed record ContactView(
    int Id, Vec2 Position, int BearingDegrees, char BandLetter, int AgeTicks);

/// <summary>Torpedo propio. Posición real, cada tick: es tuyo, sabes dónde está.</summary>
public sealed record TorpedoView(int Id, Vec2 Position, int BearingDegrees, double RunToRadius);

/// <summary>Torpedo enemigo. Posición real: se detecta por ruido, no por barrido.</summary>
public sealed record EnemyTorpedoView(int Id, Vec2 Position);

public sealed record DefenseView(int Id, Vec2 Position);

public sealed record WorldSnapshot(
    int Tick,
    double SweepDegrees,
    bool SweepRunning,
    IReadOnlyList<ContactView> Contacts,
    IReadOnlyList<TorpedoView> Torpedoes,
    IReadOnlyList<EnemyTorpedoView> EnemyTorpedoes,
    IReadOnlyList<DefenseView> Defenses,
    int AmmoLoaded,
    int AmmoReserve,
    int DefensesAvailable,
    bool ReloadInProgress,
    int HitsTaken,
    bool Destroyed);
```

Crea `src/Sonar.Controller/Presentation/SnapshotBuilder.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Sonar.Core;
using Sonar.Core.Entities;
using Sonar.Core.Spatial;

namespace Sonar.Controller.Presentation;

public static class SnapshotBuilder
{
    public static WorldSnapshot Build(WorldState state, BandLayout layout)
    {
        var contacts = new List<ContactView>();
        foreach (var reading in state.Sonar.Readings)
        {
            var bearing = Bearing.FromVector(reading.Position);
            var band = layout.TryBandAt(reading.Position.Length, out var b) ? b.Letter : '-';
            contacts.Add(new ContactView(
                reading.ContactId.Value,
                reading.Position,
                bearing.Degrees,
                band,
                state.Tick - reading.ObservedAtTick));
        }

        var own = state.Torpedoes
            .Where(t => t.Side == TorpedoSide.Own)
            .Select(t => new TorpedoView(t.Id.Value, t.Position, t.Bearing.Degrees, t.RunToRadius))
            .ToArray();

        var enemy = state.Torpedoes
            .Where(t => t.Side == TorpedoSide.Enemy)
            .Select(t => new EnemyTorpedoView(t.Id.Value, t.Position))
            .ToArray();

        var defenses = state.Defenses
            .Where(d => d.Alive)
            .Select(d => new DefenseView(d.Id.Value, d.Position))
            .ToArray();

        return new WorldSnapshot(
            state.Tick,
            state.Sonar.SweepDegrees,
            state.Sonar.SweepRunning,
            contacts,
            own,
            enemy,
            defenses,
            state.Sub.AmmoLoaded,
            state.Sub.AmmoReserve,
            state.Sub.DefensesAvailable,
            state.Sub.ReloadInProgress,
            state.Sub.HitsTaken,
            state.Sub.Destroyed);
    }
}
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 215`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Controller/Presentation tests/Sonar.Controller.Tests/SnapshotBuilderTests.cs
git commit -m "feat(controller): add presentation views and snapshot builder"
```

---

## Task 6: GameController y acumulador de paso fijo

**Files:**
- Create: `src/Sonar.Controller/GameController.cs`
- Test: `tests/Sonar.Controller.Tests/GameControllerTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Controller.Tests/GameControllerTests.cs`:

```csharp
using System.Linq;
using Sonar.Core;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Controller.Tests;

public class GameControllerTests
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

    private static GameController New() => new(Config(), seed: 1, course: new Bearing(0), speed: 0.05);

    [Fact]
    public void Advance_RunsOneTickPerTickDuration()
    {
        var controller = New();
        controller.Advance(1.0 / 60.0);
        Assert.Equal(1, controller.Snapshot.Tick);
    }

    [Fact]
    public void Advance_AccumulatesPartialTime()
    {
        var controller = New();
        controller.Advance(1.0 / 120.0);
        Assert.Equal(0, controller.Snapshot.Tick);

        controller.Advance(1.0 / 120.0);
        Assert.Equal(1, controller.Snapshot.Tick);
    }

    [Fact]
    public void Advance_RunsMultipleTicksForALargeDelta()
    {
        var controller = New();
        controller.Advance(3.0 / 60.0);
        Assert.Equal(3, controller.Snapshot.Tick);
    }

    [Fact]
    public void Advance_CapsCatchUpAndDiscardsTheRest()
    {
        // Sin tope, un congelamiento de la ventana produce una avalancha de ticks que el
        // jugador experimenta como un salto. Es la espiral de la muerte clásica (TDD §6.3).
        var controller = New();
        controller.Advance(10.0);   // diez segundos de golpe

        Assert.Equal(5, controller.Snapshot.Tick);   // MaxCatchUpTicks
    }

    [Fact]
    public void Advance_DoesNotCarryDiscardedTimeIntoTheNextCall()
    {
        var controller = New();
        controller.Advance(10.0);
        controller.Advance(1.0 / 60.0);

        Assert.Equal(6, controller.Snapshot.Tick);
    }

    [Fact]
    public void SubmitInput_QueuesForTheNextTickBoundary()
    {
        var controller = New();
        controller.SubmitInput("STATUS");

        // Todavía no se ejecutó nada.
        Assert.Equal(0, controller.Snapshot.Tick);

        var events = controller.Advance(1.0 / 60.0);
        Assert.Contains(events, e => e is StatusReported);
    }

    [Fact]
    public void SubmitInput_UnknownText_ProducesNotRecognizedEvent()
    {
        var controller = New();
        controller.SubmitInput("what is this");
        var events = controller.Advance(1.0 / 60.0);

        var evt = (InputNotRecognized)Assert.Single(events, e => e is InputNotRecognized);
        Assert.Equal("what is this", evt.RawText);
    }

    [Fact]
    public void SubmitInput_EmptyText_QueuesNothing()
    {
        var controller = New();
        controller.SubmitInput("   ");
        var events = controller.Advance(1.0 / 60.0);

        Assert.Empty(events);
    }

    [Fact]
    public void MultipleInputs_AreAppliedInSubmissionOrder()
    {
        var controller = New();
        controller.SubmitInput("C047");
        controller.SubmitInput("C048");
        controller.Advance(1.0 / 60.0);

        var fired = controller.LastEvents.OfType<TorpedoFired>().ToArray();
        Assert.Equal(2, fired.Length);
        Assert.Equal(47, fired[0].BearingDegrees);
        Assert.Equal(48, fired[1].BearingDegrees);
    }

    [Fact]
    public void Snapshot_IsRebuiltEachAdvance()
    {
        var controller = New();
        var first = controller.Snapshot;
        controller.Advance(1.0 / 60.0);
        Assert.NotSame(first, controller.Snapshot);
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'GameController' could not be found`.

- [ ] **Step 3: Implementar `GameController`**

Crea `src/Sonar.Controller/GameController.cs`:

```csharp
using System;
using System.Collections.Generic;
using Sonar.Controller.Presentation;
using Sonar.Core;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;

namespace Sonar.Controller;

/// <summary>
/// Frontera entre la presentación y la simulación. Acumula tiempo real y avanza Core en
/// pasos fijos; Core nunca ve tiempo de pared.
/// </summary>
public sealed class GameController
{
    private readonly SimulationConfig _config;
    private readonly BandLayout _layout;
    private readonly CommandParser _parser;
    private readonly Simulation _simulation;
    private readonly List<Command> _pending = new();

    private double _accumulator;

    public WorldSnapshot Snapshot { get; private set; }

    /// <summary>Eventos producidos por la última llamada a Advance.</summary>
    public IReadOnlyList<GameEvent> LastEvents { get; private set; } = Array.Empty<GameEvent>();

    public GameController(SimulationConfig config, int seed, Bearing course, double speed)
    {
        config.Validate();
        _config = config;
        _layout = config.BuildBandLayout();
        _parser = new CommandParser(_layout.Count);
        _simulation = new Simulation(config, new SeededRandomSource(seed),
            WorldState.CreateInitial(config, course, speed));
        Snapshot = SnapshotBuilder.Build(_simulation.State, _layout);
    }

    /// <summary>Coloca contactos. En M1 lo usa el arranque; después será el guion de misión.</summary>
    public void SetContacts(IEnumerable<Contact> contacts)
    {
        _simulation.SetContacts(contacts);
        Snapshot = SnapshotBuilder.Build(_simulation.State, _layout);
    }

    /// <summary>
    /// Encola texto crudo. Se parsea aquí y se aplica en la próxima frontera de tick,
    /// nunca a mitad: es lo que mantiene la simulación reproducible.
    /// </summary>
    public void SubmitInput(string rawText)
    {
        var command = _parser.Parse(rawText);
        if (command is not null) _pending.Add(command);
    }

    /// <summary>
    /// Acumula tiempo real y ejecuta los ticks completos que quepan, hasta
    /// MaxCatchUpTicks. El excedente se descarta: sin tope, un congelamiento produce
    /// una avalancha que el jugador percibe como un salto.
    /// </summary>
    public IReadOnlyList<GameEvent> Advance(double deltaSeconds)
    {
        _accumulator += deltaSeconds;

        var events = new List<GameEvent>();
        var executed = 0;

        while (_accumulator >= _config.TickDurationSeconds && executed < _config.MaxCatchUpTicks)
        {
            _accumulator -= _config.TickDurationSeconds;

            var batch = _pending.ToArray();
            _pending.Clear();
            events.AddRange(_simulation.Tick(batch));
            executed++;
        }

        // Descartar el excedente en vez de arrastrarlo al siguiente frame.
        if (_accumulator >= _config.TickDurationSeconds) _accumulator = 0;

        Snapshot = SnapshotBuilder.Build(_simulation.State, _layout);
        LastEvents = events;
        return events;
    }
}
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 225`

- [ ] **Step 5: Verificar que el Controller no adquirió Godot**

```bash
grep -rn "Godot" src/Sonar.Controller/ || echo "sin referencias a Godot"
```

Expected: `sin referencias a Godot`

- [ ] **Step 6: Commit**

```bash
git add src/Sonar.Controller/GameController.cs tests/Sonar.Controller.Tests/GameControllerTests.cs
git commit -m "feat(controller): add GameController with capped fixed-step accumulator"
```

---

## Task 7: Proyecto Godot

**Files:**
- Create: `src/Sonar.Game/project.godot`
- Create: `src/Sonar.Game/Sonar.Game.csproj`

- [ ] **Step 1: Crear el proyecto desde el editor de Godot**

Abre Godot, crea un proyecto nuevo en `src/Sonar.Game`, nómbralo `Sonar`, y elige el renderizador **Compatibility** — el juego es líneas y texto sobre negro, no necesita más.

Luego, desde el editor: menú **Project → Tools → C# → Create C# solution**. Godot generará `Sonar.Game.csproj`.

- [ ] **Step 2: Enlazar el proyecto Godot con el Controller**

```bash
dotnet add src/Sonar.Game/Sonar.Game.csproj reference src/Sonar.Controller/Sonar.Controller.csproj
```

> No agregues `Sonar.Game` a `Sonar.sln`. Godot mantiene su propia solución, y mezclarlas hace
> que `dotnet test` intente compilar el proyecto de Godot sin sus ensamblados. La suite corre
> sobre `Sonar.sln`; el juego se compila desde Godot.

- [ ] **Step 3: Verificar que la suite sigue verde**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 225`

- [ ] **Step 4: Verificar que Godot compila**

```bash
dotnet build src/Sonar.Game/Sonar.Game.csproj
```

Expected: `Build succeeded.`

Si falla por versión de .NET, revisa que el TFM de `Sonar.Game.csproj` coincida con el de
`Sonar.Core` y `Sonar.Controller`.

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Game
git commit -m "chore: add Godot project referencing the controller"
```

---

## Task 8: Dibujo del sonar

**Files:**
- Create: `src/Sonar.Game/SonarView.cs`

- [ ] **Step 1: Escribir el nodo de dibujo**

Crea `src/Sonar.Game/SonarView.cs`:

```csharp
using Godot;
using Sonar.Controller.Presentation;

namespace Sonar.Game;

/// <summary>
/// Dibuja el snapshot. No sabe nada de reglas: no decide si un disparo acierta, no mide
/// tiempo, no genera azar. Solo pinta lo que el Controller le entrega.
///
/// Deliberadamente feo: M1 valida si extrapolar lecturas es divertido, no si se ve bien.
/// Pulir esto es señal de haberse salido del milestone.
/// </summary>
public partial class SonarView : Node2D
{
    private static readonly Color Grid = new(0.29f, 0.87f, 0.50f, 0.30f);
    private static readonly Color GridStrong = new(0.29f, 0.87f, 0.50f, 0.75f);
    private static readonly Color Label = new(0.73f, 0.97f, 0.82f);
    private static readonly Color ContactColor = new(0.36f, 1.0f, 0.61f);
    private static readonly Color OwnTorpedo = new(1.0f, 0.82f, 0.36f);
    private static readonly Color EnemyTorpedo = new(1.0f, 0.42f, 0.42f);
    private static readonly Color DefenseColor = new(0.56f, 0.83f, 1.0f);
    private static readonly Color SubColor = new(0.92f, 1.0f, 0.95f);

    private const float Radius = 320f;
    private const int BandCount = 5;

    private WorldSnapshot? _snapshot;
    private Font _font = null!;

    public override void _Ready() => _font = ThemeDB.FallbackFont;

    public void SetSnapshot(WorldSnapshot snapshot)
    {
        _snapshot = snapshot;
        QueueRedraw();
    }

    /// <summary>Espacio de simulación a píxeles. El submarino está en el origen.</summary>
    private static Vector2 ToScreen(Sonar.Core.Spatial.Vec2 v) =>
        new((float)v.X * Radius, (float)v.Y * Radius);

    public override void _Draw()
    {
        if (_snapshot is not { } snap) return;

        // Anillos de banda
        for (var i = 1; i <= BandCount; i++)
            DrawArc(Vector2.Zero, Radius * i / BandCount, 0, Mathf.Tau, 96, Grid, 1.5f);

        DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 128, GridStrong, 2.5f);

        // Marcas de marcación cada 30 grados
        for (var d = 0; d < 360; d += 30)
        {
            var rad = Mathf.DegToRad(d);
            var dir = new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad));
            DrawLine(dir * (Radius - 16), dir * Radius, GridStrong, 2f);
            DrawString(_font, dir * (Radius + 22), d.ToString("D3"),
                HorizontalAlignment.Center, -1, 14, Label);
        }

        // Haz
        var sweepRad = Mathf.DegToRad((float)snap.SweepDegrees);
        var sweepDir = new Vector2(Mathf.Sin(sweepRad), -Mathf.Cos(sweepRad));
        DrawLine(Vector2.Zero, sweepDir * Radius,
            snap.SweepRunning ? GridStrong : Grid, 2f);

        // Contactos: posición de la lectura, atenuada por edad
        foreach (var contact in snap.Contacts)
        {
            var age = Mathf.Clamp(1f - contact.AgeTicks / 210f, 0.2f, 1f);
            var position = ToScreen(contact.Position);
            DrawCircle(position, 7f, new Color(ContactColor, age));
            DrawString(_font, position + new Vector2(0, -18),
                $"{contact.BearingDegrees:D3} {contact.BandLetter}",
                HorizontalAlignment.Center, -1, 14, new Color(Label, age));
        }

        // Torpedos propios: posición real, con su punto de detonación marcado
        foreach (var torpedo in snap.Torpedoes)
        {
            var position = ToScreen(torpedo.Position);
            DrawLine(Vector2.Zero, position, OwnTorpedo, 2.5f);
            DrawCircle(position, 4.5f, OwnTorpedo);

            var rad = Mathf.DegToRad(torpedo.BearingDegrees);
            var mark = new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad))
                       * (float)torpedo.RunToRadius * Radius;
            DrawArc(mark, 14f, 0, Mathf.Tau, 24, new Color(OwnTorpedo, 0.4f), 1.5f);
        }

        // Torpedos enemigos: se oyen, no se barren
        foreach (var torpedo in snap.EnemyTorpedoes)
        {
            var position = ToScreen(torpedo.Position);
            DrawLine(position + new Vector2(-6, 0), position + new Vector2(6, 0), EnemyTorpedo, 2.5f);
            DrawLine(position + new Vector2(0, -6), position + new Vector2(0, 6), EnemyTorpedo, 2.5f);
        }

        foreach (var defense in snap.Defenses)
            DrawArc(ToScreen(defense.Position), 10f, 0, Mathf.Tau, 24, DefenseColor, 2.5f);

        DrawCircle(Vector2.Zero, 6f, SubColor);
    }
}
```

- [ ] **Step 2: Verificación manual**

Se completa en la Tarea 9, cuando exista la escena que alimenta este nodo.

- [ ] **Step 3: Commit**

```bash
git add src/Sonar.Game/SonarView.cs
git commit -m "feat(game): draw the sonar snapshot"
```

---

## Task 9: Escena, terminal y bucle

**Files:**
- Create: `src/Sonar.Game/Main.cs`
- Create: `src/Sonar.Game/Main.tscn` (desde el editor)

- [ ] **Step 1: Escribir el nodo principal**

Crea `src/Sonar.Game/Main.cs`:

```csharp
using System.Collections.Generic;
using Godot;
using Sonar.Controller;
using Sonar.Core;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;

namespace Sonar.Game;

public partial class Main : Node
{
    private const string ConfigPath = "res://../../config/simulation.working.json";

    private GameController _controller = null!;
    private SonarView _sonar = null!;
    private LineEdit _input = null!;
    private RichTextLabel _log = null!;
    private Label _hud = null!;

    public override void _Ready()
    {
        _sonar = GetNode<SonarView>("%SonarView");
        _input = GetNode<LineEdit>("%CommandInput");
        _log = GetNode<RichTextLabel>("%Log");
        _hud = GetNode<Label>("%Hud");

        var json = FileAccess.GetFileAsString(ConfigPath);
        if (string.IsNullOrEmpty(json))
        {
            GD.PushError($"No se pudo leer la configuración en {ConfigPath}");
            return;
        }

        var config = SimulationConfigLoader.LoadFromJson(json);
        _controller = new GameController(config, seed: 20260811,
            course: new Bearing(0), speed: 0.05);

        _controller.SetContacts(BuildOpeningContacts(config));

        _input.TextSubmitted += OnCommandSubmitted;
        _input.GrabFocus();

        Append("[color=#5d8f74]SONAR M1 — prototipo de validación[/color]");
        Append("[color=#5d8f74]escribe C047 y Enter. RELOAD · SONAR · STATUS · DEF C047[/color]");
    }

    private static IEnumerable<Contact> BuildOpeningContacts(SimulationConfig config)
    {
        var spawner = new ContactSpawner(config);
        var enemies = spawner.SpawnEnemies(
            spawnTick: 0, firstId: 100,
            positions: new[]
            {
                new Vec2(0.10, -0.78), new Vec2(0.74, 0.18),
                new Vec2(-0.62, 0.44), new Vec2(-0.30, -0.70),
            },
            velocities: new[]
            {
                new Vec2(0.03, 0.06), new Vec2(-0.07, 0.02),
                new Vec2(0.05, -0.04), new Vec2(0.04, 0.05),
            });

        foreach (var enemy in enemies) yield return enemy;

        // Referencia inerte: deriva con el mundo mientras las defensas no.
        // Es lo que hace legible la anomalía del §25 — ver spec §4.9.
        yield return Contact.CreateInert(new ContactId(200), new Vec2(0.55, 0.55));
    }

    public override void _Process(double delta)
    {
        if (_controller is null) return;

        foreach (var evt in _controller.Advance(delta)) Report(evt);

        _sonar.SetSnapshot(_controller.Snapshot);

        var snap = _controller.Snapshot;
        _hud.Text = $"tick {snap.Tick}   munición {snap.AmmoLoaded}/{snap.AmmoReserve}   " +
                    $"defensas {snap.DefensesAvailable}   impactos {snap.HitsTaken}/2" +
                    (snap.ReloadInProgress ? "   RECARGANDO" : string.Empty) +
                    (snap.Destroyed ? "   DESTRUIDO" : string.Empty);
    }

    private void OnCommandSubmitted(string text)
    {
        Append($"[color=#fde047]> {text}[/color]");
        _controller.SubmitInput(text);
        _input.Clear();
    }

    /// <summary>
    /// Cuatro registros visuales, el cuarto reservado (spec §4.7). Aquí solo existen los
    /// tres primeros: el registro sin autor no se usa en M1.
    /// </summary>
    private void Report(GameEvent evt)
    {
        switch (evt)
        {
            case TorpedoFired e:
                Instrument($"TORPEDO {e.TorpedoId} · {e.BandLetter}{e.BearingDegrees:D3}");
                break;
            case ContactDestroyed e:
                Instrument($"IMPACTO · CONTACTO {e.ContactId} DESTRUIDO · {e.Cause}");
                break;
            case TorpedoDetonated { HitSomething: false }:
                Instrument("SIN CONTACTO · DETONACIÓN EN VACÍO");
                break;
            case FireRejected e: Instrument($"DISPARO RECHAZADO · {e.Reason}"); break;
            case DefenseDeployed e: Instrument($"DEFENSA {e.DefenseId} DESPLEGADA"); break;
            case DefenseRejected e: Instrument($"DEFENSA RECHAZADA · {e.Reason}"); break;
            case ReloadStarted: Instrument("RECARGA EN CURSO"); break;
            case ReloadCompleted e: Instrument($"RECARGA COMPLETA · {e.AmmoLoaded}"); break;
            case SonarToggled e: Instrument(e.Running ? "BARRIDO ACTIVO" : "BARRIDO DETENIDO"); break;
            case StatusReported e:
                Instrument($"MUNICIÓN {e.AmmoLoaded}/{e.AmmoReserve} · DEFENSAS {e.Defenses}");
                break;
            case EnemyTorpedoFired: Instrument("RUIDO DE LANZAMIENTO"); break;
            case SubHit e: Instrument($"IMPACTO RECIBIDO · {e.HitsTaken}/2"); break;
            case SubDestroyed: Instrument("CASCO COMPROMETIDO"); break;
            case InputNotRecognized: break;   // ya se registró al escribirlo
        }
    }

    private void Instrument(string text) => Append($"[color=#5d8f74]  {text}[/color]");

    private void Append(string bbcode)
    {
        _log.AppendText(bbcode + "\n");
        _log.ScrollToLine(_log.GetLineCount() - 1);
    }
}
```

- [ ] **Step 2: Construir la escena en el editor de Godot**

Crea `Main.tscn` con esta jerarquía. Marca con **Access as Unique Name** (el botón `%`) los nodos indicados:

```
Main               (Node)                → script Main.cs
├── Background      (ColorRect)          → color #04120a, anclado a pantalla completa
├── SonarView       (Node2D)   %         → script SonarView.cs, posición (420, 400)
├── Hud             (Label)    %         → arriba a la izquierda
├── Log             (RichTextLabel) %    → panel derecho, BBCode habilitado, scroll activo
└── CommandInput    (LineEdit) %         → franja inferior
```

Fija `Main.tscn` como escena principal en **Project → Project Settings → Application → Run**.

- [ ] **Step 3: Verificación manual — el sonar funciona**

Ejecuta el proyecto desde Godot (F5). Comprueba una por una:

| Verificación | Esperado |
|---|---|
| El haz gira | Una línea recorre el círculo continuamente |
| Aparecen contactos | Puntos verdes con etiqueta `047 C` al pasar el haz |
| Las etiquetas envejecen | Se atenúan hasta que el haz vuelve a pasar |
| Escribir `C047` + Enter | Sale un torpedo amarillo por esa marcación |
| El torpedo detona | Círculo tenue en el punto de corrida, mensaje en el log |
| Escribir `hola` | Aparece en el log y no pasa nada más |
| Escribir `C400` | Igual: se registra y no hace nada |
| `RELOAD` | El HUD muestra RECARGANDO y el haz **sigue girando** |
| `SONAR` | El haz se congela; las etiquetas dejan de refrescarse |
| `DEF C090` | Círculo azul en esa coordenada |
| Torpedos enemigos | Cruces rojas que se acercan al centro |
| Dos impactos | El HUD muestra `2/2` y `DESTRUIDO`, y todo se detiene |

- [ ] **Step 4: Verificación manual — el gating es real**

Esta es la verificación que valida el diseño entero:

1. Detén el barrido con `SONAR`.
2. Observa una etiqueta de contacto.
3. Espera diez segundos.

**Esperado:** la etiqueta no se mueve, aunque el contacto sí se está moviendo por debajo.
Al reactivar el barrido con `SONAR`, la etiqueta salta a la posición nueva.

Si la etiqueta sigue al contacto en tiempo real, el gating se rompió — lo más probable es que
alguien esté leyendo `Contact.Position` en vez de `Reading.Position`.

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Game
git commit -m "feat(game): add main scene, terminal and event log"
```

---

## Task 10: Instrumentación de las preguntas de M1

M1 termina cuando las ocho preguntas tienen respuesta anotada. Esta tarea construye lo mínimo
para medir en vez de opinar.

**Files:**
- Create: `src/Sonar.Game/SessionMetrics.cs`
- Modify: `src/Sonar.Game/Main.cs`
- Create: `docs/development/m1-findings.md`

- [ ] **Step 1: Escribir el registrador de métricas**

Crea `src/Sonar.Game/SessionMetrics.cs`:

```csharp
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace Sonar.Game;

/// <summary>
/// Mide la pregunta 2 de M1 — tiempo medio por comando — y la tasa de acierto que sirve
/// de proxy para la pregunta 1. Escribe a user:// al cerrar.
/// </summary>
public sealed class SessionMetrics
{
    private readonly List<double> _composeTimes = new();
    private ulong _composeStartedMsec;
    private bool _composing;

    public int ShotsFired { get; private set; }
    public int ContactsDestroyed { get; private set; }

    public double AverageComposeSeconds =>
        _composeTimes.Count == 0 ? 0 : _composeTimes.Average();

    public double HitRate => ShotsFired == 0 ? 0 : (double)ContactsDestroyed / ShotsFired;

    /// <summary>Primera tecla tras el último Enter: arranca el cronómetro de composición.</summary>
    public void NoteKeystroke()
    {
        if (_composing) return;
        _composing = true;
        _composeStartedMsec = Time.GetTicksMsec();
    }

    public void NoteSubmit()
    {
        if (!_composing) return;
        _composeTimes.Add((Time.GetTicksMsec() - _composeStartedMsec) / 1000.0);
        _composing = false;
    }

    public void NoteShot() => ShotsFired++;
    public void NoteKill() => ContactsDestroyed++;

    public void WriteReport()
    {
        using var file = FileAccess.Open("user://m1-session.csv", FileAccess.ModeFlags.Write);
        if (file is null) return;

        file.StoreLine("metric,value");
        file.StoreLine($"commands,{_composeTimes.Count}");
        file.StoreLine($"avg_compose_seconds,{AverageComposeSeconds.ToString("F3", CultureInfo.InvariantCulture)}");
        file.StoreLine($"shots_fired,{ShotsFired}");
        file.StoreLine($"contacts_destroyed,{ContactsDestroyed}");
        file.StoreLine($"hit_rate,{HitRate.ToString("F3", CultureInfo.InvariantCulture)}");
    }
}
```

- [ ] **Step 2: Conectarlo en `Main.cs`**

Agrega el campo y las llamadas:

```csharp
    private readonly SessionMetrics _metrics = new();
```

En `_Ready`, después de `_input.GrabFocus();`:

```csharp
        _input.TextChanged += _ => _metrics.NoteKeystroke();
```

En `OnCommandSubmitted`, como primera línea:

```csharp
        _metrics.NoteSubmit();
```

En `Report`, dentro de los casos correspondientes:

```csharp
            case TorpedoFired e:
                _metrics.NoteShot();
                Instrument($"TORPEDO {e.TorpedoId} · {e.BandLetter}{e.BearingDegrees:D3}");
                break;
            case ContactDestroyed e:
                _metrics.NoteKill();
                Instrument($"IMPACTO · CONTACTO {e.ContactId} DESTRUIDO · {e.Cause}");
                break;
```

Y agrega el volcado al cerrar:

```csharp
    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) _metrics.WriteReport();
    }
```

- [ ] **Step 3: Crear la plantilla de hallazgos**

Crea `docs/development/m1-findings.md`:

```markdown
# M1 — Hallazgos

M1 termina cuando las ocho preguntas tienen respuesta anotada aquí y los números están en
`config/simulation.working.json`. **No termina cuando "se siente bien"** — eso no es un
criterio, es una sensación, y con ella el milestone se estira indefinidamente.

Si la respuesta a la pregunta 1 es negativa, el milestone está igualmente completo: cumplió
su función, que era avisar a tiempo.

| # | Pregunta | Respuesta | Evidencia |
|---|---|---|---|
| 1 | ¿Extrapolar lecturas es satisfactorio? | | tasa de acierto entre sesiones |
| 2 | Tiempo medio por comando | | `user://m1-session.csv` |
| 3 | Máximo de contactos jugable | | |
| 4 | Período del barrido | | probado 2,5 / 3,5 / 5 s |
| 5 | Número de bandas y radios | | probado 5 y 8 |
| 6 | ¿Hace falta repetir y ajustar? | | |
| 7 | ¿La munición aprieta? | | |
| 8 | ¿La fase extrema se dispara o se sobrevive? | | |

## Decisiones que M1 puede invalidar

M1 tiene autoridad para revertir decisiones del spec. Si algo de esto aparece, anótalo aquí
y vuelve al spec antes de escribir narrativa:

- [ ] La extrapolación se siente como aritmética, no como pilotear un instrumento
- [ ] El tiempo por comando no cabe en el ritmo del combate
- [ ] El gating por clase hace el bullet hell ingobernable
- [ ] La banda como ventana no produce decisiones interesantes
```

- [ ] **Step 4: Verificación manual**

Ejecuta, juega una sesión de unos minutos y cierra la ventana. Comprueba que existe
`m1-session.csv` en el directorio de datos de usuario de Godot y que
`avg_compose_seconds` trae un número razonable.

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Game docs/development/m1-findings.md
git commit -m "feat(game): instrument M1 questions and add findings template"
```

---

## Definition of Done — M1 completo

- [ ] `dotnet test` reporta 225 tests pasando, 0 fallando
- [ ] `grep -rn "Godot" src/Sonar.Core/ src/Sonar.Controller/` no devuelve nada
- [ ] `grep -rn "Dictionary\|HashSet" src/Sonar.Core/` no devuelve nada
- [ ] El juego arranca desde Godot y se puede jugar con el teclado
- [ ] La verificación de gating de la Tarea 9 pasa: con el barrido detenido, las etiquetas no siguen a los contactos
- [ ] El jugador puede morir de dos impactos
- [ ] `m1-session.csv` se escribe al cerrar
- [ ] `docs/development/m1-findings.md` existe y está vacío, esperando datos

## Qué sigue

Jugar. M1 no termina cuando el código compila — termina cuando las ocho preguntas de
`m1-findings.md` tienen respuesta y los valores de balance están anotados.

Después de eso, y solo después, vienen los milestones de daño y reparación (§21), sonido,
la capa narrativa, y M2 — convertir esto en la demo de Steam manteniendo intacta la regla
de que la demo enseña las cuatro reglas del instrumento y no contiene ni una violación.
