# Plan 6 — La guardia y la telemetría (Sonar.Game)

> **Para agentes:** SUB-SKILL REQUERIDA: usa `superpowers:subagent-driven-development` o
> `superpowers:executing-plans` para ejecutar este plan tarea por tarea. Los pasos usan
> checkbox (`- [ ]`) para seguimiento.

**Goal:** Convertir la demo en una guardia legible —reloj de misión, historial que se lee— y
dejarla registrando datos aptos para veinte testers externos.

**Architecture:** Todo lo de presentación vive en `src/Sonar.Game`. La telemetría también,
porque necesita sistema de archivos y reloj de pared, pero **los datos que escribe salen del
flujo de eventos de Core**: la presentación no calcula nada, sólo serializa lo que recibe. La
única excepción es el tiempo de composición, que es tiempo de pared y no existe en la
simulación.

**Tech Stack:** C# / .NET `net9.0`, Godot 4.7 mono, xUnit para lo que se pueda testear sin
Godot.

**Spec:** [2026-08-13](../specs/2026-08-13-m1-gauntlet-and-telemetry-design.md)
**Depende de:** [plan 5](2026-08-13-m1-plan5-gauntlet-rules.md), completo.

---

## Reglas del repositorio

Lee [docs/development/AGENT_CONTRACT.md](../../development/AGENT_CONTRACT.md). Además, para
este plan en particular:

- **`src/Sonar.Game` no está en `Sonar.sln`.** `dotnet test` no lo compila. Verifícalo aparte
  con `dotnet build src/Sonar.Game/Sonar.Game.csproj`.
- El arnés del terminal se corre con
  `godot-mono --path src/Sonar.Game -- --m1-selftest`, y devuelve código distinto de cero si
  falla. Córrelo después de cada tarea que toque `Main.cs`.
- Los strings que ve el jugador van en **inglés**; comentarios y docs en español.

Estado de partida: plan 5 terminado, `dotnet test` verde, y **`src/Sonar.Game` sin compilar**
porque `Main.cs` todavía llama a `OpeningScenario`. La tarea 1 lo arregla.

---

## Estructura de archivos

| Archivo | Responsabilidad |
|---|---|
| `src/Sonar.Game/Main.cs` | Cableado, reloj, HUD. Delega registro y telemetría |
| `src/Sonar.Game/EventLog.cs` | **nuevo** — cola del historial y su ritmo |
| `src/Sonar.Game/Telemetry/JsonLine.cs` | **nuevo** — construye una línea JSON sin dependencias |
| `src/Sonar.Game/Telemetry/TelemetryWriter.cs` | **nuevo** — archivo JSONL incremental |
| `src/Sonar.Game/Telemetry/SessionRecorder.cs` | **nuevo** — traduce eventos a líneas |
| `src/Sonar.Game/SessionMetrics.cs` | Se queda: alimenta el CSV resumen |
| `src/Sonar.Core/Events/GameEvent.cs` | Ids de torpedo en los eventos de desenlace |

`Main.cs` viene creciendo. Sacar el historial y la telemetría a sus propios archivos lo
devuelve a ser lo que dice ser: el cable entre el Controller y los nodos.

---

## Task 1: Volver a compilar, con reloj de misión

**Files:**
- Modify: `src/Sonar.Game/Main.cs`

- [ ] **Step 1: Cambiar el escenario por el director**

En `src/Sonar.Game/Main.cs`, dentro de `_Ready`, reemplaza el bloque que construye el
controller:

```csharp
        _controller = new GameController(
            config, Seed, OpeningScenario.Course, OpeningScenario.SubSpeed);
        _controller.SetContacts(OpeningScenario.Contacts(config));
```

por:

```csharp
        // El director de oleadas vive dentro del GameController: acá sólo se le da rumbo y
        // avance, y él coloca los contactos cuando toca.
        _controller = new GameController(config, Seed, Course, SubSpeed);
```

Y agrega las dos constantes junto a `Seed`:

```csharp
    private const double SubSpeed = 0.012;
    private static readonly Bearing Course = new(0);
```

- [ ] **Step 2: Reloj de misión en el HUD**

Reemplaza la asignación de `_hud.Text` en `_Process`:

```csharp
        _hud.Text =
            $"{MissionClock(snap.Tick)}    wave {_controller.Wave}    " +
            $"ammo {snap.AmmoLoaded}/{snap.AmmoReserve}    " +
            $"defenses {snap.DefensesAvailable}    hits {snap.HitsTaken}/2" +
            (snap.ReloadInProgress ? "    RELOADING" : string.Empty) +
            (snap.SweepRunning ? string.Empty : "    SWEEP STOPPED") +
            (snap.Destroyed ? "    DESTROYED — TYPE RETRY" : string.Empty);
```

Y agrega el método:

```csharp
    /// <summary>
    /// El mismo número que el tick, contado como tiempo en el puesto. Cambiar la unidad
    /// cambia lo que el número significa para quien lo lee: `tick 748` es salida de
    /// depuración, `00:12:28` es una guardia.
    /// </summary>
    private string MissionClock(int tick)
    {
        var seconds = tick * _tickDuration;
        return $"{(int)(seconds / 60):D2}:{(int)(seconds % 60):D2}";
    }
```

Guarda la duración del tick en `_Ready`, junto al resto:

```csharp
    private double _tickDuration;
```

```csharp
        _tickDuration = config.TickDurationSeconds;
```

- [ ] **Step 3: Verificar que compila y corre**

```bash
dotnet build src/Sonar.Game/Sonar.Game.csproj
godot-mono --path src/Sonar.Game -- --m1-selftest
```

Expected: `Build succeeded. 0 Warning(s)` y `[SELFTEST] PASA`.

> El selftest dispara a los ~2 s y el primer contacto entra a los 10 s, así que
> `ShotAssessed` no se emitirá y `shots=1` seguirá viniendo de `TorpedoFired`. Sigue pasando.

- [ ] **Step 4: Commit**

```bash
git add src/Sonar.Game/Main.cs
git commit -m "feat(game): drive the gauntlet and show a mission clock"
```

---

## Task 2: Historial con ritmo

**Files:**
- Create: `src/Sonar.Game/EventLog.cs`
- Modify: `src/Sonar.Game/Main.cs`

- [ ] **Step 1: Escribir el componente**

Crea `src/Sonar.Game/EventLog.cs`:

```csharp
using System.Collections.Generic;
using Godot;

namespace Sonar.Game;

/// <summary>
/// El historial con ritmo. Las líneas se encolan y salen de a una, en vez de volcarse
/// enteras en el frame en que ocurren.
///
/// Existe porque en el playtest no alcanzaba a leerse: el registro es la superficie donde va
/// a vivir la historia, y volcarlo de golpe lo convierte en salida de depuración.
/// </summary>
public sealed class EventLog
{
    /// <summary>Ritmo normal entre líneas.</summary>
    private const double IntervalSeconds = 0.25;

    /// <summary>
    /// Ritmo de emergencia. Sin él, en la oleada 8 el registro se atrasaría minutos y
    /// dejaría de describir lo que está pasando.
    /// </summary>
    private const double CatchUpSeconds = 0.08;

    private const int BacklogThreshold = 8;

    private readonly RichTextLabel _label;
    private readonly Queue<string> _queue = new();
    private double _sinceLast;

    public EventLog(RichTextLabel label) => _label = label;

    /// <summary>Encola una línea. Se muestra cuando le toque.</summary>
    public void Queue(string color, string content) => _queue.Enqueue(Format(color, content));

    /// <summary>
    /// Salta la cola y escribe de inmediato. Reservado para lo que el §4.6 del spec declara
    /// ley del mundo: la confirmación de impacto no depende de nada. El resto del registro
    /// puede llegar tarde; que te peguen, no.
    /// </summary>
    public void WriteNow(string color, string content) => Emit(Format(color, content));

    public void Advance(double delta)
    {
        if (_queue.Count == 0) return;

        _sinceLast += delta;
        var interval = _queue.Count > BacklogThreshold ? CatchUpSeconds : IntervalSeconds;
        if (_sinceLast < interval) return;

        _sinceLast = 0;
        Emit(_queue.Dequeue());
    }

    /// <summary>
    /// El color va como etiqueta aparte del contenido: el historial se muestra todo en
    /// mayúscula, y pasar la línea entera por ToUpperInvariant convertiría [color=...] en
    /// [COLOR=...], que el parser de BBCode de Godot no reconoce.
    /// </summary>
    private static string Format(string color, string content) =>
        $"[color={color}]{content.ToUpperInvariant()}[/color]";

    private void Emit(string line)
    {
        _label.AppendText(line + "\n");
        _label.ScrollToLine(_label.GetLineCount() - 1);
    }
}
```

- [ ] **Step 2: Enchufarlo en `Main`**

En `src/Sonar.Game/Main.cs`, agrega el campo y créalo en `_Ready` después de obtener `_log`:

```csharp
    private EventLog _eventLog = null!;
```

```csharp
        _eventLog = new EventLog(_log);
```

Las constantes `Dim` y `Typed` ya existen en `Main.cs`: **déjalas donde están**. Borra el
método `Append` completo y reemplaza el cuerpo de `Instrument` por:

```csharp
    private void Instrument(string text) => _eventLog.Queue(Dim, $"  {text}");
```

Y actualiza los tres sitios que llamaban a `Append`:

```csharp
        _eventLog.Queue(Dim, "SONAR M1 — VALIDATION PROTOTYPE");
        _eventLog.Queue(Dim, "TYPE C047 AND PRESS ENTER · RELOAD · SONAR · STATUS · DEF C047");
```

```csharp
        _eventLog.Queue(Typed, $"> {command}");
```

En `_Process`, después de procesar los eventos:

```csharp
        _eventLog.Advance(delta);
```

- [ ] **Step 3: Los impactos propios saltan la cola**

En `Report`, cambia los dos casos que informan daño al submarino:

```csharp
            case SubHit e: _eventLog.WriteNow(Dim, $"  WE ARE HIT · {e.HitsTaken}/2"); break;
            case SubDestroyed e:
                _eventLog.WriteNow(Dim, e.Cause == SubDeathCause.Collision
                    ? "  COLLISION · HULL BREACHED"
                    : "  HULL BREACHED");
                break;
```

- [ ] **Step 4: Verificar**

```bash
dotnet build src/Sonar.Game/Sonar.Game.csproj
godot-mono --path src/Sonar.Game -- --m1-selftest
```

Expected: build limpio y `[SELFTEST] PASA`.

> El selftest comprueba `status=True`, que depende de que `StatusReported` llegue a `Report`.
> Como el registro ahora es asíncrono pero `Report` no, sigue funcionando. Si empieza a
> fallar, el bug está en `Report`, no en la cola.

- [ ] **Step 5: Mirarlo con ojos**

```bash
godot-mono --path src/Sonar.Game
```

Escribe `STATUS` varias veces seguidas. Las respuestas tienen que aparecer **de a una**, no
todas juntas. Cierra la ventana.

- [ ] **Step 6: Commit**

```bash
git add src/Sonar.Game
git commit -m "feat(game): pace the log so it can actually be read"
```

---

## Task 3: El latido del barrido y la oleada

**Files:**
- Modify: `src/Sonar.Game/Main.cs`

- [ ] **Step 1: Registrar las vueltas del haz**

En `Report`, agrega el caso:

```csharp
            case SweepCompleted e:
                Instrument($"SWEEP {e.Revolution} · {e.ReadingCount} CONTACTS");
                break;
```

- [ ] **Step 2: Anunciar la oleada**

Agrega el campo y el chequeo. `WaveDirector` no emite eventos —es guion, no simulación—, así
que la presentación detecta el cambio comparando:

```csharp
    private int _announcedWave;
```

En `_Process`, después de `_eventLog.Advance(delta);`:

```csharp
        if (_controller.Wave != _announcedWave)
        {
            _announcedWave = _controller.Wave;
            Instrument($"— WAVE {_announcedWave} —");
        }
```

- [ ] **Step 3: Verificar a ojo**

```bash
godot-mono --path src/Sonar.Game
```

Espera 15 segundos sin tocar nada. Tienes que ver dos líneas `SWEEP n · 0 CONTACTS` antes de
`— WAVE 1 —`. Cierra.

- [ ] **Step 4: Commit**

```bash
git add src/Sonar.Game/Main.cs
git commit -m "feat(game): log sweep revolutions and wave changes"
```

---

## Task 4: Ids de torpedo en los eventos de desenlace

Sin esto no se puede unir un disparo con lo que le pasó, y la mitad de la telemetría de tiro
queda coja.

**Files:**
- Modify: `src/Sonar.Core/Events/GameEvent.cs`
- Modify: `src/Sonar.Core/Simulation.cs`
- Test: `tests/Sonar.Core.Tests/Combat/ShotOutcomeLinkTests.cs`

- [ ] **Step 1: Escribir el test que falla**

Crea `tests/Sonar.Core.Tests/Combat/ShotOutcomeLinkTests.cs`:

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

public class ShotOutcomeLinkTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Command[] Fire(int bearing, int band) =>
        new Command[] { new FireCommand(new TargetCoordinate(new Bearing(bearing), new Band(band))) };

    [Fact]
    public void DirectHit_NamesTheTorpedoThatCausedIt()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            new Contact
            {
                Id = new ContactId(1), Kind = ContactKind.Enemy,
                Position = new Vec2(0.40, 0.0), Velocity = Vec2.Zero,
                Alive = true, NextFireTick = int.MaxValue,
            },
        });

        var events = new List<GameEvent>(sim.Tick(Fire(90, 2)));
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        var fired = events.OfType<TorpedoFired>().Single();
        var killed = events.OfType<ContactDestroyed>().Single();

        Assert.Equal(fired.TorpedoId, killed.TorpedoId);
    }

    [Fact]
    public void RunOutDetonation_NamesItsTorpedo()
    {
        var sim = TestWorld.Still();

        var events = new List<GameEvent>(sim.Tick(Fire(90, 2)));
        for (var i = 0; i < 120; i++) events.AddRange(sim.Tick(NoCommands));

        var fired = events.OfType<TorpedoFired>().Single();
        var detonated = events.OfType<TorpedoDetonated>().Single();

        Assert.Equal(fired.TorpedoId, detonated.TorpedoId);
    }

    [Fact]
    public void DeathsWithoutATorpedo_ReportMinusOne()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[]
        {
            new Contact
            {
                Id = new ContactId(1), Kind = ContactKind.Enemy,
                Position = new Vec2(0.55, 0.0), Velocity = Vec2.Zero,
                Alive = true, NextFireTick = int.MaxValue,
            },
        });

        var events = new List<GameEvent>(sim.Tick(new Command[]
        {
            new DeployDefenseCommand(new TargetCoordinate(new Bearing(90), new Band(2))),
        }));
        for (var i = 0; i < 10; i++) events.AddRange(sim.Tick(NoCommands));

        var killed = events.OfType<ContactDestroyed>().Single();
        Assert.Equal(DestructionCause.Defense, killed.Cause);
        Assert.Equal(-1, killed.TorpedoId);
    }
}
```

- [ ] **Step 2: Correr para verificar que falla**

Run: `dotnet test`
Expected: FAIL con `'ContactDestroyed' does not contain a definition for 'TorpedoId'`.

- [ ] **Step 3: Agregar los campos**

En `src/Sonar.Core/Events/GameEvent.cs`, cambia las dos declaraciones. El parámetro va al
final y con valor por defecto para no romper los sitios que ya construyen estos eventos:

```csharp
public sealed record TorpedoDetonated(int Tick, Vec2 Position, bool HitSomething,
    int TorpedoId = -1) : GameEvent(Tick);

/// <summary>
/// `TorpedoId` es el torpedo responsable, o -1 cuando no lo hubo — una defensa, por ejemplo.
/// Sirve para unir un disparo con lo que le pasó al analizar la telemetría.
/// </summary>
public sealed record ContactDestroyed(int Tick, int ContactId, Vec2 Position,
    DestructionCause Cause, int TorpedoId = -1) : GameEvent(Tick);
```

- [ ] **Step 4: Rellenarlos**

En `src/Sonar.Core/Simulation.cs`:

En `ResolveDirectHits`:

```csharp
                events.Add(new ContactDestroyed(tick, contact.Id.Value,
                    torpedo.Position, DestructionCause.DirectHit, torpedo.Id.Value));
```

En `ResolveRunOuts`, las dos emisiones:

```csharp
                    events.Add(new ContactDestroyed(tick, contact.Id.Value, contact.Position,
                        DestructionCause.Blast, torpedo.Id.Value));
```

```csharp
            events.Add(new TorpedoDetonated(tick, point, killed, torpedo.Id.Value));
```

En `ResolveTorpedoCollisions`, la detonación de la intercepción:

```csharp
                events.Add(new TorpedoDetonated(tick, own.Position, true, own.Id.Value));
```

- [ ] **Step 5: Correr para verificar que pasan**

Run: `dotnet test`
Expected: `Failed: 0`.

- [ ] **Step 6: Commit**

```bash
git add src/Sonar.Core tests/Sonar.Core.Tests/Combat/ShotOutcomeLinkTests.cs
git commit -m "feat(core): name the responsible torpedo on outcome events"
```

---

## Task 5: Escritor JSONL

**Files:**
- Create: `src/Sonar.Game/Telemetry/JsonLine.cs`
- Create: `src/Sonar.Game/Telemetry/TelemetryWriter.cs`

- [ ] **Step 1: Constructor de líneas JSON**

Crea `src/Sonar.Game/Telemetry/JsonLine.cs`:

```csharp
using System.Globalization;
using System.Text;

namespace Sonar.Game.Telemetry;

/// <summary>
/// Construye una línea JSON a mano. Se hace así y no con un serializador por objeto porque
/// los eventos tienen campos distintos entre sí y definir un tipo por cada uno sería más
/// código que valor.
///
/// Todos los números salen en cultura invariante: con la configuración regional de Chile el
/// separador decimal es la coma, que rompería el JSON.
/// </summary>
public sealed class JsonLine
{
    private readonly StringBuilder _sb = new("{");
    private bool _first = true;

    public JsonLine(string type) => Add("t", type);

    public JsonLine Add(string key, string value) => Field(key, $"\"{Escape(value)}\"");
    public JsonLine Add(string key, int value) => Field(key, value.ToString(CultureInfo.InvariantCulture));
    public JsonLine Add(string key, bool value) => Field(key, value ? "true" : "false");

    public JsonLine Add(string key, double value) =>
        Field(key, value.ToString("0.####", CultureInfo.InvariantCulture));

    /// <summary>Inserta JSON ya formado, para anidar la configuración completa.</summary>
    public JsonLine Raw(string key, string json) => Field(key, json);

    private JsonLine Field(string key, string value)
    {
        if (!_first) _sb.Append(',');
        _first = false;
        _sb.Append('"').Append(Escape(key)).Append("\":").Append(value);
        return this;
    }

    /// <summary>
    /// Escapa lo mínimo que puede aparecer en este archivo. El texto crudo que teclea el
    /// jugador entra acá, así que comillas y contrabarras no son hipotéticas.
    /// </summary>
    private static string Escape(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    public override string ToString() => _sb.ToString() + "}";
}
```

- [ ] **Step 2: El escritor**

Crea `src/Sonar.Game/Telemetry/TelemetryWriter.cs`:

```csharp
using System;
using System.Text.Json;
using Godot;
using Sonar.Core;

namespace Sonar.Game.Telemetry;

/// <summary>
/// Registro de eventos en JSONL, una línea por evento, escrito incrementalmente.
///
/// Incremental y no al cerrar: un cierre forzado perdería la sesión entera, y con oleadas y
/// RETRY las sesiones se alargan.
///
/// Guarda eventos y no promedios: de los eventos se derivan los agregados, y al revés no.
/// Con veinte testers, las preguntas que todavía no se le ocurren a nadie se contestan
/// después sobre los mismos datos.
///
/// **Nada personal.** Id anónimo por sesión, sin nombre de usuario, sin rutas, sin nombre de
/// máquina.
/// </summary>
public sealed class TelemetryWriter : IDisposable
{
    /// <summary>Sube cuando cambie la forma del archivo, para poder mezclar sesiones viejas.</summary>
    private const int Schema = 1;

    public const string Path = "user://m1-session.jsonl";

    private readonly FileAccess? _file;

    public string SessionId { get; } = Guid.NewGuid().ToString("N")[..8];

    public TelemetryWriter(SimulationConfig config, Vector2I viewport)
    {
        _file = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        if (_file is null)
        {
            GD.PushError($"No se pudo abrir {Path} para telemetría");
            return;
        }

        // La configuración completa va en la cabecera: sin ella, dos testers con tuning
        // distinto producen datos que no se pueden juntar.
        Write(new JsonLine("session")
            .Add("schema", Schema)
            .Add("id", SessionId)
            .Add("startedUtc", DateTime.UtcNow.ToString("s"))
            .Add("viewportW", viewport.X)
            .Add("viewportH", viewport.Y)
            .Raw("config", JsonSerializer.Serialize(config)));
    }

    public void Write(JsonLine line)
    {
        if (_file is null) return;
        _file.StoreLine(line.ToString());
        _file.Flush();
    }

    public void Dispose() => _file?.Close();
}
```

- [ ] **Step 3: Verificar que compila**

```bash
dotnet build src/Sonar.Game/Sonar.Game.csproj
```

Expected: `Build succeeded. 0 Warning(s)`.

- [ ] **Step 4: Commit**

```bash
git add src/Sonar.Game/Telemetry
git commit -m "feat(game): add an incremental JSONL telemetry writer"
```

---

## Task 6: Traducir eventos a telemetría

**Files:**
- Create: `src/Sonar.Game/Telemetry/SessionRecorder.cs`
- Modify: `src/Sonar.Game/Main.cs`

- [ ] **Step 1: El traductor**

Crea `src/Sonar.Game/Telemetry/SessionRecorder.cs`:

```csharp
using Sonar.Controller;
using Sonar.Core.Events;

namespace Sonar.Game.Telemetry;

/// <summary>
/// Traduce el flujo de eventos a líneas de telemetría. Vive aparte de Main para que agregar
/// una medición no signifique tocar el cableado de la escena.
///
/// No calcula nada sobre el mundo: todo lo que escribe viene ya medido desde Core. La única
/// excepción es el tiempo de composición, que es tiempo de pared y no existe en la
/// simulación.
/// </summary>
public sealed class SessionRecorder
{
    private readonly TelemetryWriter _writer;

    private int _wave;
    private int _waveStartTick;
    private int _waveShots;
    private int _waveKills;

    public SessionRecorder(TelemetryWriter writer) => _writer = writer;

    public void RunStarted(int attempt, int seed) =>
        _writer.Write(new JsonLine("run").Add("n", attempt).Add("seed", seed));

    public void RunEnded(int attempt, int tick, string cause, int wave,
        int liveEnemies, int ammoLoaded, int ammoReserve, int defenses)
    {
        _writer.Write(new JsonLine("runEnd")
            .Add("n", attempt).Add("tick", tick).Add("cause", cause)
            .Add("wave", wave).Add("liveEnemies", liveEnemies)
            .Add("ammoLoaded", ammoLoaded).Add("ammoReserve", ammoReserve)
            .Add("defenses", defenses));
    }

    /// <summary>Texto crudo tal como se escribió. Los typos son dato, no ruido.</summary>
    public void Command(int tick, string raw, bool recognised, double composeSeconds) =>
        _writer.Write(new JsonLine("cmd")
            .Add("tick", tick).Add("raw", raw)
            .Add("ok", recognised).Add("compose", composeSeconds));

    public void WaveStarted(int wave, int tick, int spawned)
    {
        FlushWave(tick);
        _wave = wave;
        _waveStartTick = tick;
        _waveShots = 0;
        _waveKills = 0;
        _writer.Write(new JsonLine("wave").Add("n", wave).Add("tick", tick).Add("spawned", spawned));
    }

    /// <summary>Cierra la oleada en curso con su resumen. Idempotente si no hay ninguna.</summary>
    public void FlushWave(int tick)
    {
        if (_wave == 0) return;
        _writer.Write(new JsonLine("waveEnd")
            .Add("n", _wave).Add("ticks", tick - _waveStartTick)
            .Add("shots", _waveShots).Add("kills", _waveKills));
        _wave = 0;
    }

    public void Observe(GameEvent evt)
    {
        switch (evt)
        {
            case ShotAssessed e:
                _waveShots++;
                _writer.Write(new JsonLine("shot")
                    .Add("tick", e.Tick).Add("torpedo", e.TorpedoId)
                    .Add("nearest", e.NearestContactId)
                    .Add("errDeg", e.ErrorDegrees).Add("errRad", e.ErrorRadius)
                    .Add("ageTicks", e.ReadingAgeTicks));
                break;

            case TorpedoFired e:
                // Se registran los dos eventos del mismo disparo. Un `fired` sin su `shot`
                // es un tiro al vacío —no había contra qué medirlo—, y sin esta línea esos
                // disparos desaparecerían de los conteos.
                _writer.Write(new JsonLine("fired")
                    .Add("tick", e.Tick).Add("torpedo", e.TorpedoId)
                    .Add("brg", e.BearingDegrees).Add("band", e.BandLetter.ToString()));
                break;

            case ContactDestroyed e:
                _waveKills++;
                _writer.Write(new JsonLine("kill")
                    .Add("tick", e.Tick).Add("contact", e.ContactId)
                    .Add("cause", e.Cause.ToString()).Add("torpedo", e.TorpedoId));
                break;

            case TorpedoDetonated e:
                _writer.Write(new JsonLine("shotEnd")
                    .Add("tick", e.Tick).Add("torpedo", e.TorpedoId)
                    .Add("hit", e.HitSomething));
                break;

            case NearMiss e:
                _writer.Write(new JsonLine("nearMiss")
                    .Add("tick", e.Tick).Add("contact", e.ContactId).Add("dist", e.Distance));
                break;

            case FireRejected e:
                _writer.Write(new JsonLine("fireRejected")
                    .Add("tick", e.Tick).Add("reason", e.Reason.ToString()));
                break;

            case ReloadStarted e:
                _writer.Write(new JsonLine("reload").Add("tick", e.Tick));
                break;

            case DefenseDeployed e:
                _writer.Write(new JsonLine("defense").Add("tick", e.Tick).Add("id", e.DefenseId));
                break;

            case DefenseDetonated e:
                _writer.Write(new JsonLine("defenseSpent")
                    .Add("tick", e.Tick).Add("id", e.DefenseId)
                    .Add("destroyed", e.TargetsDestroyed));
                break;

            case StatusReported e:
                // Pedirlo mucho significa que el HUD no comunica.
                _writer.Write(new JsonLine("status").Add("tick", e.Tick));
                break;

            case SonarToggled e:
                _writer.Write(new JsonLine("sweepToggle")
                    .Add("tick", e.Tick).Add("running", e.Running));
                break;

            case SubHit e:
                _writer.Write(new JsonLine("hit").Add("tick", e.Tick).Add("taken", e.HitsTaken));
                break;
        }
    }
}
```

- [ ] **Step 2: Enchufarlo en `Main`**

Agrega los campos:

```csharp
    private TelemetryWriter _telemetry = null!;
    private SessionRecorder _recorder = null!;
    private int _recordedAttempt;
```

En `_Ready`, después de construir el controller:

```csharp
        _telemetry = new TelemetryWriter(config, DisplayServer.WindowGetSize());
        _recorder = new SessionRecorder(_telemetry);
        _recorder.RunStarted(_controller.Attempt, _controller.Seed);
        _recordedAttempt = _controller.Attempt;
```

En `Report`, como primera línea del método:

```csharp
        _recorder.Observe(evt);
```

En `OnCommandSubmitted`, cambia la llamada a `SubmitInput` para quedarte con lo que devuelve y
registra el comando. `SubmitInput` reporta si entendió el texto; la escena no puede deducirlo
porque el parser vive dentro del controller:

```csharp
        var recognised = _controller.SubmitInput(command);
        _recorder.Command(_controller.Snapshot.Tick, command, recognised,
            _metrics.LastComposeSeconds);
```

Agrega a `SessionMetrics` la propiedad que eso necesita, junto a `Commands`:

```csharp
    /// <summary>Duración de composición del último comando enviado.</summary>
    public double LastComposeSeconds { get; private set; }
```

Y en `NoteSubmit`, después de agregar a la lista:

```csharp
        LastComposeSeconds = _composeTimes[^1];
```

En `_Dispose`/`_ExitTree`, cierra el archivo:

```csharp
        _telemetry.Dispose();
```

- [ ] **Step 3: Verificar**

```bash
dotnet build src/Sonar.Game/Sonar.Game.csproj
godot-mono --path src/Sonar.Game -- --m1-selftest
cat ~/.local/share/godot/app_userdata/Sonar/m1-session.jsonl
```

Expected: build limpio, `[SELFTEST] PASA`, y un archivo cuya primera línea empieza con
`{"t":"session","schema":1,...` e incluye la configuración completa. Tiene que haber al menos
una línea `{"t":"cmd"...` con `"raw":"C047"`.

- [ ] **Step 4: Commit**

```bash
git add src/Sonar.Game
git commit -m "feat(game): record the event stream as JSONL telemetry"
```

---

## Task 7: Oleadas, muertes y RETRY en la telemetría

**Files:**
- Modify: `src/Sonar.Game/Main.cs`

- [ ] **Step 1: Oleadas**

En `_Process`, dentro del bloque que ya detecta el cambio de oleada:

```csharp
        if (_controller.Wave != _announcedWave)
        {
            _announcedWave = _controller.Wave;
            Instrument($"— WAVE {_announcedWave} —");
            _recorder.WaveStarted(_announcedWave, snap.Tick, _controller.LiveEnemyCount);
        }
```

> Ese bloque tiene que quedar **después** de `var snap = _controller.Snapshot;`. Si está
> antes, muévelo.

- [ ] **Step 2: Fin de intento**

Agrega el campo y el chequeo en `_Process`, al final:

```csharp
    private bool _runEndRecorded;
```

```csharp
        if (snap.Destroyed && !_runEndRecorded)
        {
            _runEndRecorded = true;
            _recorder.FlushWave(snap.Tick);
            _recorder.RunEnded(_controller.Attempt, snap.Tick, _lastDeathCause,
                _controller.Wave, _controller.LiveEnemyCount,
                snap.AmmoLoaded, snap.AmmoReserve, snap.DefensesAvailable);
        }
```

Guarda la causa cuando llega el evento. En `Report`, en el caso de `SubDestroyed`:

```csharp
            case SubDestroyed e:
                _lastDeathCause = e.Cause.ToString();
                _eventLog.WriteNow(Dim, e.Cause == SubDeathCause.Collision
                    ? "  COLLISION · HULL BREACHED"
                    : "  HULL BREACHED");
                break;
```

Con el campo:

```csharp
    private string _lastDeathCause = "Unknown";
```

- [ ] **Step 3: RETRY abre un intento nuevo**

En `_Process`, antes de todo lo demás, detecta que el controller reinició:

```csharp
        if (_controller.Attempt != _recordedAttempt)
        {
            _recordedAttempt = _controller.Attempt;
            _recorder.RunStarted(_controller.Attempt, _controller.Seed);
            _announcedWave = 0;
            _runEndRecorded = false;
            _lastDeathCause = "Unknown";
            _eventLog.Queue(Dim, $"— ATTEMPT {_controller.Attempt} —");
        }
```

- [ ] **Step 4: Verificar a mano**

```bash
godot-mono --path src/Sonar.Game
```

Juega hasta morir —lo más rápido es no disparar y dejar que la oleada 1 te embista— y escribe
`RETRY`. Comprueba en el JSONL que aparecen `runEnd` con `"cause":"Collision"` y después un
`run` con `"n":2` y una semilla distinta.

- [ ] **Step 5: Commit**

```bash
git add src/Sonar.Game/Main.cs
git commit -m "feat(game): record waves, deaths and retries"
```

---

## Task 8: Silencios y aviso de privacidad

**Files:**
- Modify: `src/Sonar.Game/Telemetry/SessionRecorder.cs`
- Modify: `src/Sonar.Game/Main.cs`

- [ ] **Step 1: Registrar el hueco antes de cada comando**

En `SessionRecorder.Command`, agrega el hueco desde el comando anterior:

```csharp
    private int _lastCommandTick = -1;

    /// <summary>
    /// Texto crudo tal como se escribió. Los typos son dato, no ruido.
    ///
    /// `gapTicks` es el silencio desde el comando anterior. Con eso se puede preguntar si el
    /// ritmo funciona — y en particular si teclean algo durante los diez segundos iniciales,
    /// que sería señal de que el silencio se lee como que el juego está roto.
    /// </summary>
    public void Command(int tick, string raw, bool recognised, double composeSeconds)
    {
        _writer.Write(new JsonLine("cmd")
            .Add("tick", tick).Add("raw", raw)
            .Add("ok", recognised).Add("compose", composeSeconds)
            .Add("gapTicks", _lastCommandTick < 0 ? -1 : tick - _lastCommandTick));

        _lastCommandTick = tick;
    }
```

- [ ] **Step 2: Aviso en pantalla**

En `_Ready`, junto a las dos líneas de bienvenida:

```csharp
        _eventLog.Queue(Dim,
            $"THIS PROTOTYPE RECORDS ANONYMOUS PLAY DATA TO {TelemetryWriter.Path}");
        _eventLog.Queue(Dim,
            "COMMANDS, SHOTS AND TIMINGS ONLY · NO PERSONAL INFORMATION");
```

- [ ] **Step 3: Verificar**

```bash
dotnet build src/Sonar.Game/Sonar.Game.csproj
godot-mono --path src/Sonar.Game -- --m1-selftest
```

Expected: build limpio y `[SELFTEST] PASA`. En el JSONL, la segunda línea `cmd` tiene que
traer un `gapTicks` positivo.

- [ ] **Step 4: Commit**

```bash
git add src/Sonar.Game
git commit -m "feat(game): record command gaps and disclose what is collected"
```

---

## Task 9: Resumen CSV y hallazgos

**Files:**
- Modify: `src/Sonar.Game/SessionMetrics.cs`
- Modify: `docs/development/m1-findings.md`

- [ ] **Step 1: Agregar oleada al resumen**

En `src/Sonar.Game/SessionMetrics.cs`, agrega la propiedad y su línea:

```csharp
    /// <summary>Oleada más alta alcanzada en toda la sesión.</summary>
    public int BestWave { get; private set; }

    public void NoteWave(int wave)
    {
        if (wave > BestWave) BestWave = wave;
    }
```

En `WriteReport`, después de `contacts_destroyed`:

```csharp
        file.StoreLine($"best_wave,{BestWave}");
```

Y en `Main._Process`, dentro del bloque de cambio de oleada:

```csharp
            _metrics.NoteWave(_announcedWave);
```

- [ ] **Step 2: Reescribir cómo se mide**

En `docs/development/m1-findings.md`, reemplaza la sección `## Cómo se mide` completa por:

```markdown
## Cómo se mide

La fuente de verdad es `user://m1-session.jsonl`: una línea JSON por evento, escrita
incrementalmente. `user://m1-session.csv` es un resumen para mirar de reojo, no para
analizar.

En Linux, los dos quedan en `~/.local/share/godot/app_userdata/Sonar/`.

| Pregunta | De dónde sale |
|---|---|
| 1 · ¿Extrapolar es satisfactorio? | Líneas `shot`: `errDeg` contra `ageTicks`. Si el error crece con la edad de la lectura, la extrapolación existe y cuesta; si es plano, nadie extrapola. Si `errDeg` baja a lo largo de la sesión, se aprende |
| 2 · Tiempo por comando | Líneas `cmd`, campo `compose` |
| 3 · Máximo de contactos | Líneas `waveEnd` y `runEnd`: en qué oleada muere la gente y cuántos quedaban vivos |
| 4 · Período del barrido | La curva de la pregunta 1: dónde `errDeg` se dispara marca el techo |
| 5 · Bandas y radios | `errRad` en las líneas `shot`, y el `config` de la cabecera para comparar tuning |
| 6 · ¿Repetir y ajustar? | Campo `raw` de las líneas `cmd`: los typos y las repeticiones aparecen tal cual se escribieron |
| 7 · ¿La munición aprieta? | Líneas `reload` y `fireRejected`, y `ammoLoaded` en `runEnd` |
| 8 · ¿La fase extrema se dispara o se sobrevive? | `runEnd` por oleada y causa de muerte |

**La sesión es reproducible.** Con la semilla de la línea `run` y los comandos con su tick, se
puede re-ejecutar la partida de cualquier tester y mirarla. Las preguntas que hoy no se le
ocurren a nadie se contestan después sobre los mismos datos.

**Cabecera de sesión.** La línea `session` lleva el `SimulationConfig` completo. Sin eso, dos
testers con tuning distinto producen datos que no se pueden juntar.
```

- [ ] **Step 3: Verificar**

```bash
dotnet build src/Sonar.Game/Sonar.Game.csproj
godot-mono --path src/Sonar.Game -- --m1-selftest
```

Expected: build limpio, `[SELFTEST] PASA`.

- [ ] **Step 4: Commit**

```bash
git add src/Sonar.Game docs/development/m1-findings.md
git commit -m "docs: measure the M1 questions against the JSONL log"
```

---

## Task 10: Cerrar el plan

**Files:**
- Modify: `src/Sonar.Game/Main.cs`
- Modify: `docs/development/godot-wiring.md`
- Modify: `project-state.yaml`

- [ ] **Step 1: Extender el arnés para cubrir RETRY**

En `Main.cs`, cambia el guion del selftest para que también ejercite el reinicio:

```csharp
    private const string SelfTestScript = "c047\nstatus\nretry\n";
```

Y en `ReportSelfTest`, agrega la comprobación:

```csharp
        var ok = _metrics.Commands == 3 && _metrics.ShotsFired == 1 && _sawStatus
                 && _controller.Attempt == 1;   // RETRY vivo no debe reiniciar nada
```

> Con el submarino vivo, `RETRY` se ignora. Que el intento siga en 1 es exactamente lo que
> hay que fijar: un reinicio accidental a mitad de partida sería peor que no tener RETRY.

Ajusta el mensaje impreso para que muestre `commands=…/3` y `attempt=…`.

- [ ] **Step 2: Correrlo seis veces**

```bash
for i in 1 2 3 4 5 6; do godot-mono --path src/Sonar.Game -- --m1-selftest 2>&1 | grep SELFTEST; done
```

Expected: seis `PASA`. Si alguna falla, el arnés volvió a ponerse intermitente: revisa la
guarda de `IsEditing` y el ritmo de una tecla cada tres frames antes de seguir.

- [ ] **Step 3: Documentar la telemetría en el contrato de cableado**

En `docs/development/godot-wiring.md`, reemplaza la sección `## 11 · Métricas` completa por:

```markdown
## 11 · Telemetría

La fuente de verdad es `user://m1-session.jsonl`, una línea JSON por evento, escrita
incrementalmente. `SessionRecorder` traduce el flujo de eventos; `TelemetryWriter` lo escribe.

Tres reglas que conviene no romper al rehacer la escena:

1. **`_recorder.Observe(evt)` va como primera línea del manejador de eventos.** Si se filtra
   por tipo antes, se pierden mediciones sin que nadie se entere.
2. **Se guardan eventos, no promedios.** De los eventos se derivan los agregados; al revés no.
3. **Nada personal.** Id anónimo, sin usuario, sin rutas, sin nombre de máquina. Y el juego
   dice en pantalla qué guarda.

`ShotAssessed` es la medición central y **no se dibuja**: mostrarla le entregaría al jugador
la corrección que el juego le pide calcular.
```

- [ ] **Step 4: Estado del proyecto**

En `project-state.yaml`:

```yaml
  plan_6_watch_and_telemetry: done   # 10/10 tareas
```

```yaml
  shot_telemetry: done
  godot_presentation: prototype      # guardia legible y medida; el diseño visual va tras M1
```

- [ ] **Step 5: Verificación final**

```bash
dotnet test
dotnet build
dotnet build src/Sonar.Game/Sonar.Game.csproj
godot-mono --path src/Sonar.Game -- --m1-selftest
grep -rn "^using Godot" src/Sonar.Core/ src/Sonar.Controller/
grep -rn "new Dictionary\|new HashSet" src/Sonar.Core/
```

Expected: todo verde, los dos greps sin resultados.

Y una partida a mano de un par de minutos: jugar, morir, `RETRY`, jugar de nuevo, cerrar.
Después revisar que el JSONL trae dos líneas `run` con semillas distintas, líneas `shot` con
`errDeg` y `ageTicks` poblados, y al menos un `waveEnd`.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "chore: close the watch and telemetry plan"
```

---

## Definition of Done — Plan 6

- [ ] `dotnet test` verde y `dotnet build` sin advertencias
- [ ] `src/Sonar.Game` compila
- [ ] El selftest pasa seis veces seguidas
- [ ] El HUD muestra reloj de misión y número de oleada, no `tick`
- [ ] Las líneas del registro salen de a una y se alcanzan a leer
- [ ] Un impacto sobre el submarino aparece de inmediato, sin esperar la cola
- [ ] El JSONL trae cabecera con la configuración completa y crece durante la partida
- [ ] Morir y escribir `RETRY` produce un `runEnd` y un `run` nuevo con otra semilla
- [ ] `m1-findings.md` explica de dónde sale cada una de las ocho respuestas
- [ ] Diez commits, uno por tarea

## Qué sigue

Jugar, y hacer jugar. M1 termina cuando las ocho preguntas tienen respuesta anotada en
[m1-findings.md](../../development/m1-findings.md) y los números están en
`config/simulation.working.json`.

Si la respuesta a la pregunta 1 resulta negativa, M1 está igualmente completo: cumplió su
función, que era avisar a tiempo y barato.
