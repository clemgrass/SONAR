# Piloto automático y la pared — diseño

Una corrida determinista del gauntlet, jugada por un bot, que se puede **mirar** en el juego
real y **afirmar** en un test. Sirve para contestar dónde está la pared de dificultad y para
que los planes 7 y 8 dejen de estar sin validar.

Notación del §70.6 del GDD: `[DECIDED]`, `[OPEN]`, `[ASSUMPTION]`, `[RISK]`.

---

## 1 · El problema

`docs/development/m1-findings.md` tiene dos `[OPEN]` que dicen lo mismo con distintas
palabras:

> **El baseline sólo habla de la oleada 1.** Sin input el único contacto nunca muere, así que
> la compuerta de "esperar a que se limpie" no deja entrar la oleada 2. […] Para medir eso
> hace falta jugar, **o un bot que dispare**.

> `[OPEN]` **El plan 8 está sin validar en su conjunto.** Sus reglas están probadas en tests
> unitarios con escenarios construidos a mano, pero ninguna se ha visto ocurrir en una partida.

Cinco contadores del plan 8 dan cero en las cinco semillas medidas, y la razón es correcta:
sin nadie que dispare, sólo entra la oleada 1, que es un solo STALKER. No hay compañero al que
tapar el carril, no hay fuego amigo posible, y el RAM no aparece hasta la oleada 4.

El corolario incómodo: **hoy nadie ha visto nunca las oleadas 2 a 4 de este juego.** Existen
en el código y en los tests unitarios, y en ningún otro lado.

## 2 · Qué se construye

Un piloto automático que juega el gauntlet leyendo lo mismo que lee un jugador, con un
presupuesto declarado de un comando cada `X` segundos, más un medidor que dice cuándo dejó
de dar abasto. Dos consumidores del mismo bucle:

- **Un test** que afirma la *forma* de la muerte, no sus números.
- **Un modo demo en Godot** donde la miras correr en el scope real, con los comandos del bot
  apareciendo en el terminal como si alguien los tecleara.

Los dos ejecutan el mismo código y producen la misma corrida. Lo que ves es lo que el test
afirma.

## 3 · Decisiones

| # | Decisión | Estado |
|---|---|---|
| 1 | El piloto es **perfecto con perilla de ritmo**: extrapola exacto y actúa apenas puede, limitado por un comando cada `X` s | `[DECIDED]` |
| 2 | Repertorio completo: `FIRE`, `RELOAD`, `DEF` | `[DECIDED]` |
| 3 | Se mira en **Godot**, reusando la presentación que ya existe | `[DECIDED]` |
| 4 | El test afirma un **invariante**; los números van a un reporte | `[DECIDED]` |
| 5 | El índice de saturación es **amenazas ÷ comandos** sobre una ventana de 30 s | `[DECIDED]` |
| 6 | El bot vive en un **proyecto propio**, `src/Sonar.Autopilot` | `[DECIDED]` |
| 7 | Sin `SONAR` en el repertorio: congelar el haz es herramienta de lectura humana | `[DECIDED]` |

### Por qué perfecto y no humano

Un bot humano simulado necesita tres números que hoy no están medidos —tiempo de composición,
error de puntería, lecturas perdidas— y produciría una corrida con tres ficciones adentro con
aspecto de medición. El piloto perfecto tiene una sola perilla declarada, y lo que mide es una
**cota superior**: ninguna persona va a superar esa pared. Si el juego ya es imposible para el
bot en la oleada 4, para una persona lo es antes.

### Por qué proyecto propio

El bot es un instrumento de medición. Meter el instrumento dentro de la capa que mide es cómo
se termina con un Controller que tiene una segunda personalidad. Separado, la dirección de
dependencias es greppable y el índice de saturación —que es una medición— no queda escondido
entre clases de producción, que es donde las mediciones se pudren sin que nadie lo note.

Precedente en contra, anotado porque es real: el `WaveDirector` vive en el Controller con el
argumento "es guion, no regla". La diferencia es que ese guion *es* la misión que juega la
persona; el bot es un aparato de laboratorio.

## 4 · Arquitectura

```
Sonar.Game (Godot)  ──▶  Sonar.Autopilot  ──▶  Sonar.Controller  ──▶  Sonar.Core
```

Una sola dirección, igual que antes. **`Sonar.Autopilot` no referencia Godot**, y el grep de
la Definition of Done lo cubre igual que a las otras dos capas.

| Pieza | Responsabilidad | Lo que no hace |
|---|---|---|
| `ContactTracker` | Recuerda lecturas por id, estima velocidad | No decide |
| `Autopilot` | `Decide(WorldSnapshot, tick) → string?` | No avanza el mundo ni mide |
| `SaturationMeter` | Cuenta amenazas y comandos, calcula el índice | No influye en las decisiones |
| `AutopilotRun` | El bucle: consulta, envía, avanza un tick | No sabe de táctica |

### La firma es el enforcement

`Decide` recibe un `WorldSnapshot` y nada más. No recibe el `GameController`, así que no
alcanza `LiveEnemyCount` ni ningún otro dato verdadero del mundo: el bot juega con lecturas
envejecidas y con gating, igual que el jugador.

Esto no es una convención que alguien pueda romper por descuido: **hacer trampa no compila.**
Es el mismo mecanismo por el que `ContactView` impide que la presentación dibuje la verdad.

### El bucle

```
StepOneTick():
    si tick >= próximoPermitido:
        texto = piloto.Decide(controller.Snapshot, tick)
        si texto != null:
            controller.SubmitInput(texto)
            emitir CommandEmitted(tick, texto)
            próximoPermitido = tick + X_ticks
    medidor.Observe(controller.Snapshot)
    antes = controller.Snapshot.Tick
    repetir controller.Advance(tickDuration) hasta que Snapshot.Tick > antes
```

El test lo llama hasta que el submarino muera. Godot lo llama `N` veces por `_Process`, con
`N` derivado del reloj de pared y de la velocidad de reproducción.

**Por qué los dos producen la misma corrida:** el bot decide por número de tick, no por frame.
Un frame perdido en Godot hace la simulación más lenta, nunca distinta.

**Por qué el `repetir`:** llamar `GameController.Advance` con exactamente `tickDuration`
ejecuta cero o un tick, nunca dos —el acumulador no puede juntar dos períodos con un solo
aporte de uno—, pero la deriva de punto flotante hace que de vez en cuando ejecute cero. Sin
el `repetir`, esa vez el bot decide dos veces sobre el mismo tick y las dos corridas divergen.

`START` lo envía el constructor de `AutopilotRun`. Arrancar una sesión no es táctica.

## 5 · El piloto

### Seguimiento

`ContactTracker` guarda las dos últimas lecturas distintas por id de contacto. Detecta lectura
nueva cuando `AgeTicks` baja, y anota `(tick − AgeTicks, posición)`. La velocidad estimada es
`Δposición / Δticks`.

Consecuencia deliberada: **el bot no dispara hasta tener dos barridos del blanco**, unos 14 s
desde la primera lectura. Sin velocidad, disparar es tirarle a donde el blanco estaba. Un
jugador está en la misma situación.

`[ASSUMPTION]` **El bot asocia lecturas por `Id`; una persona no puede.** `SonarView` no
rotula los contactos a propósito —el contrato dice que rotularlos entregaría hecha justo la
lectura que M1 mide—, así que una persona asocia por cercanía y a veces se confunde de blanco.
El bot nunca. Es parte de "piloto perfecto" y va declarado en el reporte.

Si más adelante se quiere la versión honesta, se cambia a asociación por vecino más cercano y
se comparan las dos corridas: la diferencia **es** el costo de confundir contactos.

### Solución de tiro

Un disparo es `(marcación, banda)`. El torpedo sale del borde de la zona muerta, vuela recto
por esa marcación y detona **en el centro de la banda**, no donde esté el blanco. Apuntar es
resolver un encuentro. Para cada banda `b`:

1. `t_b = (centro(b) − DeadZoneRadius) / TorpedoSpeed`
2. Extrapolar el blanco a `ahora + t_b`
3. Error = distancia entre esa posición y el punto de detonación, `marcación(p) × centro(b)`

Se elige la banda de menor error y se dispara sólo si el error `< BlastRadius`. Sin solución,
no dispara: no se gasta munición en una esperanza.

Con la configuración de hoy los vuelos son **2,1 / 6,3 / 10,5 / 14,7 / 18,8 s** para A…E — el
torpedo nace en el borde de la zona muerta, no en el origen, y por eso se resta. A la banda C
son 10,5 s, y un contacto cerrando a 0,015 —el techo, la mitad de `EnemyTorpedoSpeed`— recorre
0,157 en ese rato, contra un radio de explosión de 0,100. **La banda correcta no es donde está
el blanco: es donde va a estar.**

### Prioridad

Por radio extrapolado ascendente entre los que tienen solución: primero el que ya está encima.
El bot no puede priorizar "el que está por disparar" porque `NextFireTick` es verdad del mundo
y no llega al snapshot.

### Defensa

Los torpedos enemigos se ven completos en el snapshot: se oyen por ruido, no por barrido. Van
rectos al origen a `EnemyTorpedoSpeed` con marcación constante, y **no reciben la deriva del
mundo** —sólo los contactos la reciben—, así que una defensa quieta sobre esa marcación
intercepta seguro. La anomalía del §25 no ensucia esta cuenta.

El bot despliega contra el entrante más cercano cuando va a cruzar la banda B, en su
marcación, con la banda cuyo centro le queda por delante. Con `DefenseRadius` 0,070 contra un
error angular de redondeo de ~0,005, la intercepción está holgada.

**Restricción que impone el contrato:** una defensa mata los torpedos propios. Antes de
desplegar, el bot compara la marcación de la defensa contra la de cada torpedo propio en vuelo
y no la pone en su carril. Sacrificar un tiro propio para parar uno enemigo es mal negocio
cuando `RELOAD` cuesta un comando y tres segundos.

### Recarga

`RELOAD` cuando los tubos están vacíos, y también de forma oportunista: si en un turno de
presupuesto no hay solución de tiro y faltan proyectiles en los tubos, recarga.

Importa porque `RELOAD` mueve **un solo proyectil** (`room = Math.Min(1, …)`): llenar tres
tubos cuesta tres comandos y nueve segundos. Con presupuesto, recargar compite con disparar,
y ese costo aparece medido en vez de escondido. Es la pregunta 7 de M1.

### Comandos que no se emiten

El bot **nunca emite un comando que la simulación rechazaría**: no dispara con los tubos
vacíos ni con una recarga en curso, no recarga mientras hay una en curso o con la reserva
agotada, no despliega defensa si ya hay una en el agua.

No es cortesía: un comando rechazado no es una respuesta, y contarlo inflaría el denominador
del índice de saturación hasta hacerlo mentir. Con esta regla el ritmo de comandos queda
acotado por la mecánica —una recarga cada 180 ticks, tres tiros por cargador— y no por lo
rápido que el bot sea capaz de teclear. Es lo que hace que el índice siga significando algo
con `X = 0`.

### El presupuesto `X`

Un comando cada `X` segundos, contado en ticks. Corridas propuestas: **0** (techo físico del
diseño), **1,5 s** y **3,0 s**.

`[ASSUMPTION]` La pregunta 2 de M1 —tiempo medio por comando— está sin responder. `X` es una
perilla declarada: el reporte dice "bajo este supuesto", nunca "el jugador tarda esto".

## 6 · El índice de saturación

> **S = amenazas nuevas en los últimos 30 s ÷ comandos emitidos en los últimos 30 s**

- **Amenaza nueva**: un id de torpedo enemigo que aparece en `snapshot.EnemyTorpedoes` y no
  estaba antes.
- **Comando emitido**: uno que el bot efectivamente mandó. Ese número ya lleva adentro las dos
  esperas que importan —el presupuesto `X` y el tiempo que el barrido lo hace esperar por una
  lectura utilizable—, así que no hay que modelarlas: quedan medidas.

`S > 1` significa que llegan amenazas más rápido de lo que el bot produce respuestas. Eso es
"no seguir el ritmo", como número.

Dos honestidades sobre la fórmula:

- **Está sesgada a la baja, a propósito.** No todo comando atiende una amenaza: un `RELOAD`
  cuenta igual e infla el denominador, así que `S` se ve más bajo que la asfixia real. Si aun
  así cruza 1, cruzó de verdad.
- **La ventana de 30 s** son unos cuatro barridos y más de un ciclo de fuego enemigo
  (`EnemyFireIntervalTicks` son 25 s). Más corta salta con cada torpedo; más larga borra el
  momento en que la cosa se rompe.

Sin amenazas en la ventana, `S = 0`. Con amenazas y cero comandos, `S` es genuinamente enorme
y se reporta como tal: es el caso "me están matando y no alcanzo ni a teclear".

Elegida sobre una alternativa más completa —deuda de acciones, que mete munición y recarga en
la ecuación— porque esa necesita un modelo, y un modelo equivocado produce una pared falsa con
aspecto de dato. Ésta son dos conteos sobre la misma ventana y no tiene dónde esconder un
error.

## 7 · El test

El test de la pared vive junto a `GauntletTests`, que ya corre con la configuración real de
disco. Los unitarios de las cuatro piezas —extrapolación, elección de banda, carril propio,
aritmética del medidor— viven en `tests/Sonar.Autopilot.Tests`.

Tres afirmaciones, y las tres se controlan entre sí:

1. **El bot juega** — llegó al menos a la oleada 2 y mató al menos un contacto.
2. **Iba sobrado al principio** — el **máximo** de `S` mientras la oleada 1 estuvo en curso
   es `≤ 1`. El límite es no estricto a propósito: `S = 1` es exactamente "una respuesta por
   amenaza", o sea seguirle el ritmo. Quedarse atrás empieza estrictamente por encima de 1.
3. **Murió ahogado** — en la muerte, `S > 1` y subiendo: `S` de los últimos 30 s mayor que `S`
   de 60 s antes.

La 1 es el control positivo, y no es decorativa: **sin ella, un bot que no teclea nada pasa la
3 trivialmente** —muere ahogado en la oleada 1 con `S` enorme— y el test lo celebraría. Es
exactamente la forma de test vacuo que ya se coló dos veces en este repo.

La 2 y la 3 se controlan mutuamente: un medidor que devuelva siempre un número alto rompe la
2; uno que devuelva siempre cero rompe la 3.

**Ningún número exacto se afirma.** Ni el tick de muerte, ni la oleada, ni el valor de `S`.
M1 es un milestone de tuning: un test que afirme "muere en el tick 8 943" se pone rojo cada vez
que se hace el trabajo del milestone, y un test que se pone rojo por trabajo legítimo se
termina ajustando hasta que pase. Lo que no debe sobrevivir a un cambio de tuning es que el
bot deje de jugar.

### Verificación por mutación

Se rompe la implementación a propósito y se cuenta qué cae, antes de dar la propiedad por
cerrada:

| Mutación | Debe fallar |
|---|---|
| `Decide` devuelve siempre `null` | 1, y la 2 por cero comandos |
| Extrapolación apagada: apunta a la última lectura | 1 — sin adelantar el blanco no le pega a nada |
| `SaturationMeter` devuelve una constante alta | 2 |
| `SaturationMeter` devuelve cero | 3 |
| El bot nunca despliega `DEF` | Ninguna — y eso también es dato |

La última fila es la interesante. Si quitar la defensa no mueve la pared, la defensa no está
haciendo nada, y eso es un hallazgo de M1 y no un defecto del test.

## 8 · El reporte

Ninguno de los dos canales escribe en el repositorio.

**El test** imprime por `ITestOutputHelper`: cabecera con semilla, `X` y configuración; una
fila por oleada con ticks, enemigos vivos al cerrar, disparos, muertes, amenazas, comandos y
`S`; y la línea de muerte con tick, causa, oleada y `S`.

**El modo demo** reusa el `TelemetryWriter` que ya existe y escribe a `user://m1-session.jsonl`
con una línea `pilot` que declara bot y `X`. Así las corridas del bot entran al mismo corpus
que las de los testers y **el análisis de las preguntas 3, 7 y 8 sirve tal cual, sin
herramienta nueva**.

## 9 · El modo demo

```bash
make demo
# godot-mono --path src/Sonar.Game -- --demo --seed=20260811 --pace=1.5 --speed=1
```

| Bandera | Qué hace | Default |
|---|---|---|
| `--demo` | Enciende el piloto. Sin ella el juego es idéntico a hoy | apagado |
| `--seed=N` | Semilla de la corrida | `20260811` |
| `--pace=X` | Segundos por comando | `1.5` |
| `--speed=K` | Multiplicador de reproducción; `8` para llegar rápido a la oleada 4 | `1` |

El patrón de banderas ya existe: `--m1-selftest` lee `OS.GetCmdlineUserArgs()` igual.

### Cableado en `Main.cs`

1. Leer las banderas y, con `--demo`, construir `Autopilot` y `AutopilotRun` sobre el
   `GameController` que ya se crea ahí.
2. Cambiar quién avanza el mundo: hoy `_Process` llama `_controller.Advance(delta)` una vez;
   en modo demo llama `run.StepOneTick()` `N` veces, con `N = speed × delta / tickDuration` y
   un tope para que un frame perdido no cuelgue la ventana. Fuera de modo demo, nada cambia.
3. Enganchar `CommandEmitted` al mismo camino que recorre un comando tecleado: `> C047` en el
   registro, `_recorder.Command(…)`, `_metrics.NoteSubmit()`. Los comandos del bot aparecen en
   el terminal como si alguien los escribiera, y la telemetría los registra igual.
4. Al morir, detener el bucle e imprimir el reporte con `GD.Print`, dejando el scope congelado
   en el instante de la muerte.

### Límite de rol

El §7 del contrato dice que el diseño visual es de la persona. **No se toca `SonarView.cs` ni
el layout de `Main.tscn`.** Lo que se deja accesible desde `Main` es el dato:

| Dato | Tipo | Para qué serviría dibujarlo |
|---|---|---|
| `run.Saturation` | `double`, actualizado cada tick | Ver el índice cruzar 1 antes de la muerte es la respuesta que se busca |
| `run.LastCommand` | `string?` | Que el `LineEdit` muestre lo que el bot "teclea" |
| `run.Report` | tabla por oleada | Un panel de cierre al morir, si se lo quiere en pantalla |

`[DECIDED]` El campo de comandos se deja como está —`_Process` le sigue robando el foco—, así
que se puede interrumpir al bot tecleando encima. Cambiarlo a sólo lectura es decisión de
diseño visual, no de cableado.

## 10 · Lo que no se hace

- **Visor web con scrub.** Sería un segundo renderer, o sea una segunda verdad visual que
  puede discrepar del juego, y además es código de frontend. Se descartó a favor de mirar la
  corrida en el juego real.
- **`SONAR` en el repertorio.** Congelar el haz sirve para que una persona lea con calma una
  coordenada. Un bot que calcula al instante no gana nada y sólo deja de recibir lecturas.
- **Modelar la anomalía de las defensas.** Es un misterio del jugador, no un parámetro de
  puntería. El bot despliega a corto plazo y no la compensa.
- **Afirmar números.** Ver §7.
- **Un piloto humano simulado.** Ver §3.

## 11 · Riesgos

`[RISK]` **El bot podría no llegar a la oleada 2.** Si el techo de velocidad de cierre y los
tiempos de vuelo hacen que un solo STALKER sobreviva a un piloto perfecto, la afirmación 1
falla y el test se pone rojo el primer día. Eso **no es un defecto del plan: es el hallazgo**.
Significaría que la oleada 1 ya es una pared, y hay que anotarlo en `m1-findings.md` y ajustar
tuning antes de seguir.

`[RISK]` **La bimodalidad de la oleada 1 puede tragarse la corrida.** Las cinco semillas
medidas en el plan 7 muestran dos regímenes: o el STALKER acierta sus dos primeros tiros y mata
en 87 s, o falla y la partida se estira a seis minutos. Un bot que se defiende debería romper
esa moneda, pero si no lo hace, la corrida "que llega a la pared" depende de la semilla. Se
mitiga corriendo el test sobre las mismas cinco semillas del plan 7 y reportando las cinco.

`[OPEN]` **Cuál es la semilla que se mira.** La demo arranca con `20260811`, la del juego. Cuál
es la que mejor muestra la pared se decide con el reporte de las cinco en la mano, no antes.

## 12 · Definition of Done

1. `project-state.yaml` actualizado — primer paso, no último.
2. `grep -rn "^using Godot" src/Sonar.Core/ src/Sonar.Controller/ src/Sonar.Autopilot/` vacío.
3. Suite completa en verde sobre `Sonar.sln`.
4. Verificación por mutación ejecutada, con el conteo de tests caídos anotado en el reporte de
   la tarea.
5. `make demo` corre y se ve la corrida terminar en muerte.
6. El reporte de las cinco semillas anotado en `docs/development/m1-findings.md`, con `X`
   declarado.
7. `docs/development/godot-wiring.md` documenta el contrato del modo demo.
