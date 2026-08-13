# Demo de gameplay M1 — la guardia, el gauntlet y la telemetría

Fecha: 2026-08-13 · Estado: aprobado en diseño, pendiente de plan de implementación

Segundo spec del proyecto. El primero
([2026-08-11](2026-08-11-sonar-preproduction-design.md)) decidió qué es el juego antes de
escribir código. Este decide qué le falta a la **demo de gameplay** de M1 para responder sus
ocho preguntas con datos en vez de sensaciones, y para acercarse a lo que el jugador quiere
que sea el juego: un LARP simplificado de operar un sonar.

Notación del §70.6 del GDD: `[DECIDED]`, `[OPEN]`, `[ASSUMPTION]`, `[RISK]`.

---

## 1. Contexto

La demo de gameplay funciona: el sonar barre, las lecturas envejecen, los torpedos corren
diez segundos, el terminal acepta comandos. Dos observaciones del playtest la mandaron a
revisión:

1. **No alcanza a leerse el historial.** El registro es la superficie donde va a vivir la
   historia, y la demo lo trata como salida de depuración.
2. **Se siente como un arcade abstracto**, no como un puesto de trabajo.

El diagnóstico es el mismo por los dos lados. El §30 del GDD define una tripulación de cuatro
personas cuyos identificadores aparecen en el historial; el §22 lista *esperar* como acción
del jugador; el §8 dedica cuatro de sus catorce pasos a la tripulación hablando antes del
primer disparo. **Nada de eso está en la demo**, porque M1 se acotó a la pregunta del sonar y
lo demás se cortó. La única voz en la sala es el instrumento.

`[DECIDED]` **La tripulación sigue fuera de M1.** Es contenido y diálogo, con el `[RISK]` de
idioma del spec §4.8 encima, y M1 no lo necesita para responder sus preguntas.

`[ASSUMPTION]` Con eso fuera, el techo de la demo es una **guardia de instrumento en
silencio**: solitaria y tensa, pero no coral. El salto a LARP completo ocurre cuando entre la
tripulación al juego real, y va a ser mayor que el que damos acá.

---

## 2. Alcance

Dentro:

- **A · La guardia** — reloj de misión, historial pausado, vueltas del haz registradas
- **B · El gauntlet** — oleadas progresivas 1 → 2 → 4 → 8 → …
- **C · Colisión** — un contacto enemigo que te toque mata de una
- **D · RETRY** — reinicio puro con spawns distintos
- **E · Telemetría** — registro de eventos apto para veinte testers externos

Fuera: tripulación, pistas, narrativa, fase 2, sistema de daño, audio, diseño visual
definitivo.

`[DECIDED]` **Sin pecio inerte en esta demo.** Un contacto que no es amenaza agrega una
variable que ensucia la medición de la pregunta 3, y sin rótulos en los puntos se lee más como
un bug que como una decisión. La anomalía del §25 se valida aparte, no acá.

> Esto deja `DefenseAnomalyTests` y la exención de contactos inertes de §4.4 del TDD **sin
> ejercitarse en la demo**. Los tests siguen pasando y la regla sigue en Core: lo que sale es
> el pecio del escenario, no la mecánica.

---

## 3. A · La guardia

### 3.1 Reloj de misión

`[DECIDED]` El HUD deja de mostrar `tick 748` y muestra `00:12:28`. Es el mismo número
—`Tick × TickDurationSeconds`— contado como tiempo en el puesto en vez de como contador de
depuración. Cambiar la unidad cambia lo que el número significa para quien lo lee.

El tick sigue existiendo en la telemetría, donde sí es la unidad correcta.

### 3.2 Historial pausado

`[DECIDED]` Las líneas del registro se encolan y se sueltan **de a una cada 250 ms**, en vez
de volcarse enteras en el frame en que ocurren.

`[DECIDED]` **Excepción: los impactos sobre el propio submarino se saltan la cola** y aparecen
de inmediato. Es corolario del §4.6 del spec anterior — la confirmación de impacto es ley del
mundo y no depende de nada. El resto puede llegar tarde; que te peguen, no.

`[DECIDED]` Si la cola supera **8 líneas** pendientes, el intervalo baja a 80 ms hasta
drenarla. Sin esto, en la oleada 8 el registro se atrasaría minutos y dejaría de describir lo
que está pasando.

`[ASSUMPTION]` 250 ms es legible sin ser lento. Es un valor a medir, no una certeza.

### 3.3 Vueltas del haz

`[DECIDED]` Cada vuelta completa del barrido emite un evento nuevo, `SweepCompleted`, y el
registro lo muestra como una línea con el número de vuelta y cuántos contactos hay en la
lista de lecturas.

No es una mecánica: no cambia ningún estado ni habilita ninguna acción. Es un latido. Le da al
silencio una unidad contable, y "eso fue hace dos barridos" se vuelve una frase que el jugador
puede pensar.

---

## 4. B · El gauntlet

### 4.1 Forma

`[DECIDED]` Progresión dobladora, cada oleada empieza cuando la anterior queda limpia:

| Momento | Contactos | Propósito |
|---|---|---|
| 0–10 s | 0 | El jugador se orienta. Silencio deliberado |
| Oleada 1 | 1, casi quieto | Aprender a disparar. Se puede fallar y volver a intentar |
| Oleada 2 | 2 | Primera vez que hay que elegir a cuál |
| Oleada 3 | 4 | La munición empieza a apretar |
| Oleada 4 | 8 | |
| Oleada *n* | 2ⁿ⁻¹ | Hasta que muera |

`[ASSUMPTION]` **La oleada 4 es probablemente invencible** con tres torpedos, recarga de a uno
y corridas de diez segundos. No es un defecto: el gauntlet existe para terminar, y en qué
oleada termina **es** la respuesta a la pregunta 3 de M1.

Consecuencia de diseño que conviene tener presente: la demo es un *cuánto aguantas*, no un *lo
completaste*.

### 4.2 Geometría de aparición

`[DECIDED]` Los contactos **entran por el borde del scope** (radio 1.0), nunca aparecen en el
medio. Un contacto que se materializa dentro del alcance rompe la ficción de que el sonar
detecta lo que llega.

`[DECIDED]` Marcación de entrada al azar, con **separación angular mínima** entre los
contactos de una misma oleada. Sin ese mínimo, dos entradas cercanas se leen como una sola
marca y la oleada mide menos de lo que dice medir.

`[OPEN]` Valor de la separación mínima. Punto de partida: `360 / (2 × contactos)` grados,
acotado a un mínimo absoluto — con 8 contactos eso da 22°, que a ojo se distingue en el anillo
graduado. → Ajustar con datos.

### 4.3 Movimiento

`[DECIDED]` Los contactos **cierran hacia el submarino**. Es lo que convierte no disparar en
una consecuencia y no en una espera.

`[DECIDED]` La velocidad de cierre **sube por oleada**. La oleada 1 casi no se mueve, para que
el jugador lento igual alcance a disparar.

`[OPEN]` Curva exacta de velocidad por oleada. → La telemetría del §7 la responde.

> Ojo con la deriva del mundo: se suma a la velocidad propia de todos los contactos. Ya
> produjo un escenario degenerado una vez (todo lo que entraba por el norte caía encima del
> submarino en siete segundos). Las velocidades se especifican como **velocidad de cierre
> neta deseada**, y el director resta la deriva al construir el contacto.
>
> El guardián contra esa degeneración vive hoy en
> `OpeningScenarioTests.ContactsDoNotPileOntoTheSub`. Al reemplazar `OpeningScenario` por el
> `WaveDirector` hay que **moverlo, no borrarlo**: con colisión mortal el fallo pasa de
> "escenario aburrido" a "muerte instantánea sin haber jugado".

---

## 5. C · Colisión

`[DECIDED]` Un contacto **enemigo** que llegue a `config.SubHitRadius` del origen destruye el
submarino de inmediato, sin importar los impactos previos.

`[ASSUMPTION]` Se reutiliza `SubHitRadius` en vez de agregar un radio de embestida propio. Un
submarino es más grande que un torpedo, así que en rigor debería ser mayor; se empieza
reutilizando para no agregar superficie de configuración, y se separa sólo si al jugarlo las
colisiones se sienten injustas por tardías.

`[DECIDED]` Los contactos **inertes no chocan**. Misma lógica que la exención de las defensas:
un pecio ya está hundido, y una muerte por chocar con terreno es barata.

`[DECIDED]` `SubDestroyed` gana una causa: `Torpedoes` o `Collision`. La telemetría necesita
distinguirlas, y el jugador también — morir por descuido de rumbo y morir bajo fuego son dos
lecciones distintas.

Esto además tapa un hueco que existe hoy: un contacto puede derivar encima del submarino sin
consecuencia. Se detectó jugando, no se había reportado como defecto porque no estaba en el
diseño.

---

## 6. D · RETRY

`[DECIDED]` Con el submarino destruido, `RETRY` reinicia. El §4.4 del spec anterior ya lo
decidió como **reset puro**: no persiste nada.

`[DECIDED]` Cada intento usa la **siguiente semilla de una secuencia derivada de la semilla de
sesión**. Los spawns cambian entre intentos, y la sesión completa sigue siendo reproducible
anotando la semilla base y el número de intento.

El determinismo no se pierde: se parametriza. La promesa sigue siendo *misma semilla → mismo
resultado*.

> Los enemigos de la demo son contactos ordinarios, no pistas. La reproducibilidad que el
> spec anterior exigía es la de las **pistas**, que en fase 2 ocurren en puntos fijos. Variar
> enemigos no la toca.

`[DECIDED]` **Primer consumidor real de `IRandomSource`.** El plan 3 dejó anotado que el test
de "semillas distintas producen resultados distintos" no se podía escribir todavía y que no
debía forzarse inventando aleatoriedad. Ese momento llegó: ahora se escribe.

---

## 7. E · Telemetría

Contexto: **veinte personas van a probar esto y mandar sus datos.** Eso cambia el problema de
"medir mis partidas" a "juntar veinte sesiones y sacar conclusiones".

### 7.1 Dos decisiones que gobiernan el resto

`[DECIDED]` **Guardar eventos, no promedios.** De los eventos se derivan los agregados; al
revés no. El CSV actual guarda cinco números y todo lo demás ya se perdió.

`[DECIDED]` **La corrida es reproducible, así que semilla + comandos con su tick reconstruyen
la sesión completa.** Es el activo más valioso que tenemos y sale gratis del trabajo de
determinismo: permite re-ejecutar la partida de cualquier tester y mirarla. Las preguntas que
todavía no se le ocurren a nadie se contestan después, sobre los mismos datos.

### 7.2 La métrica que responde la pregunta 1

`[DECIDED]` En cada disparo se registra el **error angular contra el contacto vivo más
cercano** y la **edad de la lectura** de ese contacto en ese momento.

Con esos dos números por disparo sale la **curva de extrapolación**: cuánto se degrada la
precisión conforme la lectura envejece.

| Lo que muestra la curva | Lo que significa |
|---|---|
| El error baja a lo largo de la sesión | La habilidad existe y se aprende → pregunta 1 en sí |
| El error es plano contra la edad | Nadie extrapola: disparan donde vieron la marca |
| El error explota pasados N segundos | Ahí está el techo del período de barrido → pregunta 4 |

Es mucho mejor proxy que la tasa de acierto, que mezcla puntería, suerte y elección de banda.

`[DECIDED]` El error lo calcula **`Sonar.Core`**, no la capa de presentación. Requiere la
posición **real** del contacto, que el snapshot esconde a propósito; calcularlo arriba
obligaría a filtrar la verdad hacia la presentación y rompería la defensa central del diseño.

Se emite como evento propio, `ShotAssessed`, marcado **sólo telemetría** en el documento de
cableado — igual que `NearMiss`. No se dibuja.

### 7.3 Catálogo

**Cabecera de sesión** — sin esto nada es comparable entre testers.
Versión de esquema, id anónimo de sesión, versión del juego, **el `SimulationConfig`
completo**, parámetros del gauntlet, resolución de pantalla.

**Por intento** — semilla, oleada alcanzada, duración, causa de muerte, munición y defensa al
morir, contactos vivos al morir.

**Por oleada** *(pregunta 3)* — número, cuántos entraron, tiempo hasta limpiarla, disparos,
muertes, torpedos gastados sin matar, casi-impactos con su distancia.

**Por comando** *(preguntas 2 y 6)* — tick, **texto crudo tal como se escribió**, si se
reconoció, tiempo de composición. Los typos son dato: si medio mundo escribe `C47` en vez de
`C047`, eso es la pregunta 6 contestándose sola.

**Por disparo** *(pregunta 1)* — coordenada, error angular, edad de la lectura, tiempo de
vuelo, desenlace (impacto directo / explosión / casi-impacto con distancia / agua).

**Por uso de sistema** *(pregunta 7)* — cuándo recargan (¿preventivo o sólo en cero?), cuántas
veces piden `STATUS` —pedirlo mucho significa que el HUD no comunica—, cuándo despliegan la
defensa y si sirvió.

**Silencios** *(ritmo)* — distribución de huecos entre comandos, y en particular **si escriben
algo durante los diez segundos iniciales**. Si teclean ahí, el silencio se está leyendo como
que el juego está roto.

### 7.4 Forma

`[DECIDED]` **JSONL**, una línea por evento. Los eventos tienen campos distintos entre sí y
forzarlos a columnas fijas los mutila. Se mantiene el CSV resumen para mirar rápido, pero la
fuente de verdad es el JSONL.

`[DECIDED]` **Escritura incremental**, no sólo al cerrar. Hoy un cierre forzado pierde la
sesión entera, y con oleadas y RETRY las sesiones se alargan.

`[DECIDED]` **Nada personal**: id anónimo por sesión, sin nombre de usuario, sin rutas, sin
nombre de máquina.

`[DECIDED]` El juego **dice en pantalla qué guarda**. Es barato y es lo correcto si el archivo
va a circular a veinte personas.

---

## 8. Dónde vive cada cosa

La regla de capas no se toca: `Sonar.Game → Sonar.Controller → Sonar.Core`, sin vuelta.

| Pieza | Capa | Por qué ahí |
|---|---|---|
| Colisión, `SweepCompleted`, `ShotAssessed` | **Core** | Son reglas y medidas sobre la verdad |
| `WaveDirector` | **Controller** | Es guion de misión, no regla. Como `OpeningScenario` |
| `RETRY` / reinicio | **Controller** | Reconstruye la `Simulation` con la semilla siguiente |
| Reloj, cola del historial | **Game** | Presentación pura |
| Escritor de telemetría | **Game** | Necesita sistema de archivos y reloj de pared |

`[DECIDED]` El `WaveDirector` recibe su propio `IRandomSource`. La simulación no se entera de
que hay oleadas: recibe contactos, como siempre.

---

## 9. Qué queda desactualizado

- **TDD §4.6** — la IA enemiga gana la regla de colisión
- **TDD §4.7** — `SubDestroyed` gana causa
- **TDD §5.1** — el barrido emite `SweepCompleted`
- **TDD §10.1** — el test de semillas distintas ya se puede escribir
- **godot-wiring §6** — tres eventos nuevos; `ShotAssessed` marcado sólo telemetría
- **m1-findings.md** — la sección "cómo se mide" se reescribe contra el JSONL
- **`OpeningScenario`** — lo reemplaza el `WaveDirector`; el pecio sale
- **`OpeningScenarioTests`** — se reescriben contra el `WaveDirector`. Los siete tests siguen
  valiendo casi todos: la ventana tranquila inicial, que los contactos no se peguen al
  submarino, que el terminal dispare, que un typo no haga nada, que el barrido detenido
  congele las etiquetas. El del pecio derivando es el único que se cae con el pecio

Por tamaño, esto es **dos planes de implementación**, no uno: reglas y gauntlet primero
(Core + Controller, todo testeable sin abrir Godot), presentación y telemetría después.

---

## 10. Riesgos

`[RISK]` **La demo mide una cosa distinta de la que medía.** El gauntlet dobla contactos hasta
matarte; eso responde la pregunta 3 muy bien y presiona la 7 y la 8, pero da **menos** tiempo
tranquilo para observar y extrapolar, que es la pregunta 1. Los diez segundos iniciales y la
oleada 1 casi quieta son la mitigación. Vigilar que la pregunta 1 no quede sin datos limpios.

`[RISK]` **Crece el alcance de M1.** Director de oleadas, RETRY, colisión, primer uso del RNG
y un sistema de telemetría completo. Se justifica porque es el criterio de salida del
milestone, pero es trabajo real y no un ajuste de números.

`[RISK]` **La retícula graduada cada 5° no sobrevive al 3D.** Cuando el sonar pase a ser una
textura dentro de una sala, leída en ángulo y sobre un CRT curvo, esa graduación hay que
rediseñarla. Mejor saberlo antes de modelar la consola.

---

## 11. Nota sobre el 3D

No hay nada que preparar. `WorldSnapshot` no sabe si lo dibuja un `Node2D` o una textura en
una sala 3D, y el flujo de eventos es exactamente lo que se necesita para las luces:
`TorpedoFired` bajando una luz es una línea en el manejador que ya existe.

El `SonarView` actual pasa a ser un `SubViewport` sobre un plano y no cambia nada más. La
separación de capas ya pagó por esto.
