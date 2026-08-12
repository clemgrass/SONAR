# TDD — M1 · Simulación núcleo

**Fecha:** 2026-08-11
**Estado:** propuesto
**Deriva de:** `docs/superpowers/specs/2026-08-11-sonar-preproduction-design.md`
**Alcance:** exclusivamente los sistemas que construye M1

---

## 0. Alcance y no-alcance

Este documento resuelve los items del §63 del GDD **correspondientes a los sistemas de M1**, según
la propia redacción del §63: *"antes de la implementación de los sistemas correspondientes"*.

**Cubre:** §63.1 (arquitectura), §63.2 (game loop), §63.3 (tiempo, parcial), §63.4 (dominio),
§63.5 (coordenadas), §63.6 (sonar), §63.7 (combate), §63.8 (daño, parcial — solo supervivencia del
jugador), §63.9 (armas, parcial), §63.10 (defensa), §63.11 (IA enemiga, mínima), §63.12 (terminal,
parcial), §63.18 (RNG), §63.24 (testing, parcial).

**No cubre, por depender de respuestas de M1 o de milestones posteriores:** estados de daño y
reparación (§63.8, resto), IA enemiga avanzada (§63.11, resto), conversaciones (§63.13), estado
narrativo (§63.14), pistas (§63.15), retry (§63.16), guardado (§63.17), audio (§63.19), UI (§63.20),
input avanzado (§63.21), accesibilidad (§63.22), performance (§63.23), debugging (§63.25),
herramientas (§63.26), localización (§63.31), Steam (§63.32), telemetría (§63.33), seguridad
(§63.34), release (§63.35).

### Terminología

`[DECIDED]` **`Torpedo` para ambos bandos.** No existe el tipo `Missile`. Ver la sección
*Terminología* del §1 del spec. Se distinguen por facción: `TorpedoSide.Own` y `TorpedoSide.Enemy`.

---

## 1. Unidades y espacio

`[DECIDED]` **Unidades normalizadas.** El radio del scope es `1.0`. Toda distancia y velocidad se
expresa en fracciones de scope. Velocidad en fracciones de scope por segundo.

No hay metros ni nudos en la simulación. Si el diálogo menciona velocidades reales, es texto — no
tiene contraparte numérica en Core. Esto elimina una clase entera de bugs de conversión de unidades
y no cuesta nada en M1.

`[DECIDED]` **El submarino está fijo en el origen `(0,0)` y nunca se mueve en espacio de
simulación.** El mundo se mueve a su alrededor.

Es la consecuencia directa del §12 del GDD (el submarino siempre centrado) y hace que la anomalía
del §4.9 del spec sea literalmente una línea de código. Ver §4.4 de este documento.

`[DECIDED]` **Convención angular.** Marcación `0` = arriba (norte), creciendo en sentido horario.

```
bearingRadians = degrees * PI / 180
x =  sin(bearingRadians) * distance
y = -cos(bearingRadians) * distance      // y negativo = norte
```

La inversa, normalizada a `[0, 360)`:

```
degrees = atan2(x, -y) * 180 / PI
if (degrees < 0) degrees += 360
```

`[DECIDED]` **Bandas.** Definidas por un arreglo de radios exteriores en `SimulationConfig`. La
banda `i` cubre `[outerRadii[i-1], outerRadii[i])`, con `outerRadii[-1] = deadZoneRadius`.

La letra de una banda es `(char)('A' + index)`. El rango de letras se deriva del conteo — cinco
bandas dan A–E. Ningún código puede asumir cinco.

`[DECIDED]` **Fuera de rango.** Un contacto con `|position| > 1.0` no aparece en el snapshot y no
es blanco válido. No se destruye: sigue simulándose por si vuelve a entrar. Un torpedo que supera
`1.0` se elimina.

---

## 2. Modelo de dominio

Namespace raíz: `Sonar.Core`. C# moderno: `record` para datos inmutables, `readonly record struct`
para valores pequeños, namespaces con ámbito de archivo, referencias nullable habilitadas.

### 2.1 Tipos de valor

```csharp
namespace Sonar.Core.Spatial;

public readonly record struct Vec2(double X, double Y)
{
    public double Length => System.Math.Sqrt(X * X + Y * Y);
    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
    public static double Distance(Vec2 a, Vec2 b) => (a - b).Length;
}
```

> **No usar `Godot.Vector2`.** `Sonar.Core` no referencia Godot. La conversión ocurre únicamente en
> `Sonar.Game`, al dibujar.

```csharp
namespace Sonar.Core.Spatial;

/// Marcación en grados enteros, siempre normalizada a [0, 360).
public readonly record struct Bearing
{
    public int Degrees { get; }
    public Bearing(int degrees) => Degrees = ((degrees % 360) + 360) % 360;

    public static Bearing FromVector(Vec2 v);   // usa atan2 según §1
    public Vec2 ToUnitVector();
}

/// Índice de banda, base 0. La letra se deriva del índice.
public readonly record struct Band(int Index)
{
    public char Letter => (char)('A' + Index);
    public static bool TryParseLetter(char c, int bandCount, out Band band);
}

/// Objetivo escrito por el jugador: una marcación y una banda.
public readonly record struct TargetCoordinate(Bearing Bearing, Band Band);
```

### 2.2 Identidad de entidades

`[DECIDED]` Todas las entidades llevan un `int` monótono asignado por un contador en `WorldState`.
Los ids **nunca se reutilizan** dentro de una corrida.

Razón: los ids son la clave de orden determinista en resolución de colisiones simultáneas (§3.3) y
la clave de correlación entre lecturas de sonar y contactos.

```csharp
public readonly record struct ContactId(int Value);
public readonly record struct TorpedoId(int Value);
public readonly record struct DefenseId(int Value);
```

### 2.3 Entidades

```csharp
namespace Sonar.Core.Entities;

public enum ContactKind { Enemy, Inert }

public sealed record Contact
{
    public required ContactId Id { get; init; }
    public required ContactKind Kind { get; init; }
    public required Vec2 Position { get; init; }
    /// Velocidad propia, sin incluir la deriva del mundo. Cero para Inert.
    public required Vec2 Velocity { get; init; }
    public required bool Alive { get; init; }
    /// Tick en el que este contacto puede volver a disparar. Solo Enemy.
    public required int NextFireTick { get; init; }
}

public enum TorpedoSide { Own, Enemy }

public sealed record Torpedo
{
    public required TorpedoId Id { get; init; }
    public required TorpedoSide Side { get; init; }
    public required Vec2 Position { get; init; }
    /// Dirección unitaria de viaje. Para Own siempre coincide con Bearing.ToUnitVector();
    /// para Enemy apunta hacia el origen desde donde nació.
    public required Vec2 Direction { get; init; }
    public required double Speed { get; init; }

    // --- Solo Own. Para Enemy son irrelevantes y valen 0. ---
    public required Bearing Bearing { get; init; }
    /// Radio interior desde el que el torpedo puede detonar por contacto.
    public required double ArmedFromRadius { get; init; }
    /// Radio al que agota su corrida y detona con radio de explosión.
    public required double RunToRadius { get; init; }
}

public sealed record Defense
{
    public required DefenseId Id { get; init; }
    /// Posición en espacio de simulación. NO recibe deriva del mundo — ver §4.4.
    public required Vec2 Position { get; init; }
    public required bool Alive { get; init; }
}

public sealed record PlayerSub
{
    public required int AmmoLoaded { get; init; }
    public required int AmmoReserve { get; init; }
    public required int DefensesAvailable { get; init; }
    public required bool ReloadInProgress { get; init; }
    public required int ReloadCompletesAtTick { get; init; }
    /// Rumbo y velocidad actuales. Solo se usan para calcular la deriva del mundo.
    public required Bearing Course { get; init; }
    public required double Speed { get; init; }
    /// Impactos recibidos. Al llegar a 2 termina la corrida (§20 del GDD).
    public required int HitsTaken { get; init; }
    public required bool Destroyed { get; init; }
}
```

### 2.4 Estado del sonar

```csharp
namespace Sonar.Core.Sensing;

/// Lo último que el barrido registró de un contacto.
public sealed record Reading
{
    public required ContactId ContactId { get; init; }
    public required Vec2 Position { get; init; }
    public required int ObservedAtTick { get; init; }
}

public sealed record SonarState
{
    /// Ángulo actual del haz, en grados [0, 360).
    public required double SweepDegrees { get; init; }
    public required bool SweepRunning { get; init; }
    /// Lecturas por contacto, ordenadas por ContactId ascendente. Ver §5.2.
    public required IReadOnlyList<Reading> Readings { get; init; }
}
```

### 2.5 Estado del mundo

```csharp
namespace Sonar.Core;

public sealed record WorldState
{
    public required int Tick { get; init; }
    public required PlayerSub Sub { get; init; }
    public required IReadOnlyList<Contact> Contacts { get; init; }
    public required IReadOnlyList<Torpedo> Torpedoes { get; init; }
    public required IReadOnlyList<Defense> Defenses { get; init; }
    public required SonarState Sonar { get; init; }
    public required int NextEntityId { get; init; }
}
```

`[DECIDED]` **Todas las colecciones son `IReadOnlyList<T>` ordenadas por id ascendente. Nunca
`Dictionary` ni `HashSet` para nada que se itere durante la simulación.**

Razón de determinismo: el orden de enumeración de `Dictionary` en .NET no está garantizado por
contrato. Una lista ordenada por id lo está. Esta regla no es negociable y debe verificarse en
revisión de código.

---

## 3. Bucle de simulación

### 3.1 Paso fijo

`[DECIDED]` `TickDurationSeconds = 1.0 / 60.0`, constante, en `SimulationConfig`.

`Sonar.Core` **no conoce tiempo de pared**. Expone:

```csharp
public interface ISimulation
{
    WorldState State { get; }
    /// Avanza exactamente un tick. Consume los comandos encolados.
    IReadOnlyList<GameEvent> Tick(IReadOnlyList<Command> commands);
}
```

### 3.2 Orden de actualización

`[DECIDED]` Fijo y documentado. Cambiarlo cambia el resultado de la simulación y por tanto rompe el
determinismo respecto a corridas anteriores.

1. Aplicar comandos encolados, en el orden en que llegaron
2. Actualizar rumbo y velocidad del submarino (guion de misión)
3. Calcular deriva del mundo: `worldDrift = -Sub.Course.ToUnitVector() * Sub.Speed`
4. Mover contactos: `pos += (Velocity + worldDrift) * dt`
5. **Disparo enemigo** (§4.6), en orden de `ContactId` ascendente
6. Mover torpedos: `pos += Direction * Speed * dt`
7. Resolver colisiones (§3.3)
8. Resolver corridas agotadas y detonaciones por radio
9. **Resolver impactos sobre el submarino** (§4.7)
10. Avanzar el barrido y registrar lecturas (§5)
11. Avanzar temporizador de recarga
12. Retirar entidades muertas y fuera de rango
13. `Tick++`

### 3.3 Resolución de colisiones simultáneas

`[DECIDED]` Se evalúan **torpedos en orden de `TorpedoId` ascendente**. Para cada torpedo, se
evalúan **contactos en orden de `ContactId` ascendente** y se detona con el **primero** que cumpla
la condición de impacto.

Un contacto ya destruido en este mismo tick no puede volver a ser impactado. Un torpedo que ya
detonó no sigue evaluándose.

Esto elimina toda ambigüedad de simultaneidad sin necesidad de desempates aleatorios.

### 3.4 Invariante anti-tunneling

`[DECIDED]` **`hitRadius > maxTorpedoSpeed * TickDurationSeconds`**, siempre.

Si el torpedo avanza más por tick que su propio radio de impacto, puede atravesar un contacto sin
detectarlo. Con `dt = 1/60` y velocidad `0.7`, el avance por tick es `≈0.0117`; cualquier
`hitRadius` mayor a eso es seguro.

`[DECIDED]` Esta invariante se verifica en la construcción de `SimulationConfig` y lanza si no se
cumple. Es un error de configuración, no un caso a manejar en tiempo de ejecución.

---

## 4. Combate

### 4.1 Disparo

Al aplicarse un `FireCommand(TargetCoordinate)`:

1. Si `AmmoLoaded == 0` → evento `FireRejected(NoAmmo)`, no se crea torpedo
2. Si `ReloadInProgress` → evento `FireRejected(Reloading)`
3. Si no, se crea un `Torpedo` con:
   - `Bearing` = la marcación pedida
   - `Position` = `Bearing.ToUnitVector() * config.DeadZoneRadius`
   - `ArmedFromRadius` = `config.DeadZoneRadius`
   - `RunToRadius` = radio central de la banda pedida
   - `Speed` = `config.TorpedoSpeed`
4. `AmmoLoaded--`
5. Evento `TorpedoFired`

`[DECIDED]` El torpedo **nace en el borde de la zona muerta**, no en el origen. Simplifica el
render y hace la zona muerta visible sin código extra.

`[DECIDED]` `RunToRadius` es el **radio central** de la banda, no su borde exterior. Así la banda
se comporta como una distancia nominal con tolerancia a ambos lados, que es lo que el spec §4.3
describe.

### 4.2 Impacto directo

Para cada torpedo, cada tick, después de moverse:

```
si torpedo.Position.Length >= torpedo.ArmedFromRadius:
    para cada contacto vivo, en orden de id:
        si Distance(torpedo.Position, contacto.Position) < config.HitRadius:
            destruir contacto
            emitir ContactDestroyed(contacto.Id, torpedo.Position, DirectHit)
            eliminar torpedo
            terminar
```

`[DECIDED]` **El torpedo choca con lo primero que se le cruce, sea `Enemy` o `Inert`.** Un pecio
detiene un torpedo. Es coherente con la física y con el spec §4.3, y da consecuencia táctica a los
contactos inertes.

### 4.3 Detonación de fin de corrida

```
si torpedo.Position.Length >= torpedo.RunToRadius:
    detonationPoint = torpedo.Bearing.ToUnitVector() * torpedo.RunToRadius
    destruidos = contactos vivos con Distance(pos, detonationPoint) < config.BlastRadius
                 (evaluados en orden de id)
    emitir TorpedoDetonated(detonationPoint, destruidos.Count > 0)
    para cada destruido: emitir ContactDestroyed(id, pos, Blast)
    eliminar torpedo
```

`[DECIDED]` La detonación por radio puede destruir **más de un contacto**. No hay razón para
limitarlo a uno y limitarlo requeriría una regla de desempate arbitraria.

`[DECIDED]` **Ventana de casi-impacto.** Para la pista 2 del spec §4.10, cuando la detonación no
destruye nada se calcula la distancia al contacto vivo más cercano. Si cae en
`[config.BlastRadius, config.NearMissRadius)`, se emite además `NearMiss(contactId, distance)`.

Este evento no tiene efecto en M1 y no se muestra en la UI. Se emite desde ahora para que el
sistema de pistas lo consuma en un milestone posterior sin tocar el código de combate.

### 4.4 Defensas — y la anomalía

Al aplicarse `DeployDefenseCommand(TargetCoordinate)`:

- Si `DefensesAvailable == 0` → `DefenseRejected(NoneAvailable)`
- Si no, se crea `Defense` con `Position` = punto central de la coordenada pedida

Cada tick, una defensa destruye cualquier torpedo (propio o ajeno) y cualquier contacto **enemigo**
cuya distancia sea menor a `config.DefenseRadius`, evaluando en orden de id. La defensa sobrevive.

`[DECIDED]` Los contactos **inertes** son inmunes a las defensas y las atraviesan sin efecto.

Razón: la anomalía de abajo sólo es observable si el jugador puede dejar una defensa junto a una
referencia inerte y verlas separarse. Con la regla original —"destruye cualquier contacto"— el
experimento natural que revela la anomalía la borraría: el pecio desaparece en el instante en que
la defensa se despliega a su lado. La excepción no es una concesión mecánica sino la condición
para que la pista exista. Narrativamente tampoco cuesta nada: un pecio ya está hundido.

Lo pinchan `DefenseTests.Defense_LeavesInertContactsAlone` y los dos `DefenseAnomalyTests` que
colocan pecio y defensa en el mismo punto.

> ### La anomalía del §25 del GDD
>
> En el paso 4 de §3.2, los contactos reciben `+ worldDrift`. **Las defensas no aparecen en ese
> paso.** No tienen velocidad y nada las mueve.
>
> Como el submarino está fijo en el origen y el mundo deriva a su alrededor, una defensa quieta en
> espacio de simulación es una defensa que **acompaña al submarino** en la ficción. Un pecio, que
> sí recibe la deriva, se aleja.
>
> **Eso es toda la implementación de la anomalía.** No hay código especial, no hay bandera, no hay
> caso raro. Hay una omisión deliberada, y está documentada aquí para que nadie la "arregle"
> agregándole deriva a las defensas creyendo que es un bug.

### 4.5 Colisiones de torpedos enemigos

`[DECIDED]` Un torpedo `Enemy` **no daña a los contactos**. No hay fuego amigo entre enemigos, y
tampoco atraviesa contactos inertes: los ignora por completo.

Razón: modelar fuego amigo enemigo abre una lata de gusanos de IA (evitar la línea de tiro,
reposicionarse) que no aporta nada a lo que M1 mide, y crea situaciones donde el jugador gana sin
hacer nada.

Un torpedo `Enemy` **sí** es destruido por una defensa (§4.4) y **sí** puede chocar con un torpedo
`Own`. En ambos casos se destruyen los dos, lo cual es exactamente el §24 del GDD.

`[DECIDED]` Torpedo contra torpedo se evalúa en orden de `TorpedoId` ascendente, con la misma regla
de primer impacto de §3.3.

### 4.6 Disparo enemigo — IA mínima de M1

`[DECIDED]` El comportamiento más simple que produce presión real:

```
para cada contacto Enemy vivo y dentro de rango, en orden de ContactId:
    si Tick >= contacto.NextFireTick:
        crear Torpedo con Side = Enemy
            Position  = contacto.Position
            Direction = normalizar(origen - contacto.Position)   // recto hacia el submarino
            Speed     = config.EnemyTorpedoSpeed
        contacto.NextFireTick = Tick + config.EnemyFireIntervalTicks
        emitir EnemyTorpedoFired(contacto.Id)
```

`[DECIDED]` **Dispara hacia dónde está el submarino, no hacia dónde estará.** El submarino está
fijo en el origen, así que la distinción no existe: el torpedo enemigo viaja recto por la marcación
del contacto en el momento del disparo. Esquivar no es una mecánica (§12); la respuesta del jugador
es la defensa o destruir la fuente.

`[DECIDED]` **`NextFireTick` se inicializa escalonado** al crear cada contacto:
`NextFireTick = spawnTick + config.EnemyFireIntervalTicks + (contactIndex * config.EnemyFireStaggerTicks)`.

Sin escalonar, todos los contactos disparan en el mismo tick y la presión llega en oleadas
sincronizadas en vez de sostenida. Es determinista y no usa el RNG.

`[DECIDED]` **La IA de M1 no evade, no persigue y no elige blanco.** El §63.11 completo queda para
un milestone posterior. Lo que M1 necesita medir es al jugador bajo presión, no la inteligencia del
enemigo.

### 4.7 Supervivencia del jugador

`[DECIDED]` Un torpedo `Enemy` cuya distancia al origen sea menor a `config.SubHitRadius` impacta:

```
Sub.HitsTaken++
eliminar torpedo
emitir SubHit(HitsTaken)
si Sub.HitsTaken >= 2:
    Sub.Destroyed = true
    emitir SubDestroyed
```

`[DECIDED]` **Dos impactos y se acaba la corrida** (§20 del GDD). **Sin desactivación de sistemas
en M1.** Los estados de daño del §21 y el comando `REPAIR` van a un milestone posterior, porque el
§63.8 exige decidir qué sistema cae y la prohibición de azar de §4.3 del spec obliga a que esa
selección sea determinista — un diseño que todavía no existe.

`[DECIDED]` Con `Destroyed = true`, la simulación **deja de avanzar**. `Tick()` devuelve una lista
vacía de eventos. Reiniciar es responsabilidad del Controller, no de Core.

`[DECIDED]` Corolario de la regla de confirmación (spec §4.6): **el impacto sobre el propio
submarino se confirma siempre.** No depende del barrido, y el evento se emite en el mismo tick.

### 4.8 Recarga

`RELOAD` cuando `AmmoReserve > 0` y no hay recarga en curso:

- `ReloadInProgress = true`, `ReloadCompletesAtTick = Tick + config.ReloadTicks`
- Evento `ReloadStarted`

Al alcanzarse el tick: mueve `min(config.MagazineSize - AmmoLoaded, AmmoReserve)` de reserva a
cargador, `ReloadInProgress = false`, evento `ReloadCompleted`.

`[DECIDED]` La recarga **no se puede interrumpir ni cancelar** en M1. El §63.9 pregunta por
interrupción y fallo de recarga; ambas son mecánicas de tensión que dependen del sistema de daño,
que no está en M1.

---

## 5. Sonar

### 5.1 Barrido

`[DECIDED]` `SweepDegrees` avanza `360 / config.SweepPeriodSeconds * dt` por tick, módulo 360.
`SONAR` alterna `SweepRunning`. Con el barrido detenido el ángulo se congela y no se registran
lecturas nuevas.

### 5.2 Detección con gating por clase

`[DECIDED]` Solo los **contactos** están sujetos a gating. Torpedos propios, defensas, detonaciones
y **torpedos enemigos** entran al snapshot siempre.

Las cuatro clases de información del spec §4.1, ya completas en M1:

| Clase | En el snapshot |
|---|---|
| Contacto enemigo o inerte | posición de la **lectura**, con edad |
| Torpedo o defensa propios | posición **real**, cada tick |
| Torpedo enemigo | posición **real**, cada tick — se detecta por ruido, no por barrido |
| Detonación e impacto | evento, en el tick en que ocurre |

`[DECIDED]` El torpedo enemigo se dibuja con menos precisión aparente que uno propio —es la clase
menos fiable del spec §4.1—, pero **eso es decisión de presentación**. El dato que entrega Core es
exacto; `Sonar.Game` decide cómo comunicar la incertidumbre. Core no miente nunca.

Detección de cruce, con manejo de la vuelta completa:

```csharp
bool Crossed(double prev, double cur, double target) =>
    cur >= prev ? (target > prev && target <= cur)
                : (target > prev || target <= cur);
```

Cada tick, para cada contacto vivo dentro de rango, en orden de id: si el haz cruzó su marcación
actual, se reemplaza su `Reading` por una nueva con la posición y el tick actuales.

`[DECIDED]` **Se guarda una sola lectura por contacto**, la más reciente. El §15 del GDD pide que
el jugador interprete posiciones sucesivas — eso lo hace **el jugador con su memoria y su
cuaderno**, no el juego mostrándole un historial de trazas.

Es una decisión de diseño, no una limitación técnica: mostrar la traza convertiría la extrapolación
en lectura pasiva y vaciaría la habilidad central que M1 está validando.

`[DECIDED]` La lectura de un contacto destruido se elimina de inmediato. La confirmación de
destrucción es una ley del mundo (spec §4.6) y no depende del barrido.

### 5.3 Envejecimiento

La edad de una lectura es `Tick - ObservedAtTick`. Se expone **en ticks** en el snapshot. Convertir
edad a opacidad es decisión de presentación y vive en `Sonar.Game`.

---

## 6. Comandos y parser

### 6.1 Tipos de comando

```csharp
namespace Sonar.Core.Commands;

public abstract record Command;

public sealed record FireCommand(TargetCoordinate Target) : Command;
public sealed record DeployDefenseCommand(TargetCoordinate Target) : Command;
public sealed record ReloadCommand : Command;
public sealed record ToggleSonarCommand : Command;
public sealed record StatusCommand : Command;
/// Texto que no corresponde a ningún comando. Se registra y no hace nada (§10 del GDD).
public sealed record UnrecognizedInput(string RawText) : Command;
```

`[DECIDED]` `UnrecognizedInput` **es un comando**, no un error. El §10 del GDD exige que todo input
quede registrado y que lo desconocido simplemente no haga nada. Modelarlo como comando hace que el
historial sea uniforme y que el sistema de conversaciones pueda engancharse después sin tocar el
parser.

### 6.2 Gramática

Vive en `Sonar.Controller`, no en Core. Core recibe comandos ya construidos.

```
input        := espacios* comando espacios*
comando      := disparo | defensa | RELOAD | SONAR | STATUS
disparo      := [ ("FIRE" | "F") espacios+ ] coordenada
defensa      := ("DEF" | "DEFEND") espacios+ coordenada
coordenada   := letra_banda espacios* dígitos{1,3}
```

`[DECIDED]` **Normalización:** recortar extremos, colapsar espacios internos, convertir a
mayúsculas. La comparación es insensible a mayúsculas.

`[DECIDED]` **Validación de marcación:** `0–359` se acepta. Un valor `≥ 360` produce
`UnrecognizedInput`, **no** se toma módulo.

Razón: tomar módulo haría que `C400` disparara silenciosamente a `C040`. El jugador escribió algo
que no existe y el sistema debe no hacer nada, que es exactamente lo que pide el §10.

`[DECIDED]` **Validación de banda:** la letra debe estar dentro del conteo configurado. `C047` con
tres bandas configuradas es `UnrecognizedInput`.

`[DECIDED]` Se aceptan `C47` y `C047`. El relleno de ceros es opcional a la entrada; la salida
siempre se formatea con tres dígitos.

### 6.3 Superficie del Controller

```csharp
namespace Sonar.Controller;

public sealed class GameController
{
    public GameController(SimulationConfig config, int seed);

    /// Encola texto crudo. Se parsea aquí; se aplica en la próxima frontera de tick.
    public void SubmitInput(string rawText);

    /// Acumula tiempo real y avanza la simulación en pasos fijos.
    /// Devuelve todos los eventos producidos por los ticks ejecutados.
    public IReadOnlyList<GameEvent> Advance(double deltaSeconds);

    /// Estado de presentación del último tick ejecutado.
    public WorldSnapshot Snapshot { get; }
}
```

`[DECIDED]` **Acumulador con tope.** `Advance` ejecuta como máximo `config.MaxCatchUpTicks` por
llamada y descarta el excedente.

Sin el tope, un congelamiento de la ventana produce una avalancha de ticks que el jugador
experimenta como un salto — la clásica espiral de la muerte. El §63.2 del GDD pregunta por
comportamiento en frames largos; esta es la respuesta.

---

## 7. Snapshot y eventos

### 7.1 Snapshot

`[DECIDED]` Inmutable, listo para dibujar, **sin tipos de dominio**. `Sonar.Game` no debe conocer
`Contact` ni `Torpedo`.

```csharp
namespace Sonar.Controller.Presentation;

public sealed record ContactView(
    int Id, Vec2 Position, int BearingDegrees, char BandLetter, int AgeTicks);

/// Torpedo propio. Posición real, cada tick.
public sealed record TorpedoView(int Id, Vec2 Position, int BearingDegrees, double RunToRadius);

/// Torpedo enemigo. Posición real, cada tick — se detecta por ruido, no por barrido.
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

`[DECIDED]` `ContactView.Position` es la **posición de la lectura**, no la posición real. Es el
gating manifestándose en el contrato de datos: la capa de presentación **no puede** dibujar la
verdad aunque quisiera, porque no la recibe.

Esta es la línea de defensa más fuerte del diseño. Un agente que intente "arreglar" el sonar para
que muestre posiciones actualizadas descubrirá que el dato no existe en su capa.

### 7.2 Eventos

```csharp
namespace Sonar.Core.Events;

public abstract record GameEvent(int Tick);

public sealed record TorpedoFired(int Tick, int TorpedoId, int BearingDegrees, char BandLetter)
    : GameEvent(Tick);
public sealed record TorpedoDetonated(int Tick, Vec2 Position, bool HitSomething)
    : GameEvent(Tick);
public sealed record ContactDestroyed(int Tick, int ContactId, Vec2 Position, DestructionCause Cause)
    : GameEvent(Tick);
public sealed record NearMiss(int Tick, int ContactId, double Distance) : GameEvent(Tick);
public sealed record FireRejected(int Tick, RejectionReason Reason) : GameEvent(Tick);
public sealed record DefenseDeployed(int Tick, int DefenseId, Vec2 Position) : GameEvent(Tick);
public sealed record DefenseRejected(int Tick, RejectionReason Reason) : GameEvent(Tick);
public sealed record ReloadStarted(int Tick) : GameEvent(Tick);
public sealed record ReloadCompleted(int Tick, int AmmoLoaded) : GameEvent(Tick);
public sealed record SonarToggled(int Tick, bool Running) : GameEvent(Tick);
public sealed record StatusReported(int Tick, int AmmoLoaded, int AmmoReserve, int Defenses)
    : GameEvent(Tick);
public sealed record InputNotRecognized(int Tick, string RawText) : GameEvent(Tick);

// --- Amenaza enemiga ---
public sealed record EnemyTorpedoFired(int Tick, int TorpedoId, int SourceContactId)
    : GameEvent(Tick);
public sealed record EnemyTorpedoDestroyed(int Tick, int TorpedoId, Vec2 Position,
    DestructionCause Cause) : GameEvent(Tick);
public sealed record SubHit(int Tick, int HitsTaken) : GameEvent(Tick);
public sealed record SubDestroyed(int Tick) : GameEvent(Tick);

public enum DestructionCause { DirectHit, Blast, Defense, TorpedoCollision }
public enum RejectionReason { NoAmmo, Reloading, NoneAvailable }
```

---

## 8. Determinismo

### 8.1 Reglas obligatorias

`[DECIDED]` Verificables en revisión de código:

1. Paso fijo. Core nunca recibe un delta variable.
2. Sin `DateTime.Now`, `Environment.TickCount`, `Stopwatch` ni `Random.Shared` dentro de Core.
3. Todo azar pasa por `IRandomSource`, inyectado y sembrado en el constructor.
4. Toda iteración de simulación recorre listas ordenadas por id. Nunca `Dictionary` ni `HashSet`.
5. Sin paralelismo dentro de Core.
6. Orden de actualización según §3.2, sin excepciones.

```csharp
public interface IRandomSource
{
    int NextInt(int minInclusive, int maxExclusive);
    double NextDouble();
}
```

### 8.2 Hash de estado

```csharp
public static long ComputeStateHash(WorldState state);
```

FNV-1a de 64 bits sobre: tick, munición, y por cada entidad en orden de id — su id y sus
coordenadas cuantizadas a `1e-9`.

`[DECIDED]` **Cuantizar antes de hashear.** El hash debe detectar divergencia real, no ruido de
último bit. Sin cuantización el test se vuelve inestable y se termina ignorando, que es peor que no
tenerlo.

### 8.3 Alcance

Reproducibilidad en la misma máquina. No se promete identidad bit a bit entre plataformas — ver
spec §5.4.

---

## 9. Configuración

`[DECIDED]` Un único `record` inmutable, cargado desde JSON, validado al construirse. Es el §61 del
GDD.

```csharp
public sealed record SimulationConfig
{
    public double TickDurationSeconds { get; init; } = 1.0 / 60.0;
    public int MaxCatchUpTicks { get; init; } = 5;

    public double DeadZoneRadius { get; init; }
    public IReadOnlyList<double> BandOuterRadii { get; init; }

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

    public void Validate();   // lanza si alguna invariante no se cumple
}
```

`Validate()` comprueba, como mínimo:

- `HitRadius > TorpedoSpeed * TickDurationSeconds` — invariante anti-tunneling (§3.4)
- `HitRadius > EnemyTorpedoSpeed * TickDurationSeconds` — la misma, para torpedo contra torpedo
- `SubHitRadius > EnemyTorpedoSpeed * TickDurationSeconds` — si no, un torpedo enemigo puede
  atravesar el submarino sin impactarlo
- `DefenseRadius > EnemyTorpedoSpeed * TickDurationSeconds` — si no, las defensas dejan pasar
  torpedos rápidos y el jugador pierde por una razón que no puede ver
- `BlastRadius >= HitRadius`
- `NearMissRadius > BlastRadius`
- `SubHitRadius < DeadZoneRadius` — el submarino debe ser alcanzable dentro de la zona donde sus
  propios torpedos no pueden estar armados
- `BandOuterRadii` estrictamente creciente, primer valor `> DeadZoneRadius`, último `<= 1.0`
- `EnemyFireIntervalTicks > 0`, `EnemyFireStaggerTicks >= 0`
- Todos los valores positivos

`[DECIDED]` Los valores concretos **no se fijan en este documento**. Son la salida de M1. El archivo
de configuración inicial lleva valores de trabajo marcados como tales.

---

## 10. Estrategia de tests

### 10.1 Test de determinismo — el primero que se escribe

```csharp
[Fact]
public void SameSeed_ProducesIdenticalStateHash()
{
    var script = ScenarioScripts.MixedCombat();   // secuencia fija de comandos por tick
    var hashA = RunAndHash(seed: 12345, script, ticks: 3600);
    var hashB = RunAndHash(seed: 12345, script, ticks: 3600);
    Assert.Equal(hashA, hashB);
}

[Fact]
public void DifferentSeed_ProducesDifferentStateHash()
{
    var script = ScenarioScripts.MixedCombat();
    Assert.NotEqual(RunAndHash(11111, script, 3600), RunAndHash(22222, script, 3600));
}
```

El segundo test importa tanto como el primero: sin él, una simulación accidentalmente sin azar
pasaría el primero y nadie se enteraría.

### 10.2 ScenarioRunner

Arnés headless que ejecuta un guion `(tick → comandos)` sobre una configuración dada y devuelve
todos los eventos. Es la herramienta con la que se prueba todo lo demás, y es lo que hace cierta la
afirmación del spec §5.5: la campaña se puede jugar sin abrir Godot.

### 10.3 Casos de combate obligatorios

Cada uno con contactos colocados en posiciones exactas, sin azar:

| Caso | Espera |
|---|---|
| Torpedo por la marcación del contacto, banda correcta | `ContactDestroyed` con causa `DirectHit` |
| Banda demasiado corta | `TorpedoDetonated(HitSomething: false)`, contacto vivo |
| Banda demasiado larga | contacto destruido **en el camino** — verifica §4.2 |
| Contacto ligeramente fuera de marcación, dentro de radio de explosión | destruido con causa `Blast` |
| Contacto fuera del radio de explosión, dentro de ventana de casi-impacto | vivo, emite `NearMiss` |
| Contacto dentro de la zona muerta | ningún torpedo puede alcanzarlo |
| Contacto inerte entre el torpedo y un enemigo | el inerte detiene el torpedo |
| Dos contactos dentro del radio de explosión | ambos destruidos |
| Disparar sin munición | `FireRejected(NoAmmo)`, sin torpedo creado |
| Recarga completa tras `ReloadTicks` | `ReloadCompleted` con el cargador correcto |

### 10.4 Casos de amenaza enemiga obligatorios

| Caso | Espera |
|---|---|
| Contacto enemigo alcanza `NextFireTick` | `EnemyTorpedoFired`, torpedo con `Side = Enemy` |
| Torpedo enemigo llega al origen | `SubHit(1)`, torpedo eliminado |
| Segundo impacto | `SubHit(2)` seguido de `SubDestroyed` |
| Tras `SubDestroyed` | `Tick()` devuelve lista vacía; el estado no avanza |
| Defensa en la trayectoria de un torpedo enemigo | `EnemyTorpedoDestroyed` con causa `Defense`, defensa viva |
| Torpedo propio y enemigo se cruzan | ambos destruidos, causa `TorpedoCollision` |
| Torpedo enemigo cruza un contacto inerte | lo atraviesa sin efecto — verifica §4.5 |
| Torpedo enemigo cruza otro contacto enemigo | lo atraviesa sin efecto — sin fuego amigo |
| Contacto inerte | nunca dispara |
| Varios enemigos creados juntos | sus `NextFireTick` quedan escalonados, no coinciden |

### 10.5 Casos de sonar obligatorios

| Caso | Espera |
|---|---|
| El haz cruza un contacto | se registra lectura con el tick actual |
| El contacto se mueve sin que el haz pase | la lectura no cambia; la edad crece |
| El haz cruza el paso por 0/360 | el cruce se detecta igual |
| Barrido detenido | no se registran lecturas nuevas |
| Contacto destruido | su lectura desaparece de inmediato |
| Torpedo propio | aparece en el snapshot sin depender del barrido |

### 10.6 Casos de parser — dirigidos por tabla

| Entrada | Resultado |
|---|---|
| `C047` | `FireCommand(C, 47)` |
| `c47` | `FireCommand(C, 47)` |
| `FIRE C047` | `FireCommand(C, 47)` |
| `  f   c 47  ` | `FireCommand(C, 47)` |
| `C400` | `UnrecognizedInput` |
| `Z047` | `UnrecognizedInput` |
| `DEF C047` | `DeployDefenseCommand(C, 47)` |
| `RELOAD` | `ReloadCommand` |
| `hello` | `UnrecognizedInput` |
| `` (vacío) | ningún comando encolado |

---

### 10.7 Corrección de alcance aplicada

`[RESOLVED — 2026-08-11]` La primera versión de este documento detectó que M1, tal como lo definía
el spec §7.1, no podía responder su propia pregunta 8: sin amenaza no hay supervivencia que medir.

**Resuelto:** el ataque enemigo entra a M1 (§4.5, §4.6, §4.7). El GDD ya lo establecía en §14, §20,
§24 y §26; la omisión estaba en el acotamiento del spec, no en el diseño.

**Límite del alcance:** dos impactos y se acaba la corrida (§20). **Sin estados de daño ni
`REPAIR`** (§21), que van a un milestone posterior porque el §63.8 exige decidir qué sistema cae y
la prohibición de azar obliga a que esa selección sea determinista — diseño que aún no existe.

Con esto M1 ejercita las **cuatro** clases de información del spec §4.1, que son las mismas cuatro
reglas que la demo debe enseñar (spec §7.2), y las preguntas 2, 3, 6 y 7 se miden bajo presión real
en vez de contra blancos pasivos.

---

## 11. Decisiones abiertas que hereda M1

| Ref | Decisión | Cómo se resuelve |
|---|---|---|
| §9 | Todos los valores de `SimulationConfig` | Salida de M1 |
| §1 | Número de bandas | Pregunta 5 de M1 |
| §4.3 | `NearMissRadius` | Pregunta 5 de M1 |
| §5.1 | `SweepPeriodSeconds` | Pregunta 4 de M1 |
| §6.2 | Repetición y ajuste fino (`+`/`-`, Enter vacío) | Pregunta 6 de M1 |
| §4.1 | `SALVO` | Pregunta 7 de M1 |

## 12. Trazabilidad

| Sección | Spec | GDD |
|---|---|---|
| §1 Unidades y espacio | §4.2 | §12, §16, §63.5 |
| §2 Dominio | §4.1–4.3 | §63.4 |
| §3 Bucle | §5.3 | §63.2, §63.3 |
| §4 Combate | §4.3, §4.6, §4.9 | §17–§19, §24, §25, §63.7, §63.10 |
| §4.5–4.7 Amenaza enemiga | §7.1 | §14, §20, §24, §26, §63.8, §63.11 |
| §5 Sonar | §4.1 | §13, §14, §15, §63.6 |
| §6 Comandos | §4.5 | §10, §11, §63.12 |
| §7 Snapshot y eventos | §5.2 | §60 |
| §8 Determinismo | §4.4, §5.4 | §62, §63.18 |
| §9 Configuración | — | §61, §64 |
| §10 Tests | §6.5 | §63.24, §65 |
