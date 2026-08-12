# M1 · Plan 1 — Fundación Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Levantar el repositorio, la solución .NET y las primitivas espaciales y de configuración sobre las que se construye toda la simulación de SONAR, con tests que pasan.

**Architecture:** Tres ensamblados en cadena de una sola dirección — `Sonar.Game` (Godot) → `Sonar.Controller` → `Sonar.Core`. Ni Core ni Controller referencian Godot; esa ausencia es el mecanismo de enforcement de la frontera. Este plan construye únicamente `Sonar.Core` y su proyecto de tests; los otros dos llegan en planes posteriores.

**Tech Stack:** C# / .NET (versión fijada en la Tarea 1), xUnit, Godot con soporte C# (versión fijada en la Tarea 1), git.

**Documentos de referencia:**
- Spec: `docs/superpowers/specs/2026-08-11-sonar-preproduction-design.md`
- TDD: `docs/tdd/M1-core-simulation.md`

---

## Estructura de archivos

Al terminar este plan existirán:

| Archivo | Responsabilidad |
|---|---|
| `global.json` | Fija la versión del SDK de .NET |
| `Sonar.sln` | Solución que agrupa los proyectos |
| `.gitignore` | Excluye artefactos de build, Godot y del entorno |
| `src/Sonar.Core/Sonar.Core.csproj` | Librería de dominio y simulación. Sin referencias externas |
| `src/Sonar.Core/Spatial/Vec2.cs` | Vector 2D de doble precisión. Reemplaza a `Godot.Vector2` dentro de Core |
| `src/Sonar.Core/Spatial/Bearing.cs` | Marcación en grados enteros, normalizada, con conversión a y desde vector |
| `src/Sonar.Core/Spatial/Band.cs` | Índice de banda y su letra derivada |
| `src/Sonar.Core/Spatial/BandLayout.cs` | Geometría de las bandas: radios interior, exterior y central; banda que contiene un radio |
| `src/Sonar.Core/Spatial/TargetCoordinate.cs` | Objetivo escrito por el jugador: marcación más banda |
| `src/Sonar.Core/SimulationConfig.cs` | Todos los valores ajustables, con validación de invariantes |
| `src/Sonar.Core/IRandomSource.cs` | Interfaz de azar inyectable |
| `src/Sonar.Core/SeededRandomSource.cs` | Implementación determinista sembrada |
| `tests/Sonar.Core.Tests/Sonar.Core.Tests.csproj` | Proyecto de tests |
| `tests/Sonar.Core.Tests/Spatial/*.cs` | Tests de las primitivas espaciales |
| `tests/Sonar.Core.Tests/SimulationConfigTests.cs` | Tests de validación de configuración |
| `tests/Sonar.Core.Tests/SeededRandomSourceTests.cs` | Tests de reproducibilidad del azar |

Cada archivo tiene una responsabilidad y se puede leer entero de una vez. `Spatial/` agrupa lo que cambia junto: si cambia la convención angular, cambian `Bearing` y `BandLayout`, y nada más.

---

## Task 1: Repositorio, versiones y solución

**Files:**
- Create: `.gitignore`
- Create: `global.json`
- Create: `Sonar.sln`

- [ ] **Step 1: Inicializar git**

```bash
cd /home/cgrass/Projects/SONAR
git init
git branch -m main
```

- [ ] **Step 2: Averiguar la versión de Godot y el SDK de .NET que exige**

No asumas versiones de memoria. Ejecuta:

```bash
godot --version
```

Si Godot no está instalado o quieres fijar otra versión, consulta la documentación oficial de la versión elegida (sección *C# / .NET*) para saber qué SDK de .NET requiere. Anota ambos números; los usarás en los pasos 3 y 5 y en `project-state.yaml` más adelante.

Luego confirma qué SDKs de .NET tienes:

```bash
dotnet --list-sdks
```

Expected: al menos una línea con la versión que Godot exige. Si no aparece, instálala antes de continuar.

- [ ] **Step 3: Crear `global.json` fijando el SDK**

Sustituye `8.0.100` por la versión exacta que apareció en `dotnet --list-sdks` y que corresponde a la que exige tu Godot.

```json
{
  "sdk": {
    "version": "8.0.100",
    "rollForward": "latestFeature"
  }
}
```

- [ ] **Step 4: Crear `.gitignore`**

```gitignore
# .NET
bin/
obj/
*.user
*.suo

# Godot
.godot/
.import/
export.cfg
export_presets.cfg
*.translation

# Herramientas de brainstorming
.superpowers/

# Sistema
.DS_Store
Thumbs.db
```

- [ ] **Step 5: Crear la solución y verificar que el SDK fijado responde**

```bash
dotnet --version
```

Expected: la versión declarada en `global.json`. Si imprime otra, `global.json` está mal escrito o falta ese SDK.

```bash
dotnet new sln --name Sonar --format sln
```

Expected: `The template "Solution File" was created successfully.`

> **`--format sln` es obligatorio.** Desde el SDK 10, `dotnet new sln` genera por defecto el
> formato XML nuevo `.slnx`, e imprime exactamente el mismo mensaje de éxito — así que el fallo
> es silencioso. El resto de este plan y los tres siguientes referencian `Sonar.sln`, incluido
> el test de la Tarea 10 que busca ese archivo para localizar la raíz del repositorio.
>
> `dotnet sln add` sí funciona con normalidad sobre un `.sln` clásico; el problema es solo el
> formato por defecto al crearlo.

- [ ] **Step 6: Commit**

```bash
git add .gitignore global.json Sonar.sln
git commit -m "chore: init repo, pin .NET SDK, create solution"
```

---

## Task 2: Proyecto Core y proyecto de tests

**Files:**
- Create: `src/Sonar.Core/Sonar.Core.csproj`
- Create: `tests/Sonar.Core.Tests/Sonar.Core.Tests.csproj`

- [ ] **Step 1: Crear ambos proyectos y enlazarlos**

```bash
dotnet new classlib --output src/Sonar.Core --name Sonar.Core
dotnet new xunit --output tests/Sonar.Core.Tests --name Sonar.Core.Tests
dotnet sln add src/Sonar.Core/Sonar.Core.csproj
dotnet sln add tests/Sonar.Core.Tests/Sonar.Core.Tests.csproj
dotnet add tests/Sonar.Core.Tests/Sonar.Core.Tests.csproj reference src/Sonar.Core/Sonar.Core.csproj
```

- [ ] **Step 2: Borrar los archivos de ejemplo que generan las plantillas**

```bash
rm -f src/Sonar.Core/Class1.cs tests/Sonar.Core.Tests/UnitTest1.cs
```

- [ ] **Step 3: Endurecer `src/Sonar.Core/Sonar.Core.csproj`**

Reemplaza el contenido completo del archivo. El TFM es `net9.0`: Godot 4.7 mono compila contra
esa versión, y el SDK 10 la construye sin problema.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>

</Project>
```

> `Sonar.Core` no debe adquirir **ninguna** referencia a paquete ni a proyecto. Si un ticket futuro pide agregar una, es señal de que la lógica pertenece a otra capa.

- [ ] **Step 4: Compilar y correr los tests vacíos**

```bash
dotnet build
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

```bash
dotnet test
```

Expected: `Passed! - Failed: 0, Passed: 0, Skipped: 0` (cero tests todavía; lo que se verifica es que el arnés corre).

- [ ] **Step 5: Commit**

```bash
git add src tests Sonar.sln
git commit -m "chore: add Sonar.Core and its test project"
```

---

## Task 3: Vec2

**Files:**
- Create: `src/Sonar.Core/Spatial/Vec2.cs`
- Test: `tests/Sonar.Core.Tests/Spatial/Vec2Tests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Spatial/Vec2Tests.cs`:

```csharp
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Core.Tests.Spatial;

public class Vec2Tests
{
    [Fact]
    public void Length_OfThreeFourVector_IsFive()
    {
        var v = new Vec2(3, 4);
        Assert.Equal(5.0, v.Length, precision: 10);
    }

    [Fact]
    public void Addition_AddsComponents()
    {
        var result = new Vec2(1, 2) + new Vec2(10, 20);
        Assert.Equal(new Vec2(11, 22), result);
    }

    [Fact]
    public void Subtraction_SubtractsComponents()
    {
        var result = new Vec2(10, 20) - new Vec2(1, 2);
        Assert.Equal(new Vec2(9, 18), result);
    }

    [Fact]
    public void ScalarMultiplication_ScalesBothComponents()
    {
        var result = new Vec2(3, -4) * 2;
        Assert.Equal(new Vec2(6, -8), result);
    }

    [Fact]
    public void Distance_BetweenTwoPoints_IsSymmetric()
    {
        var a = new Vec2(1, 1);
        var b = new Vec2(4, 5);
        Assert.Equal(5.0, Vec2.Distance(a, b), precision: 10);
        Assert.Equal(Vec2.Distance(a, b), Vec2.Distance(b, a), precision: 10);
    }

    [Fact]
    public void Normalized_OfNonZeroVector_HasUnitLength()
    {
        var v = new Vec2(3, 4).Normalized();
        Assert.Equal(1.0, v.Length, precision: 10);
    }

    [Fact]
    public void Normalized_OfZeroVector_ReturnsZero()
    {
        Assert.Equal(Vec2.Zero, Vec2.Zero.Normalized());
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL. Errores de compilación del tipo `The type or namespace name 'Vec2' could not be found`.

- [ ] **Step 3: Implementar `Vec2`**

Crea `src/Sonar.Core/Spatial/Vec2.cs`:

```csharp
namespace Sonar.Core.Spatial;

/// <summary>
/// Vector 2D de doble precisión. Existe para que Sonar.Core no dependa de Godot.
/// Nunca sustituir por Godot.Vector2: Core no referencia Godot (ver TDD §5.1 del spec).
/// </summary>
public readonly record struct Vec2(double X, double Y)
{
    public static readonly Vec2 Zero = new(0, 0);

    public double Length => System.Math.Sqrt(X * X + Y * Y);

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double scalar) => new(a.X * scalar, a.Y * scalar);

    public static double Distance(Vec2 a, Vec2 b) => (a - b).Length;

    /// <summary>Devuelve Zero si el vector es de longitud cero, en lugar de NaN.</summary>
    public Vec2 Normalized()
    {
        var length = Length;
        return length == 0 ? Zero : new Vec2(X / length, Y / length);
    }
}
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 7`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/Spatial/Vec2.cs tests/Sonar.Core.Tests/Spatial/Vec2Tests.cs
git commit -m "feat(core): add Vec2 spatial primitive"
```

---

## Task 4: Bearing

Convención del TDD §1: marcación `0` es norte (arriba), creciendo en sentido horario. En espacio de simulación el norte es `(0, -1)`.

**Files:**
- Create: `src/Sonar.Core/Spatial/Bearing.cs`
- Test: `tests/Sonar.Core.Tests/Spatial/BearingTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Spatial/BearingTests.cs`:

```csharp
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Core.Tests.Spatial;

public class BearingTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(47, 47)]
    [InlineData(359, 359)]
    [InlineData(360, 0)]
    [InlineData(361, 1)]
    [InlineData(720, 0)]
    [InlineData(-1, 359)]
    [InlineData(-360, 0)]
    [InlineData(-361, 359)]
    public void Constructor_NormalizesToZeroThreeFiftyNine(int input, int expected)
    {
        Assert.Equal(expected, new Bearing(input).Degrees);
    }

    [Theory]
    [InlineData(0, 0, -1)]
    [InlineData(90, 1, 0)]
    [InlineData(180, 0, 1)]
    [InlineData(270, -1, 0)]
    public void ToUnitVector_MapsCardinalDirections(int degrees, double x, double y)
    {
        var v = new Bearing(degrees).ToUnitVector();
        Assert.Equal(x, v.X, precision: 10);
        Assert.Equal(y, v.Y, precision: 10);
    }

    [Fact]
    public void ToUnitVector_AlwaysHasUnitLength()
    {
        for (var d = 0; d < 360; d++)
        {
            Assert.Equal(1.0, new Bearing(d).ToUnitVector().Length, precision: 10);
        }
    }

    [Theory]
    [InlineData(0, -1, 0)]
    [InlineData(1, 0, 90)]
    [InlineData(0, 1, 180)]
    [InlineData(-1, 0, 270)]
    public void FromVector_MapsCardinalDirections(double x, double y, int expected)
    {
        Assert.Equal(expected, Bearing.FromVector(new Vec2(x, y)).Degrees);
    }

    [Fact]
    public void FromVector_IsInverseOfToUnitVector()
    {
        for (var d = 0; d < 360; d++)
        {
            var roundTripped = Bearing.FromVector(new Bearing(d).ToUnitVector());
            Assert.Equal(d, roundTripped.Degrees);
        }
    }

    [Fact]
    public void FromVector_OfZeroVector_ReturnsZero()
    {
        Assert.Equal(0, Bearing.FromVector(Vec2.Zero).Degrees);
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'Bearing' could not be found`.

- [ ] **Step 3: Implementar `Bearing`**

Crea `src/Sonar.Core/Spatial/Bearing.cs`:

```csharp
namespace Sonar.Core.Spatial;

/// <summary>
/// Marcación en grados enteros, siempre normalizada a [0, 360).
/// Cero es norte (arriba) y crece en sentido horario. En espacio de simulación
/// el norte es (0, -1) — ver TDD §1.
/// </summary>
public readonly record struct Bearing
{
    public int Degrees { get; }

    public Bearing(int degrees) => Degrees = ((degrees % 360) + 360) % 360;

    public Vec2 ToUnitVector()
    {
        var radians = Degrees * System.Math.PI / 180.0;
        return new Vec2(System.Math.Sin(radians), -System.Math.Cos(radians));
    }

    /// <summary>Marcación del vector dado. Un vector cero devuelve marcación 0.</summary>
    public static Bearing FromVector(Vec2 v)
    {
        if (v.X == 0 && v.Y == 0) return new Bearing(0);
        var degrees = System.Math.Atan2(v.X, -v.Y) * 180.0 / System.Math.PI;
        return new Bearing((int)System.Math.Round(degrees));
    }

    public override string ToString() => Degrees.ToString("D3");
}
```

> `ToString` rellena con ceros a tres dígitos. El TDD §6.2 establece que la entrada acepta `C47` y `C047`, pero **la salida siempre se formatea con tres dígitos**.

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 27`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/Spatial/Bearing.cs tests/Sonar.Core.Tests/Spatial/BearingTests.cs
git commit -m "feat(core): add Bearing with 360-degree normalization"
```

---

## Task 5: Band

**Files:**
- Create: `src/Sonar.Core/Spatial/Band.cs`
- Test: `tests/Sonar.Core.Tests/Spatial/BandTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Spatial/BandTests.cs`:

```csharp
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Core.Tests.Spatial;

public class BandTests
{
    [Theory]
    [InlineData(0, 'A')]
    [InlineData(2, 'C')]
    [InlineData(4, 'E')]
    [InlineData(7, 'H')]
    public void Letter_IsDerivedFromIndex(int index, char expected)
    {
        Assert.Equal(expected, new Band(index).Letter);
    }

    [Theory]
    [InlineData('A', 5, 0)]
    [InlineData('C', 5, 2)]
    [InlineData('E', 5, 4)]
    [InlineData('a', 5, 0)]
    [InlineData('c', 5, 2)]
    public void TryParseLetter_AcceptsLettersWithinBandCount(char letter, int bandCount, int expectedIndex)
    {
        Assert.True(Band.TryParseLetter(letter, bandCount, out var band));
        Assert.Equal(expectedIndex, band.Index);
    }

    [Theory]
    [InlineData('F', 5)]
    [InlineData('Z', 5)]
    [InlineData('D', 3)]
    [InlineData('1', 5)]
    [InlineData('-', 5)]
    public void TryParseLetter_RejectsLettersOutsideBandCount(char letter, int bandCount)
    {
        Assert.False(Band.TryParseLetter(letter, bandCount, out _));
    }
}
```

> El caso `('D', 3)` es el que importa: con tres bandas configuradas, `D` no existe. Ningún código puede asumir cinco bandas (TDD §1).

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'Band' could not be found`.

- [ ] **Step 3: Implementar `Band`**

Crea `src/Sonar.Core/Spatial/Band.cs`:

```csharp
namespace Sonar.Core.Spatial;

/// <summary>
/// Índice de banda de distancia, base 0. La letra se deriva del índice.
/// El rango de letras depende del número de bandas configurado: nunca asumir cinco.
/// </summary>
public readonly record struct Band(int Index)
{
    public char Letter => (char)('A' + Index);

    public static bool TryParseLetter(char letter, int bandCount, out Band band)
    {
        var upper = char.ToUpperInvariant(letter);
        var index = upper - 'A';
        if (index < 0 || index >= bandCount)
        {
            band = default;
            return false;
        }
        band = new Band(index);
        return true;
    }

    public override string ToString() => Letter.ToString();
}
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 41`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/Spatial/Band.cs tests/Sonar.Core.Tests/Spatial/BandTests.cs
git commit -m "feat(core): add Band with count-aware letter parsing"
```

---

## Task 6: BandLayout

Geometría de las bandas según TDD §1: la banda `i` cubre `[inner, outer)`, donde `inner` es el radio exterior de la banda anterior o el de la zona muerta para la primera.

**Files:**
- Create: `src/Sonar.Core/Spatial/BandLayout.cs`
- Test: `tests/Sonar.Core.Tests/Spatial/BandLayoutTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Spatial/BandLayoutTests.cs`:

```csharp
using System;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Core.Tests.Spatial;

public class BandLayoutTests
{
    private static BandLayout FiveBands() =>
        new(deadZoneRadius: 0.10, outerRadii: new[] { 0.28, 0.46, 0.64, 0.82, 1.00 });

    [Fact]
    public void Count_IsNumberOfOuterRadii()
    {
        Assert.Equal(5, FiveBands().Count);
    }

    [Fact]
    public void FirstBand_StartsAtDeadZone()
    {
        Assert.Equal(0.10, FiveBands().InnerRadius(new Band(0)), precision: 10);
    }

    [Fact]
    public void BandInner_IsPreviousBandOuter()
    {
        var layout = FiveBands();
        Assert.Equal(layout.OuterRadius(new Band(1)), layout.InnerRadius(new Band(2)), precision: 10);
    }

    [Fact]
    public void CenterRadius_IsMidpointOfBand()
    {
        // Banda C: [0.46, 0.64) -> centro 0.55
        Assert.Equal(0.55, FiveBands().CenterRadius(new Band(2)), precision: 10);
    }

    [Theory]
    [InlineData(0.20, 0)]
    [InlineData(0.35, 1)]
    [InlineData(0.55, 2)]
    [InlineData(0.70, 3)]
    [InlineData(0.95, 4)]
    public void BandAt_ReturnsBandContainingRadius(double radius, int expectedIndex)
    {
        Assert.True(FiveBands().TryBandAt(radius, out var band));
        Assert.Equal(expectedIndex, band.Index);
    }

    [Fact]
    public void BandAt_OnBoundary_BelongsToOuterBand()
    {
        // 0.28 es el radio exterior de A y el interior de B. Pertenece a B.
        Assert.True(FiveBands().TryBandAt(0.28, out var band));
        Assert.Equal(1, band.Index);
    }

    [Theory]
    [InlineData(0.05)]   // dentro de la zona muerta
    [InlineData(1.50)]   // fuera del scope
    public void BandAt_OutsideRange_ReturnsFalse(double radius)
    {
        Assert.False(FiveBands().TryBandAt(radius, out _));
    }

    [Fact]
    public void Constructor_RejectsNonIncreasingRadii()
    {
        Assert.Throws<ArgumentException>(() =>
            new BandLayout(0.10, new[] { 0.28, 0.20, 0.64 }));
    }

    [Fact]
    public void Constructor_RejectsFirstRadiusInsideDeadZone()
    {
        Assert.Throws<ArgumentException>(() =>
            new BandLayout(0.30, new[] { 0.28, 0.46 }));
    }

    [Fact]
    public void Constructor_RejectsEmptyRadii()
    {
        Assert.Throws<ArgumentException>(() => new BandLayout(0.10, Array.Empty<double>()));
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'BandLayout' could not be found`.

- [ ] **Step 3: Implementar `BandLayout`**

Crea `src/Sonar.Core/Spatial/BandLayout.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sonar.Core.Spatial;

/// <summary>
/// Geometría de las bandas de distancia. La banda i cubre [inner, outer), donde inner es
/// el radio exterior de la banda anterior, o el de la zona muerta para la primera.
/// Ver TDD §1.
/// </summary>
public sealed class BandLayout
{
    private readonly double[] _outerRadii;

    public double DeadZoneRadius { get; }
    public int Count => _outerRadii.Length;

    public BandLayout(double deadZoneRadius, IReadOnlyList<double> outerRadii)
    {
        if (outerRadii.Count == 0)
            throw new ArgumentException("Se requiere al menos una banda.", nameof(outerRadii));
        if (deadZoneRadius < 0)
            throw new ArgumentException("El radio de zona muerta no puede ser negativo.", nameof(deadZoneRadius));
        if (outerRadii[0] <= deadZoneRadius)
            throw new ArgumentException(
                $"El primer radio exterior ({outerRadii[0]}) debe ser mayor al radio de zona muerta ({deadZoneRadius}).",
                nameof(outerRadii));

        for (var i = 1; i < outerRadii.Count; i++)
        {
            if (outerRadii[i] <= outerRadii[i - 1])
                throw new ArgumentException(
                    $"Los radios exteriores deben ser estrictamente crecientes; índice {i} ({outerRadii[i]}) no supera al anterior ({outerRadii[i - 1]}).",
                    nameof(outerRadii));
        }

        DeadZoneRadius = deadZoneRadius;
        _outerRadii = outerRadii.ToArray();
    }

    public double InnerRadius(Band band) =>
        band.Index == 0 ? DeadZoneRadius : _outerRadii[band.Index - 1];

    public double OuterRadius(Band band) => _outerRadii[band.Index];

    public double CenterRadius(Band band) => (InnerRadius(band) + OuterRadius(band)) / 2.0;

    /// <summary>
    /// Banda que contiene el radio dado. Un radio que cae justo en una frontera pertenece
    /// a la banda exterior. Devuelve false dentro de la zona muerta o fuera del scope.
    /// </summary>
    public bool TryBandAt(double radius, out Band band)
    {
        if (radius < DeadZoneRadius || radius > _outerRadii[^1])
        {
            band = default;
            return false;
        }

        for (var i = 0; i < _outerRadii.Length; i++)
        {
            if (radius < _outerRadii[i])
            {
                band = new Band(i);
                return true;
            }
        }

        band = new Band(_outerRadii.Length - 1);
        return true;
    }
}
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 56`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/Spatial/BandLayout.cs tests/Sonar.Core.Tests/Spatial/BandLayoutTests.cs
git commit -m "feat(core): add BandLayout geometry with validation"
```

---

## Task 7: TargetCoordinate

**Files:**
- Create: `src/Sonar.Core/Spatial/TargetCoordinate.cs`
- Test: `tests/Sonar.Core.Tests/Spatial/TargetCoordinateTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Spatial/TargetCoordinateTests.cs`:

```csharp
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Core.Tests.Spatial;

public class TargetCoordinateTests
{
    private static BandLayout FiveBands() =>
        new(deadZoneRadius: 0.10, outerRadii: new[] { 0.28, 0.46, 0.64, 0.82, 1.00 });

    [Fact]
    public void ToString_FormatsAsLetterThenThreeDigits()
    {
        var target = new TargetCoordinate(new Bearing(47), new Band(2));
        Assert.Equal("C047", target.ToString());
    }

    [Fact]
    public void ToString_PadsSingleDigitBearing()
    {
        var target = new TargetCoordinate(new Bearing(7), new Band(0));
        Assert.Equal("A007", target.ToString());
    }

    [Fact]
    public void ToPosition_IsAtBandCenterRadius()
    {
        var layout = FiveBands();
        var target = new TargetCoordinate(new Bearing(0), new Band(2));
        var position = target.ToPosition(layout);

        // Marcación 0 es norte: (0, -centro). Centro de C = 0.55.
        Assert.Equal(0.0, position.X, precision: 10);
        Assert.Equal(-0.55, position.Y, precision: 10);
    }

    [Fact]
    public void ToPosition_LengthEqualsBandCenterRadius()
    {
        var layout = FiveBands();
        var target = new TargetCoordinate(new Bearing(137), new Band(3));
        Assert.Equal(layout.CenterRadius(new Band(3)), target.ToPosition(layout).Length, precision: 10);
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'TargetCoordinate' could not be found`.

- [ ] **Step 3: Implementar `TargetCoordinate`**

Crea `src/Sonar.Core/Spatial/TargetCoordinate.cs`:

```csharp
namespace Sonar.Core.Spatial;

/// <summary>
/// Objetivo escrito por el jugador: una marcación y una banda. Por ejemplo C047.
/// La banda nombra una distancia nominal — el centro de la banda — no un rango.
/// </summary>
public readonly record struct TargetCoordinate(Bearing Bearing, Band Band)
{
    /// <summary>Punto en espacio de simulación al que apunta esta coordenada.</summary>
    public Vec2 ToPosition(BandLayout layout) =>
        Bearing.ToUnitVector() * layout.CenterRadius(Band);

    public override string ToString() => $"{Band.Letter}{Bearing.Degrees:D3}";
}
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 60`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/Spatial/TargetCoordinate.cs tests/Sonar.Core.Tests/Spatial/TargetCoordinateTests.cs
git commit -m "feat(core): add TargetCoordinate"
```

---

## Task 8: IRandomSource y SeededRandomSource

**Files:**
- Create: `src/Sonar.Core/IRandomSource.cs`
- Create: `src/Sonar.Core/SeededRandomSource.cs`
- Test: `tests/Sonar.Core.Tests/SeededRandomSourceTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/SeededRandomSourceTests.cs`:

```csharp
using System.Collections.Generic;
using Sonar.Core;
using Xunit;

namespace Sonar.Core.Tests;

public class SeededRandomSourceTests
{
    private static List<double> Draw(int seed, int count)
    {
        var source = new SeededRandomSource(seed);
        var values = new List<double>();
        for (var i = 0; i < count; i++) values.Add(source.NextDouble());
        return values;
    }

    [Fact]
    public void SameSeed_ProducesIdenticalSequence()
    {
        Assert.Equal(Draw(12345, 200), Draw(12345, 200));
    }

    [Fact]
    public void DifferentSeed_ProducesDifferentSequence()
    {
        Assert.NotEqual(Draw(11111, 200), Draw(22222, 200));
    }

    [Fact]
    public void NextDouble_StaysInZeroToOne()
    {
        var source = new SeededRandomSource(7);
        for (var i = 0; i < 1000; i++)
        {
            var v = source.NextDouble();
            Assert.InRange(v, 0.0, 1.0);
        }
    }

    [Fact]
    public void NextInt_StaysWithinRequestedRange()
    {
        var source = new SeededRandomSource(7);
        for (var i = 0; i < 1000; i++)
        {
            var v = source.NextInt(5, 10);
            Assert.InRange(v, 5, 9);
        }
    }

    [Fact]
    public void NextInt_WithSingleValueRange_AlwaysReturnsThatValue()
    {
        var source = new SeededRandomSource(7);
        for (var i = 0; i < 50; i++) Assert.Equal(3, source.NextInt(3, 4));
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'SeededRandomSource' could not be found`.

- [ ] **Step 3: Implementar la interfaz y su implementación sembrada**

Crea `src/Sonar.Core/IRandomSource.cs`:

```csharp
namespace Sonar.Core;

/// <summary>
/// Toda fuente de azar de la simulación pasa por aquí, inyectada y sembrada.
/// Core nunca construye generadores por su cuenta ni usa Random.Shared: eso rompería
/// el determinismo del que depende todo el diseño (ver TDD §8).
/// </summary>
public interface IRandomSource
{
    int NextInt(int minInclusive, int maxExclusive);
    double NextDouble();
}
```

Crea `src/Sonar.Core/SeededRandomSource.cs`:

```csharp
namespace Sonar.Core;

/// <summary>
/// Generador xorshift64* sembrado. Se implementa a mano en lugar de usar System.Random
/// porque el algoritmo de System.Random no está garantizado entre versiones de .NET,
/// y una corrida guardada debe reproducirse igual tras actualizar el runtime.
/// </summary>
public sealed class SeededRandomSource : IRandomSource
{
    private ulong _state;

    public SeededRandomSource(int seed)
    {
        // El estado de xorshift nunca puede ser cero.
        _state = (ulong)seed ^ 0x9E3779B97F4A7C15UL;
        if (_state == 0) _state = 0x9E3779B97F4A7C15UL;
    }

    private ulong NextRaw()
    {
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;
        return _state * 0x2545F4914F6CDD1DUL;
    }

    /// <summary>Devuelve un valor en [0, 1).</summary>
    public double NextDouble() => (NextRaw() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>Devuelve un entero en [minInclusive, maxExclusive).</summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        var range = (ulong)(maxExclusive - minInclusive);
        return minInclusive + (int)(NextRaw() % range);
    }
}
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 65`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/IRandomSource.cs src/Sonar.Core/SeededRandomSource.cs tests/Sonar.Core.Tests/SeededRandomSourceTests.cs
git commit -m "feat(core): add injectable seeded random source"
```

---

## Task 9: SimulationConfig con validación de invariantes

Todas las invariantes vienen del TDD §9. Los **valores** son de trabajo y los define M1; lo que este plan fija son las **relaciones** que deben cumplirse.

**Files:**
- Create: `src/Sonar.Core/SimulationConfig.cs`
- Test: `tests/Sonar.Core.Tests/SimulationConfigTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/SimulationConfigTests.cs`:

```csharp
using System;
using Sonar.Core;
using Xunit;

namespace Sonar.Core.Tests;

public class SimulationConfigTests
{
    /// Configuración de trabajo válida. Los valores definitivos salen de M1.
    private static SimulationConfig Valid() => new()
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
    public void Validate_AcceptsWorkingConfiguration()
    {
        Valid().Validate();   // no debe lanzar
    }

    [Fact]
    public void Validate_RejectsHitRadiusSmallerThanPerTickTravel()
    {
        // 0.70 * (1/60) ≈ 0.01167 — un HitRadius menor permite atravesar contactos.
        var config = Valid() with { HitRadius = 0.005 };
        var ex = Assert.Throws<ArgumentException>(config.Validate);
        Assert.Contains("HitRadius", ex.Message);
    }

    [Fact]
    public void Validate_RejectsSubHitRadiusSmallerThanEnemyPerTickTravel()
    {
        var config = Valid() with { SubHitRadius = 0.003 };
        var ex = Assert.Throws<ArgumentException>(config.Validate);
        Assert.Contains("SubHitRadius", ex.Message);
    }

    [Fact]
    public void Validate_RejectsDefenseRadiusSmallerThanEnemyPerTickTravel()
    {
        var config = Valid() with { DefenseRadius = 0.004 };
        var ex = Assert.Throws<ArgumentException>(config.Validate);
        Assert.Contains("DefenseRadius", ex.Message);
    }

    [Fact]
    public void Validate_RejectsBlastRadiusSmallerThanHitRadius()
    {
        var config = Valid() with { BlastRadius = 0.020 };
        Assert.Throws<ArgumentException>(config.Validate);
    }

    [Fact]
    public void Validate_RejectsNearMissRadiusNotGreaterThanBlastRadius()
    {
        var config = Valid() with { NearMissRadius = 0.100 };
        Assert.Throws<ArgumentException>(config.Validate);
    }

    [Fact]
    public void Validate_RejectsSubHitRadiusOutsideDeadZone()
    {
        // El submarino debe ser alcanzable dentro de la zona donde sus propios
        // torpedos no pueden estar armados.
        var config = Valid() with { SubHitRadius = 0.20 };
        Assert.Throws<ArgumentException>(config.Validate);
    }

    [Fact]
    public void Validate_RejectsNonIncreasingBandRadii()
    {
        var config = Valid() with { BandOuterRadii = new[] { 0.28, 0.20, 0.64 } };
        Assert.Throws<ArgumentException>(config.Validate);
    }

    [Fact]
    public void Validate_RejectsNonPositiveFireInterval()
    {
        var config = Valid() with { EnemyFireIntervalTicks = 0 };
        Assert.Throws<ArgumentException>(config.Validate);
    }

    [Fact]
    public void BandLayout_IsBuiltFromConfiguredRadii()
    {
        var layout = Valid().BuildBandLayout();
        Assert.Equal(5, layout.Count);
        Assert.Equal(0.10, layout.DeadZoneRadius, precision: 10);
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'SimulationConfig' could not be found`.

- [ ] **Step 3: Implementar `SimulationConfig`**

Crea `src/Sonar.Core/SimulationConfig.cs`:

```csharp
using System;
using System.Collections.Generic;
using Sonar.Core.Spatial;

namespace Sonar.Core;

/// <summary>
/// Todos los valores ajustables de la simulación (§61 del GDD). Los valores concretos son
/// salida de M1; lo que este tipo garantiza son las relaciones entre ellos.
/// </summary>
public sealed record SimulationConfig
{
    public double TickDurationSeconds { get; init; } = 1.0 / 60.0;
    public int MaxCatchUpTicks { get; init; } = 5;

    public double DeadZoneRadius { get; init; }
    public IReadOnlyList<double> BandOuterRadii { get; init; } = Array.Empty<double>();

    public double SweepPeriodSeconds { get; init; }

    public double TorpedoSpeed { get; init; }
    public double HitRadius { get; init; }
    public double BlastRadius { get; init; }
    public double NearMissRadius { get; init; }

    public double EnemyTorpedoSpeed { get; init; }
    public int EnemyFireIntervalTicks { get; init; }
    public int EnemyFireStaggerTicks { get; init; }
    public double SubHitRadius { get; init; }

    public double DefenseRadius { get; init; }
    public int DefenseCapacity { get; init; }

    public int MagazineSize { get; init; }
    public int AmmoReserve { get; init; }
    public int ReloadTicks { get; init; }

    public BandLayout BuildBandLayout() => new(DeadZoneRadius, BandOuterRadii);

    /// <summary>
    /// Lanza si alguna invariante no se cumple. Es un error de configuración: debe fallar
    /// al arrancar, no producir comportamiento raro en tiempo de ejecución.
    /// </summary>
    public void Validate()
    {
        Positive(TickDurationSeconds, nameof(TickDurationSeconds));
        Positive(SweepPeriodSeconds, nameof(SweepPeriodSeconds));
        Positive(TorpedoSpeed, nameof(TorpedoSpeed));
        Positive(EnemyTorpedoSpeed, nameof(EnemyTorpedoSpeed));
        Positive(HitRadius, nameof(HitRadius));
        Positive(BlastRadius, nameof(BlastRadius));
        Positive(NearMissRadius, nameof(NearMissRadius));
        Positive(SubHitRadius, nameof(SubHitRadius));
        Positive(DefenseRadius, nameof(DefenseRadius));
        Positive(DeadZoneRadius, nameof(DeadZoneRadius));

        if (MaxCatchUpTicks <= 0) throw Bad(nameof(MaxCatchUpTicks), "debe ser mayor a cero.");
        if (DefenseCapacity < 0) throw Bad(nameof(DefenseCapacity), "no puede ser negativo.");
        if (MagazineSize <= 0) throw Bad(nameof(MagazineSize), "debe ser mayor a cero.");
        if (AmmoReserve < 0) throw Bad(nameof(AmmoReserve), "no puede ser negativo.");
        if (ReloadTicks <= 0) throw Bad(nameof(ReloadTicks), "debe ser mayor a cero.");
        if (EnemyFireIntervalTicks <= 0) throw Bad(nameof(EnemyFireIntervalTicks), "debe ser mayor a cero.");
        if (EnemyFireStaggerTicks < 0) throw Bad(nameof(EnemyFireStaggerTicks), "no puede ser negativo.");

        // Invariantes anti-tunneling: nada puede avanzar más por tick que su propio radio
        // de detección, o atravesaría su objetivo sin registrarlo (TDD §3.4).
        var ownTravel = TorpedoSpeed * TickDurationSeconds;
        var enemyTravel = EnemyTorpedoSpeed * TickDurationSeconds;

        if (HitRadius <= ownTravel)
            throw Bad(nameof(HitRadius),
                $"({HitRadius}) debe superar el avance por tick del torpedo propio ({ownTravel}).");
        if (HitRadius <= enemyTravel)
            throw Bad(nameof(HitRadius),
                $"({HitRadius}) debe superar el avance por tick del torpedo enemigo ({enemyTravel}).");
        if (SubHitRadius <= enemyTravel)
            throw Bad(nameof(SubHitRadius),
                $"({SubHitRadius}) debe superar el avance por tick del torpedo enemigo ({enemyTravel}).");
        if (DefenseRadius <= enemyTravel)
            throw Bad(nameof(DefenseRadius),
                $"({DefenseRadius}) debe superar el avance por tick del torpedo enemigo ({enemyTravel}).");

        if (BlastRadius < HitRadius)
            throw Bad(nameof(BlastRadius), $"({BlastRadius}) no puede ser menor a HitRadius ({HitRadius}).");
        if (NearMissRadius <= BlastRadius)
            throw Bad(nameof(NearMissRadius), $"({NearMissRadius}) debe superar a BlastRadius ({BlastRadius}).");

        // El submarino debe ser alcanzable dentro de la zona donde sus propios torpedos
        // no pueden estar armados. Es lo que hace peligroso que algo se acerque.
        if (SubHitRadius >= DeadZoneRadius)
            throw Bad(nameof(SubHitRadius),
                $"({SubHitRadius}) debe ser menor al radio de zona muerta ({DeadZoneRadius}).");

        // Delega la validación de la geometría de bandas, que ya la implementa.
        BuildBandLayout();

        if (BandOuterRadii[^1] > 1.0)
            throw Bad(nameof(BandOuterRadii), "el radio exterior de la última banda no puede superar 1.0.");
    }

    private static void Positive(double value, string name)
    {
        if (value <= 0) throw Bad(name, $"debe ser mayor a cero, pero es {value}.");
    }

    private static ArgumentException Bad(string name, string message) =>
        new($"{name} {message}", name);
}
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 75`

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Core/SimulationConfig.cs tests/Sonar.Core.Tests/SimulationConfigTests.cs
git commit -m "feat(core): add SimulationConfig with invariant validation"
```

---

## Task 10: Configuración de trabajo en disco y verificación final

**Files:**
- Create: `config/simulation.working.json`
- Create: `src/Sonar.Core/SimulationConfigLoader.cs`
- Test: `tests/Sonar.Core.Tests/SimulationConfigLoaderTests.cs`

- [ ] **Step 1: Crear el archivo de configuración de trabajo**

Crea `config/simulation.working.json`. **Todos estos valores son provisionales y son la salida esperada de M1** — están puestos para poder arrancar, no porque estén balanceados.

```json
{
  "tickDurationSeconds": 0.016666666666666666,
  "maxCatchUpTicks": 5,
  "deadZoneRadius": 0.10,
  "bandOuterRadii": [0.28, 0.46, 0.64, 0.82, 1.00],
  "sweepPeriodSeconds": 3.5,
  "torpedoSpeed": 0.70,
  "hitRadius": 0.040,
  "blastRadius": 0.100,
  "nearMissRadius": 0.160,
  "enemyTorpedoSpeed": 0.45,
  "enemyFireIntervalTicks": 420,
  "enemyFireStaggerTicks": 70,
  "subHitRadius": 0.045,
  "defenseRadius": 0.070,
  "defenseCapacity": 3,
  "magazineSize": 4,
  "ammoReserve": 12,
  "reloadTicks": 300
}
```

- [ ] **Step 2: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/SimulationConfigLoaderTests.cs`:

```csharp
using System.IO;
using Sonar.Core;
using Xunit;

namespace Sonar.Core.Tests;

public class SimulationConfigLoaderTests
{
    [Fact]
    public void Load_ParsesJsonAndValidates()
    {
        var json = """
        {
          "tickDurationSeconds": 0.016666666666666666,
          "maxCatchUpTicks": 5,
          "deadZoneRadius": 0.10,
          "bandOuterRadii": [0.28, 0.46, 0.64, 0.82, 1.00],
          "sweepPeriodSeconds": 3.5,
          "torpedoSpeed": 0.70,
          "hitRadius": 0.040,
          "blastRadius": 0.100,
          "nearMissRadius": 0.160,
          "enemyTorpedoSpeed": 0.45,
          "enemyFireIntervalTicks": 420,
          "enemyFireStaggerTicks": 70,
          "subHitRadius": 0.045,
          "defenseRadius": 0.070,
          "defenseCapacity": 3,
          "magazineSize": 4,
          "ammoReserve": 12,
          "reloadTicks": 300
        }
        """;

        var config = SimulationConfigLoader.LoadFromJson(json);

        Assert.Equal(5, config.BandOuterRadii.Count);
        Assert.Equal(0.70, config.TorpedoSpeed, precision: 10);
        Assert.Equal(420, config.EnemyFireIntervalTicks);
    }

    [Fact]
    public void Load_ThrowsOnInvariantViolation()
    {
        var json = """
        {
          "tickDurationSeconds": 0.016666666666666666,
          "maxCatchUpTicks": 5,
          "deadZoneRadius": 0.10,
          "bandOuterRadii": [0.28, 0.46, 0.64, 0.82, 1.00],
          "sweepPeriodSeconds": 3.5,
          "torpedoSpeed": 0.70,
          "hitRadius": 0.001,
          "blastRadius": 0.100,
          "nearMissRadius": 0.160,
          "enemyTorpedoSpeed": 0.45,
          "enemyFireIntervalTicks": 420,
          "enemyFireStaggerTicks": 70,
          "subHitRadius": 0.045,
          "defenseRadius": 0.070,
          "defenseCapacity": 3,
          "magazineSize": 4,
          "ammoReserve": 12,
          "reloadTicks": 300
        }
        """;

        Assert.Throws<System.ArgumentException>(() => SimulationConfigLoader.LoadFromJson(json));
    }

    [Fact]
    public void WorkingConfigOnDisk_IsValid()
    {
        var path = Path.Combine(RepoRoot(), "config", "simulation.working.json");
        Assert.True(File.Exists(path), $"No se encontró {path}");
        SimulationConfigLoader.LoadFromJson(File.ReadAllText(path));   // no debe lanzar
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Sonar.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
```

> El tercer test es el que más valor tiene a largo plazo: cualquier edición del archivo de balance que rompa una invariante hace fallar la suite en el commit que la introduce.

- [ ] **Step 3: Correr el test para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `The type or namespace name 'SimulationConfigLoader' could not be found`.

- [ ] **Step 4: Implementar el cargador**

Crea `src/Sonar.Core/SimulationConfigLoader.cs`:

```csharp
using System;
using System.Text.Json;

namespace Sonar.Core;

public static class SimulationConfigLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Deserializa y valida. Lanza si el JSON es inválido o rompe una invariante.</summary>
    public static SimulationConfig LoadFromJson(string json)
    {
        var config = JsonSerializer.Deserialize<SimulationConfig>(json, Options)
                     ?? throw new ArgumentException("La configuración deserializó a null.", nameof(json));
        config.Validate();
        return config;
    }
}
```

- [ ] **Step 5: Correr los tests para verificar que pasan**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 78`

- [ ] **Step 6: Verificar que Core no adquirió dependencias**

```bash
grep -rn "PackageReference\|ProjectReference" src/Sonar.Core/Sonar.Core.csproj
```

Expected: sin salida. Si aparece algo, se filtró una dependencia y hay que quitarla.

```bash
grep -rn "using Godot" src/ || echo "sin referencias a Godot"
```

Expected: `sin referencias a Godot`

- [ ] **Step 7: Commit**

```bash
git add config src/Sonar.Core/SimulationConfigLoader.cs tests/Sonar.Core.Tests/SimulationConfigLoaderTests.cs
git commit -m "feat(core): load and validate simulation config from JSON"
```

---

## Definition of Done — Plan 1

- [ ] `dotnet build` termina sin advertencias ni errores
- [ ] `dotnet test` reporta 78 tests pasando, 0 fallando
- [ ] `src/Sonar.Core/Sonar.Core.csproj` no tiene `PackageReference` ni `ProjectReference`
- [ ] `grep -rn "using Godot" src/` no devuelve nada
- [ ] `config/simulation.working.json` carga y valida desde el test
- [ ] Las versiones de Godot y del SDK de .NET quedaron anotadas para `project-state.yaml`
- [ ] Diez commits, uno por tarea

## Qué sigue

**Plan 2 — Simulación de combate:** entidades (`Contact`, `Torpedo`, `Defense`, `PlayerSub`), `WorldState`, el bucle de tick con su orden de actualización fijo, disparo, impacto directo, detonación por radio, casi-impacto, defensas, munición y recarga, y el hash de estado con su test de determinismo.
