# Cableado de Godot — contrato con el Controller

Este documento es el contrato entre la escena de Godot y la simulación. Está escrito para
poder construir toda la presentación **sin leer `Sonar.Core`**: si algo que necesitas para
dibujar no aparece acá, o no existe todavía o está deliberadamente oculto.

Cubre las tareas 8 y 9 del plan 4. El diseño visual es decisión de la capa de presentación;
lo de acá abajo es lo que no se puede cambiar sin romper la simulación.

---

## 1 · La regla de capas

```
Sonar.Game (Godot)  ──referencia──▶  Sonar.Controller  ──referencia──▶  Sonar.Core
```

Las flechas van en un solo sentido y no hay ninguna de vuelta. **Ni `Sonar.Core` ni
`Sonar.Controller` referencian Godot**, y esa ausencia es todo el mecanismo de defensa: si
el Controller conociera Godot, tipos de Godot podrían filtrarse hacia la simulación y el
Core dejaría de ser testeable sin abrir el editor.

Consecuencia práctica: en `Sonar.Game` puedes usar `Vector2`, `Color`, `Node2D` y lo que
quieras. En cuanto un tipo de Godot aparezca en una firma de `Sonar.Controller`, algo se
hizo mal.

Está verificado por grep, y el chequeo es parte de la Definition of Done:

```bash
grep -rn "Godot" src/Sonar.Core/ src/Sonar.Controller/
```

---

## 2 · Todo lo que expone `GameController`

Es la única clase que la escena necesita tocar.

```csharp
public sealed class GameController
{
    public GameController(SimulationConfig config, int baseSeed, Bearing course, double speed);

    public WorldSnapshot Snapshot { get; }
    public IReadOnlyList<GameEvent> LastEvents { get; }

    public bool Started { get; }        // falso hasta que llegue START
    public int Attempt { get; }         // intento actual, base 1. RETRY lo incrementa
    public int Seed { get; }            // semilla de este intento. Anotarla lo reproduce
    public int Wave { get; }
    public int LiveEnemyCount { get; }

    public bool SubmitInput(string rawText);
    public IReadOnlyList<GameEvent> Advance(double deltaSeconds);
}
```

- **`Advance(delta)`** acumula tiempo real y corre los ticks completos que quepan. La
  simulación nunca ve tiempo de pared. Devuelve los eventos de esos ticks. **Antes de `START`
  no hace nada y tampoco acumula**: si acumulara, quien se toma dos minutos leyendo el
  instructivo recibiría una avalancha de ticks al arrancar.
- **`SubmitInput(texto)`** parsea y **encola**. El comando se aplica en la próxima frontera
  de tick, nunca a mitad. Texto vacío no encola nada. Texto que no es un comando encola un
  `UnrecognizedInput`, que no es un error. Devuelve si entendió el texto — la escena no puede
  deducirlo, porque el parser vive acá dentro.
- **`Snapshot`** se reconstruye en cada `Advance`. Es inmutable: guardarlo entre frames es
  seguro, mutarlo es imposible.

**No hay `SetContacts`.** Los contactos los coloca el `WaveDirector`, que vive dentro del
controller: la escena le da rumbo y avance, y él arma las oleadas cuando toca.

No hay `Pause()` ni acceso al `WorldState`. Si hace falta alguno, se agrega acá y se testea
acá, no en la escena.

---

## 3 · Secuencia por frame

```csharp
public override void _Process(double delta)
{
    if (_controller is null) return;

    foreach (var evt in _controller.Advance(delta))
        Report(evt);              // 1 · eventos → registros visuales y sonido

    _sonar.SetSnapshot(_controller.Snapshot);   // 2 · estado → dibujo
    UpdateHud(_controller.Snapshot);            // 3 · estado → HUD
}
```

Ese orden importa. Los eventos son **cosas que pasaron** durante el frame y se consumen una
sola vez; el snapshot es **cómo está el mundo ahora** y se puede leer todas las veces que
quieras. Confundirlos lleva a dibujar explosiones que quedan pegadas o a perder impactos.

`Advance` puede devolver cero eventos (lo normal) o varios de golpe. Nunca devuelve null.

### Ojo con el `delta` de Godot

`_Process(delta)` entrega el tiempo real del frame. Pásalo tal cual. El acumulador ya tiene
tope de recuperación (`MaxCatchUpTicks`, 5 por defecto): si la ventana se congela diez
segundos, corre cinco ticks y descarta el resto, en vez de disparar una avalancha que el
jugador percibe como un salto.

---

## 4 · Forma de los datos: `WorldSnapshot`

```csharp
public sealed record WorldSnapshot(
    int Tick,
    double SweepDegrees,          // ángulo actual del haz, [0, 360)
    bool SweepRunning,
    IReadOnlyList<ContactView> Contacts,
    IReadOnlyList<TorpedoView> Torpedoes,
    IReadOnlyList<EnemyTorpedoView> EnemyTorpedoes,
    IReadOnlyList<DefenseView> Defenses,
    int AmmoLoaded,
    int AmmoReserve,
    int DefensesAvailable,
    bool ReloadInProgress,
    int HitsTaken,                // 0, 1 o 2
    bool Destroyed);
```

### `ContactView` — lo único con gating

```csharp
public sealed record ContactView(
    int Id,
    Vec2 Position,          // ⚠ posición de la LECTURA, no la real
    int BearingDegrees,     // marcación de la lectura
    char BandLetter,        // banda de la lectura, o '-' si cae fuera de toda banda
    int AgeTicks);          // ticks transcurridos desde que el haz la registró
```

**`Position` es dónde el haz vio el contacto la última vez, no dónde está ahora.** Ese
desfase es el juego entero: extrapolar de la lectura vieja a la posición actual es la
habilidad que M1 está validando.

No existe forma de llegar a la posición real desde el snapshot. No es una convención de
buena conducta, es que el dato no viaja — hay un test (`SnapshotBuilderTests`) que falla si
alguien agrega un campo que lo filtre.

Corolarios para el dibujo:

- Un contacto que el haz todavía no barrió **no aparece en la lista**. No es que esté en
  posición cero: no está.
- `AgeTicks` sirve para atenuar la marca con la edad. La escala natural es una vuelta del
  haz: `SweepPeriodSeconds / TickDurationSeconds`. Sácala de la configuración y no la fijes
  a mano, o cambiar el período del barrido —la pregunta 4 de M1— desincroniza el desvanecido.
- No dibujes una traza con las posiciones sucesivas. Mostrarla convertiría la extrapolación
  en lectura pasiva y vaciaría la habilidad central (TDD §5.2). El cuaderno del jugador es
  de papel.
- **No le pongas la coordenada al punto.** Escribir `C047` al lado del contacto le entrega
  hecha justamente la lectura que M1 mide si es satisfactorio hacer. El jugador saca la
  marcación del anillo graduado y la banda de los anillos concéntricos. Por eso el anillo
  lleva números cada 5 grados y radiales punteadas cada 10: son la herramienta de lectura,
  no adorno.

### El resto: posición real, cada frame

```csharp
public sealed record TorpedoView(int Id, Vec2 Position, int BearingDegrees, double RunToRadius);
public sealed record EnemyTorpedoView(int Id, Vec2 Position);
public sealed record DefenseView(int Id, Vec2 Position);
```

- **Torpedo propio**: posición real. Es tuyo, sabes dónde está. `RunToRadius` es el radio
  donde detonará si no choca antes — vale la pena marcarlo, porque hace legible la decisión
  de banda.
- **Torpedo enemigo**: posición real. Se detecta por ruido, no por barrido.
- **Defensa**: posición real.

Que estas tres clases *no* tengan gating es una decisión de diseño (spec §4.1), no un
descuido. Las cuatro reglas de información son: contactos con gating, todo lo demás sin él.

---

## 5 · Conversión de coordenadas

El espacio de simulación es normalizado: el borde del scope está en radio **1.0** y el
submarino siempre en el origen `(0,0)`.

**Marcación 0 es el norte, o sea `(0, -1)`, y crece en sentido horario.** Coincide con el
eje Y hacia abajo de Godot, así que la conversión es directa:

```csharp
private const float Radius = 320f;   // píxeles del borde del scope

private static Vector2 ToScreen(Sonar.Core.Spatial.Vec2 v) =>
    new((float)v.X * Radius, (float)v.Y * Radius);

// Marcación (grados) a dirección unitaria en pantalla:
private static Vector2 BearingToDirection(float degrees)
{
    var rad = Mathf.DegToRad(degrees);
    return new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad));
}
```

Dos cosas que sorprenden y conviene tener presentes:

1. **El haz arranca en marcación 000 y gira horario**, así que la marcación 000 es la
   *última* en barrerse, no la primera. Un contacto justo al norte espera una vuelta entera
   antes de aparecer. Ya indujo a error una vez, y hay un test que lo fija
   (`BearingZero_IsTheLastBearingSwept_NotTheFirst`).
2. **La zona muerta** (radio 0,10 con los valores de trabajo) es donde ningún torpedo propio
   está armado. Vale la pena dibujarla, porque explica visualmente por qué un disparo a
   quemarropa no hace nada.

---

## 6 · Catálogo de eventos

Todos heredan de `GameEvent` y traen `int Tick`. Están en `Sonar.Core.Events`.

| Evento | Campos | Qué significa para el jugador |
|---|---|---|
| `TorpedoFired` | `TorpedoId`, `BearingDegrees`, `BandLetter` | Salió un torpedo |
| `TorpedoDetonated` | `Position`, `HitSomething` | Detonó: por fin de corrida o al interceptar |
| `ContactDestroyed` | `ContactId`, `Position`, `Cause` | Confirmación de destrucción |
| `NearMiss` | `ContactId`, `Distance` | **No mostrar en M1.** Es base de la pista 2 |
| `FireRejected` | `Reason` | `NoAmmo` o `Reloading` |
| `DefenseDeployed` | `DefenseId`, `Position` | Defensa puesta |
| `DefenseRejected` | `Reason` | `NoneAvailable` — ya hay una desplegada |
| `DefenseDetonated` | `DefenseId`, `Position`, `TargetsDestroyed` | La defensa se gastó. **Hay que mostrarlo** |
| `ReloadStarted` | — | Empezó la recarga |
| `ReloadCompleted` | `AmmoLoaded` | Terminó |
| `SonarToggled` | `Running` | Barrido activado o detenido |
| `StatusReported` | `AmmoLoaded`, `AmmoReserve`, `Defenses` | Respuesta a `STATUS` |
| `InputNotRecognized` | `RawText` | Texto que no es comando. **No es un error** |
| `EnemyTorpedoFired` | `TorpedoId`, `SourceContactId` | Ruido de lanzamiento enemigo |
| `EnemyTorpedoDestroyed` | `TorpedoId`, `Position`, `Cause` | Un torpedo enemigo murió |
| `SubHit` | `HitsTaken` | Te pegaron. A los 2 se acaba |
| `SweepCompleted` | `Revolution`, `ReadingCount` | El haz dio una vuelta. Latido del silencio |
| `ShotAssessed` | error angular, error radial, edad de lectura | **No mostrar.** Sólo telemetría |
| `SubDestroyed` | `Cause` | `Torpedoes` o `Collision` |

`DestructionCause`: `DirectHit`, `Blast`, `Defense`, `TorpedoCollision`.
`RejectionReason`: `NoAmmo`, `Reloading`, `NoneAvailable`.

### Munición: el enemigo juega con las mismas reglas

Tres tubos, y `RELOAD` carga **uno**. Vaciar el cargador cuesta tres recargas.

Los enemigos igual: un solo torpedo en el agua a la vez y el mismo tiempo de recarga. Con torpedos
lentos, el límite que manda es el primero — mientras su torpedo viaje, ese enemigo no dispara otro.

Tiene una consecuencia que conviene mostrar bien: interceptar un torpedo enemigo **libera a quien
lo lanzó** para disparar de nuevo. `EnemyTorpedoDestroyed` no es sólo una buena noticia.

### La defensa se gasta

`DefenseCapacity` limita cuántas defensas puede haber **desplegadas a la vez**, no cuántas quedan
en la corrida. Con el valor de trabajo en uno: pones una, y hasta que detone no puedes poner otra.

Por eso `DefenseDetonated` no es un evento decorativo. Es el momento en que el jugador vuelve a
quedar descubierto, y si no se muestra, la decisión de cuándo poner la siguiente desaparece.

### La confirmación de impacto no depende del barrido

`ContactDestroyed` y `SubHit` son ley del mundo: llegan siempre, aunque el haz esté en la
otra punta del círculo o detenido (spec §4.6). El jugador tiene que poder saber si acertó.
Es la única excepción al gating y es deliberada.

### Los cuatro registros

El spec §4.7 pide cuatro registros visuales distinguibles. En M1 existen los tres primeros;
el cuarto se reserva y **no se usa todavía**:

1. **Comando** — lo que el jugador escribió, tal cual, incluido lo que no se reconoció
2. **Instrumento** — lo que reporta el sistema: disparos, impactos, recargas
3. **Tripulación** — voz humana. Sin uso en M1
4. **Sin firmar** — *reservado.* Que esté vacío en M1 es el punto: cuando aparezca algo acá,
   tiene que significar algo. Si se usa antes de tiempo, deja de significar

---

## 7 · Arranque

```csharp
private const string ConfigPath = "res://../../config/simulation.working.json";

var json = FileAccess.GetFileAsString(ConfigPath);
var config = SimulationConfigLoader.LoadFromJson(json);

_controller = new GameController(config, seed: 20260811,
    course: new Bearing(0), speed: 0.05);

_controller.SetContacts(BuildOpeningContacts(config));
```

`SimulationConfig.Validate()` corre sola en el constructor y **lanza** si algún valor rompe
una invariante (por ejemplo, un radio de impacto menor al avance por tick de un torpedo, que
produciría tunneling). Es a propósito: un error de configuración debe reventar al arrancar,
no producir comportamiento raro media hora después.

La semilla fija hace la sesión reproducible. Cambiarla cambia el escenario.

### Contactos de apertura

Viven en `Sonar.Controller/OpeningScenario.cs`, no en el proyecto de Godot, para que los
tests ejerciten el escenario **real** en vez de una copia. Una copia se desincroniza y deja
de probar lo que se juega.

```csharp
_controller = new GameController(
    config, seed: 20260811, OpeningScenario.Course, OpeningScenario.SubSpeed);
_controller.SetContacts(OpeningScenario.Contacts(config));
```

Tres cosas de ese archivo importan más de lo que parece, y las tres se descubrieron
jugando, no razonando:

1. **`SubSpeed` fija cuánto dura un encuentro.** La deriva del mundo se suma a la velocidad
   de *todos* los contactos, así que las velocidades propias tienen que casi cancelarla. Con
   las del plan original, un enemigo caía encima del submarino en siete segundos.
2. **`FirstFireGraceTicks` retrasa el primer disparo enemigo.** Sin él el jugador moría en el
   tick 582 sin haber alcanzado a leer dos barridos, y M1 no puede medir si extrapolar es
   satisfactorio si nunca se llega a extrapolar.
3. **El pecio inerte no es decorado.** No tiene velocidad propia, así que recibe la deriva
   pura y cruza el scope de norte a sur. Sin una referencia que sí derive, la anomalía de
   las defensas no se puede leer.

El `WaveDirector` escalona los tiempos de disparo. Sin escalonar, los cuatro enemigos
dispararían en el mismo tick y la presión llegaría en oleadas sincronizadas en vez de
sostenida. Lo hacía una clase aparte, `ContactSpawner`, que el `WaveDirector` **nunca llegó a
llamar** — por eso el escalonamiento no ocurría en la demo aunque tuviera tests que pasaban.
Esa clase ya no existe: tener dos dueños del spawn era la causa del defecto.

Estos números son material de M1: la pregunta 3 se responde tocándolos y volviendo a jugar.
Lo pinchan `OpeningScenarioTests`, que fallan si el escenario se vuelve a degenerar.

---

## 8 · Comandos que el parser acepta

| Texto | Efecto |
|---|---|
| `C047`, `c47`, `FIRE C047`, `F C047` | Dispara a marcación 047, banda C |
| `DEF C047`, `DEFEND C047` | Despliega defensa ahí |
| `START` | Arranca la corrida. Antes de esto el mundo está detenido |
| `RELOAD` | Carga **un** torpedo. **No pausa el mundo**: el haz sigue girando |
| `RETRY` | Reinicia desde cero. Sólo con el submarino destruido |
| `SONAR` | Alterna el barrido |
| `STATUS` | Reporta munición y defensas |

Mayúsculas, minúsculas y espacios de sobra dan igual. Lo que **no** se acepta:

- `C400` y `C360` — marcación fuera de rango. **No se toma módulo**: tomarlo haría que un
  error de tipeo disparara silenciosamente a C040, y con munición limitada eso se paga caro
- `F047` — con cinco bandas, F no existe
- Cualquier otra cosa → `UnrecognizedInput`, que se registra y no hace nada

El número de bandas sale de la configuración, no está fijo en el parser.

**El mundo no corre hasta `START`.** No es un detalle de flujo: la demo tiene que poder abrirse
sin nadie al lado que la explique, y eso exige un momento con el reloj detenido en el que leer
las instrucciones. Antes de `START` los demás comandos se parsean pero **no se encolan** — uno
encolado se dispararía en el instante en que el jugador arranca. `RETRY` reinicia la corrida
pero **no** devuelve al instructivo.

**Un contacto enemigo que te alcance te mata de una**, sin importar los impactos previos. Los
pecios inertes no chocan.

---

## 9 · Verificación manual

### El sonar funciona

| Verificación | Esperado |
|---|---|
| El haz gira | Una línea recorre el círculo continuamente |
| Aparecen contactos | Marca con etiqueta `047 C` al pasar el haz |
| Las etiquetas envejecen | Se atenúan hasta que el haz vuelve a pasar |
| `C047` + Enter | Sale un torpedo por esa marcación |
| El torpedo detona | Marca en el punto de corrida, mensaje en el registro |
| `hola` | Aparece en el registro y no pasa nada más |
| `C400` | Igual: se registra y no hace nada |
| `RELOAD` | El HUD muestra recargando y el haz **sigue girando** |
| `SONAR` | El haz se congela; las etiquetas dejan de refrescarse |
| `DEF C090` | Marca de defensa en esa coordenada |
| Torpedos enemigos | Marcas que se acercan al centro |
| Dos impactos | El HUD muestra `2/2` y destruido, y todo se detiene |

### El gating es real — la que valida el diseño entero

1. Detén el barrido con `SONAR`
2. Observa una etiqueta de contacto
3. Espera diez segundos

**Esperado:** la etiqueta no se mueve, aunque el contacto sí se está moviendo por debajo. Al
reactivar el barrido con `SONAR`, la etiqueta salta a la posición nueva.

Si la etiqueta sigue al contacto en tiempo real, el gating se rompió. Lo más probable es que
alguien esté leyendo `Contact.Position` en vez de `ContactView.Position` — pero eso ya no
debería ser posible desde la escena, porque `Contact` no viaja en el snapshot.

### La anomalía se puede leer

Con el submarino avanzando (`speed: 0.05`), deja una defensa junto al contacto inerte y
mira un rato: el pecio se aleja hacia atrás y la defensa se queda contigo. **Es la mecánica,
no un bug** — no le agregues deriva a las defensas para "arreglarlo".

---

## 10 · La trampa del terminal

**El `LineEdit` deja de aceptar teclas después de Enter aunque conserve el foco.** Godot 4.4
separó el *modo edición* del foco: al enviar, el `LineEdit` sale del modo edición y el
jugador tiene que hacer clic para escribir el comando siguiente. Es un terminal, así que eso
lo rompe entero.

Lo confuso es el diagnóstico: `HasFocus()` devuelve `true`, `Editable` es `true`, y
`GuiGetFocusOwner()` apunta al `LineEdit`. Todo se ve bien y las teclas se pierden igual.

```csharp
// en _Ready
_input.KeepEditingOnTextSubmit = true;

// en _Process — las dos condiciones son distintas y hacen falta las dos:
// el foco se pierde al hacer clic fuera, el modo edición se pierde al enviar.
if (!_input.HasFocus()) _input.GrabFocus();
if (!_input.IsEditing()) _input.Edit();
```

Hay una verificación automática. Teclea dos comandos seguidos sin clic entremedio y devuelve
código de salida distinto de cero si el segundo se pierde:

```bash
godot-mono --path src/Sonar.Game -- --m1-selftest
```

Si reconstruyes la escena, córrela.

## 11 · Telemetría

La fuente de verdad es `user://m1-session.jsonl`, una línea JSON por evento, escrita
incrementalmente. `SessionRecorder` traduce el flujo de eventos; `TelemetryWriter` lo escribe.
`SessionMetrics` sigue existiendo y alimenta el CSV resumen.

Tres reglas que conviene no romper al rehacer la escena:

1. **`_recorder.Observe(evt)` va como primera línea del manejador de eventos.** Si se filtra
   por tipo antes, se pierden mediciones sin que nadie se entere.
2. **Se guardan eventos, no promedios.** De los eventos se derivan los agregados; al revés no.
3. **Nada personal.** Id anónimo, sin usuario, sin rutas, sin nombre de máquina. Y el juego
   dice en pantalla qué guarda, junto al instructivo.

`ShotAssessed` es la medición central y **no se dibuja**: mostrarla le entregaría al jugador
la corrección que el juego le pide calcular.

Se escribe incrementalmente y no al cerrar: un cierre forzado perdería la sesión entera, y con
oleadas y RETRY las sesiones se alargan.

## 12 · Lo que M1 no está midiendo

La escena de M1 es deliberadamente fea. M1 valida **si extrapolar lecturas de sonar es
satisfactorio**, no si se ve bien. Pulir la presentación antes de tener las ocho respuestas
anotadas en [m1-findings.md](m1-findings.md) es la señal más clara de haberse salido del
milestone.

Si la respuesta a la pregunta 1 resulta negativa, M1 está igualmente completo: cumplió su
función, que era avisar a tiempo y barato.
