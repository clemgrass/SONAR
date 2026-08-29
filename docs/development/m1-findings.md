# M1 — Hallazgos

M1 termina cuando las ocho preguntas tienen respuesta anotada aquí y los números están en
`config/simulation.working.json`. **No termina cuando "se siente bien"** — eso no es un
criterio, es una sensación, y con ella el milestone se estira indefinidamente.

Si la respuesta a la pregunta 1 es negativa, el milestone está igualmente completo: cumplió
su función, que era avisar a tiempo.

| # | Pregunta | Respuesta | Evidencia |
|---|---|---|---|
| 1 | ¿Extrapolar lecturas es satisfactorio? | | `errDeg` contra `ageTicks` en el JSONL |
| 2 | Tiempo medio por comando | | `compose` en el JSONL |
| 3 | Máximo de contactos jugable | | |
| 4 | Período del barrido | | probado 3,5 / 5 / 7 s |
| 5 | Número de bandas y radios | | probado 5 y 8 |
| 6 | ¿Hace falta repetir y ajustar? | | |
| 7 | ¿La munición aprieta? | | |
| 8 | ¿La fase extrema se dispara o se sobrevive? | | |

## Cómo se mide

La fuente de verdad es `user://m1-session.jsonl`: una línea JSON por evento, escrita
incrementalmente. `user://m1-session.csv` es un resumen para mirar de reojo, no para analizar.

En Linux los dos quedan en `~/.local/share/godot/app_userdata/Sonar/`.

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

**La sesión es reproducible.** Con la semilla de la línea `run` y los comandos con su tick se
puede re-ejecutar la partida de cualquier tester y mirarla. Las preguntas que hoy no se le
ocurren a nadie se contestan después sobre los mismos datos.

**Cabecera de sesión.** La línea `session` lleva el `SimulationConfig` completo. Sin eso, dos
testers con tuning distinto producen datos que no se pueden juntar.

**Silencios.** Cada línea `cmd` trae `gapTicks`, el hueco desde el comando anterior. Si la
gente teclea durante los diez segundos iniciales en que no pasa nada, el silencio se está
leyendo como que el juego está roto.

**Nada personal.** Id anónimo por sesión, sin nombre de usuario, sin rutas, sin nombre de
máquina. El juego lo dice en pantalla al abrirse.

## Decisiones que M1 puede invalidar

M1 tiene autoridad para revertir decisiones del spec. Si algo de esto aparece, anótalo aquí
y vuelve al spec antes de escribir narrativa:

- [ ] La extrapolación se siente como aritmética, no como pilotear un instrumento
- [ ] El tiempo por comando no cabe en el ritmo del combate
- [ ] El gating por clase hace el bullet hell ingobernable
- [ ] La banda como ventana no produce decisiones interesantes

## Notas de sesión

<!-- Una entrada por sesión: fecha, qué valores se probaron, qué se observó. -->

### 2026-08-14 — nerfeo enemigo

**Observado en juego:** stunlock de misiles. Los enemigos disparaban al mismo ritmo que el
jugador y no quedaba ventana para calcular un disparo propio; el turno entero era esquivar.

**Cambios:**

| Perilla | Antes | Ahora |
|---|---|---|
| Recarga enemiga | `reloadTicks` (420) | `enemyReloadTicks` 840 — número propio, el doble |
| Munición enemiga | uno en el agua | uno en el agua **y** uno de cargador: disparar vacía |
| Techo de cierre | sin techo | la mitad de `enemyTorpedoSpeed` (0.015) |

**Medido, no supuesto.** El nerfeo de recarga no hace nada mientras el enemigo esté lejos: de
la banda C hacia afuera el ciclo lo fija el vuelo del torpedo, no la recarga. Vale el doble
completo de banda C hacia adentro. Tabla de ciclos por radio en el TDD, §IA enemiga.

Consecuencia directa: **el baseline sin input no se movió** (muere en el tick 4577 contra 4578).
La oleada 1 tiene un solo enemigo lejano, y ahí el vuelo siempre mandó. Si al jugar la asfixia
sigue apareciendo temprano, la perilla que queda es `enemyFireIntervalTicks` (cuándo se arma un
contacto recién entrado) o bajar `enemyTorpedoSpeed`, no la recarga.

El techo de cierre recorta sólo de la oleada 3 en adelante — antes la escalada cruzaba la
velocidad del propio torpedo cerca de la oleada 5 y los cascos alcanzaban a sus municiones.
Las oleadas 1 y 2 quedan idénticas, que son las que enseñan a jugar.

- [ ] Pendiente de playtest: ¿la ventana entre disparos enemigos alcanza para componer un tiro?

### 2026-08-16 — plan 7: la mente del enemigo

Once tareas. El enemigo pasa de "apunta al origen y dispara" a tener personalidad como dato,
oír al jugador en vez de saberlo siempre, apuntar a una estimación envejecida, gobernar su
movimiento hacia un radio preferido y dudar antes de tirar. 336 tests.

**El baseline sin input se movió mucho, y es bimodal.** Cinco semillas, jugando la demo tal
como se envía y sin escribir un solo comando después de START:

| Semilla | Muerte | Disparos enemigos | Impactos | Acierto |
|---|---|---|---|---|
| 20260811 | 365.4 s | 6 | 2 | 33% |
| 11111111 | 301.2 s | 5 | 2 | 40% |
| 22222222 | **87.2 s** | 2 | 2 | **100%** |
| 33333333 | 365.3 s | 6 | 2 | 33% |
| 44444444 | **87.8 s** | 2 | 2 | **100%** |

Antes del rediseño la muerte era en el tick 4577 (76 s), siempre. Ahora va de 87 a 365
segundos, y **no hay término medio**: o el STALKER acierta sus dos primeros tiros y mata en
87 s, o falla y la partida se estira a cinco o seis minutos. La media de acierto es 61%,
cerca del ~50% que predijo el spec §12, pero la media esconde lo que pasa: no es una moneda
por disparo, son dos regímenes.

`[OPEN]` Esa bimodalidad es lo primero que hay que mirar en el playtest. Con un solo contacto
puede ser correcta —el error de puntería depende de la geometría del spawn, que la semilla
fija— pero si se mantiene con varios enemigos, la partida se decide en los primeros veinte
segundos y eso sí sería un problema.

**El baseline sólo habla de la oleada 1.** Sin input el único contacto nunca muere, así que
la compuerta de "esperar a que se limpie" no deja entrar la oleada 2. No dice nada sobre
2, 3 ni 4, que es justo donde vive la queja original de la andanada simultánea. Para medir
eso hace falta jugar, o un bot que dispare.

**El primer disparo enemigo cae siempre en el tick 2100.** Para una oleada de un contacto no
hay azar en esa cuenta: `spawnTick 600 + EnemyFireIntervalTicks 1500 + 0`. La variación entra
después, por la duda y por la geometría.

**Nueve defectos, los nueve del plan y ninguno de la ejecución.** Vale anotar la forma que
comparten, porque se va a repetir: todos compilaban y pasaban los tests. Eran correctos por
accidente, sostenidos por los valores de hoy.

Los dos que más enseñan:

- Un test llamado `SilenceLetsTheFixGoStale` afirmaba lo correcto y **no probaba nada**:
  capturaba el valor a los 31 ticks cuando una vuelta del haz tarda 210, así que comparaba el
  cero del constructor contra sí mismo. Lo delató anular el método entero y contar cuántos
  tests caían — tres de cuatro. El que quedaba en pie era el mentiroso.
- `FiringRadius` se escribía, se poblaba y se testeaba, y **nadie lo leía**. Eso dejaba al RAM
  disparando torpedos cuando su diseño entero es no disparar, y al STALKER tirando desde el
  borde en vez de cerrar primero. Los tests no podían verlo: afirmaban que el dato era
  correcto, no que alguien lo usara.

- [ ] Pendiente de playtest: ¿la bimodalidad se mantiene con varios enemigos?
- [ ] Pendiente de playtest: ¿se distinguen las personalidades mirando el scope?

### 2026-08-16 — plan 8: fuego contenido, embestida y telemetría

Ocho tareas. Los enemigos ahora pueden matarse entre ellos, no se disparan a través de un
compañero, el que embiste avisa antes de llegar, y la telemetría distingue por personalidad.
352 tests.

**Lo más importante de esta medición es que no midió nada.** Los cinco contadores nuevos dan
cero en las cinco semillas, y el baseline sin input es idéntico al del plan 7:

| Contador | Valor en las 5 semillas |
|---|---|
| Fuego contenido por compañero | 0 |
| Muertes por fuego amigo | 0 |
| Torpedos perdidos en choques | 0 |
| Avisos de embestida | 0 |

La razón es correcta y conviene entenderla: **sin input sólo entra la oleada 1, que es un solo
STALKER.** Con un enemigo no hay compañero que tapar el carril, no hay a quién matar por
error, no hay torpedos que se crucen, y el RAM no aparece hasta la oleada 4. Todo el plan 8
vive en oleadas que el baseline sin input no alcanza.

`[OPEN]` **El plan 8 está sin validar en su conjunto.** Sus reglas están probadas en tests
unitarios con escenarios construidos a mano, pero ninguna se ha visto ocurrir en una partida.
Hace falta jugar, o un bot que dispare, para llegar a la oleada 2 y más allá.

**Una afirmación de diseño retirada.** El spec §7.2 decía que ocho enemigos rodeando al
jugador se taparían la línea entre ellos, y con eso justifiqué descartar la manada con turnos.
Medido al implementarlo, es falso: en un anillo al mismo radio el carril va radial hacia el
centro, así que el vecino a 45° queda a `R·sin45°` —0.636 para R=0.90— contra un corredor de
0.040 de ancho. Un enemigo más cercano tapa apenas un cono de ±4°, un 2% del círculo. Con
ocho enemigos la probabilidad de estar tapado ronda el 15%: ocurre de vez en cuando, no la
mayoría de las veces.

El chequeo se queda porque su trabajo principal sí lo hace —convierte el fuego amigo de
imposible en raro-pero-posible, sin dados— pero **no ordena la presión**. Lo que sí reparte el
fuego, y ya estaba, es el escalonamiento de entrada, la duda por personalidad y la receta de
oleada.

**Un defecto funcional que encontró el cierre del plan 7.** `FiringRadius` se escribía, se
poblaba en las cinco personalidades y se testeaba — y nadie lo leía. El RAM disparaba torpedos
cuando su diseño entero es no disparar, y el STALKER tiraba desde el borde en vez de cerrar
primero, lo que vaciaba de sentido el ciclo de acecho. Los tests no podían verlo: afirmaban
que el dato era correcto, no que alguien lo usara.

El arreglo obvio habría sido peor. El gobierno se acerca al radio preferido de forma asintótica
**desde afuera** —medido: PICKET converge a 0.900000000 sin cruzar— así que un radio de tiro
igual al preferido significa que esa personalidad no dispara nunca. Los radios de compromiso
quedaron por encima de los preferidos, con la razón escrita en el campo.

- [ ] Pendiente de playtest: ¿ocurre alguna vez el fuego amigo, y se lee como accidente?
- [ ] Pendiente de playtest: ¿el aviso del RAM llega con tiempo para reaccionar?
- [ ] Pendiente de playtest: ¿se distinguen las personalidades mirando el scope?

### 2026-08-28 — plan 9: el piloto automático encuentra una pared que no es la esperada

Primera vez que alguien —o algo— ve las oleadas 2, 3 y 4 de este juego. El bot juega con el
mismo snapshot que el jugador: lecturas envejecidas y con gating, sin acceso a la verdad del
mundo. No recibe el `GameController`, así que hacer trampa no compila.

`[ASSUMPTION]` Presupuesto de un comando cada 1,5 s. La pregunta 2 sigue sin responder, así
que es una perilla declarada, no un dato.

`[ASSUMPTION]` El bot asocia lecturas por id de contacto; una persona asocia por cercanía y a
veces se equivoca de blanco. Eso lo hace una **cota superior**: nadie va a superar esta pared.

**El hallazgo: la corrida no la decide el ritmo, la decide el pañol.**

| Semilla | Disparos por oleada | Total | Final |
|---|---|---|---|
| 20260811 | 2 · 4 · 8 · 1 | **15** | muere en la oleada 4, por embestida, tick 10 526 |
| 11111111 | 1 · 5 · 7 · 2 | **15** | muere en la oleada 4, por embestida, tick 11 344 |
| 22222222 | 7 · 7 · 1 | **15** | **no muere**: 53 593 ticks a la deriva en la oleada 3 |
| 33333333 | 7 · 8 | **15** | **no muere**: 55 454 ticks a la deriva en la oleada 2 |
| 44444444 | 7 · 6 · 2 | **15** | **no muere**: 53 757 ticks a la deriva en la oleada 3 |

Las cinco terminan con **exactamente quince tiros**, que es todo lo que hay:
`magazineSize` 3 más `ammoReserve` 12. Ninguna corrida se acaba porque el jugador no diera
abasto — se acaban porque se quedó sin torpedos. Medido en el diagnóstico: con el pañol en
cero, el piloto seguía teniendo soluciones de tiro válidas con errores de 0,005 a 0,066, todas
muy dentro del radio de explosión de 0,100. **Veía los blancos y no tenía con qué.**

**Pregunta 7 — ¿la munición aprieta? Respondida, y de más:** no aprieta, estrangula. Es la
restricción que decide las cinco corridas antes que cualquier otra cosa.

**Pregunta 8 — ¿la fase extrema se dispara o se sobrevive?** Ninguna de las dos. Con el pañol
vacío y la defensa todavía disponible, el submarino entra en un limbo: no puede matar y casi
no puede morir. Tres de cinco semillas se quedaron ahí quince minutos de reloj de juego.

**La defensa es dominante.** `defenseCapacity: 1` es un límite de **concurrencia**, no de
inventario: se puede volver a poner una apenas explota la anterior. Medido en la oleada 3 de
la semilla 44444444: **168 amenazas absorbidas** con esa única ranura reutilizable. Al
desactivar la defensa en el piloto, las cinco semillas mueren, tres de ellas ahogadas — y la
20260811 muere en la oleada 4 con el índice de saturación en 4,00 y seis enemigos vivos.

O sea que la pared existe y se puede ver; hoy está tapada por dos cosas a la vez, y las dos
son perillas de diseño, no defectos del código:

| Perilla | Qué produce hoy |
|---|---|
| `ammoReserve` 12 | La corrida se acaba a los quince tiros, siempre |
| `defenseCapacity` como concurrencia | Con el pañol vacío, el jugador es casi inmortal |

**Las muertes que sí ocurren son por embestida, no por torpedo.** Las dos semillas que mueren
lo hacen con causa `Collision`, en la oleada 4, que es donde entra el RAM. Con el pañol vacío
el RAM no tiene oposición posible.

`[OPEN]` Qué se ajusta —el pañol, la defensa, o ninguno porque el limbo es información
suficiente— es decisión de diseño y no la toma este plan.

**Verificación por mutación.** Cinco mutaciones sobre una suite de 411 tests:

| Mutación | Caídos | Qué demuestra |
|---|---|---|
| M1 · `Decide` devuelve siempre `null` | **24** | Cae `ThePilotActuallyPlays`: el control positivo sirve. Un bot que no teclea no pasa |
| M2 · el índice devuelve siempre 99 | **12** | Cae `TheFirstWaveIsNotAWall` |
| M3 · el índice devuelve siempre 0 | **6** | Cae `TheDemoSeedDiesUnderPressure`, y `TheFirstWaveIsNotAWall` aguanta |
| M4 · extrapolación apagada | **8** | Cae la muerte bajo presión, pero **no** `ThePilotActuallyPlays` |
| M5 · el piloto nunca despliega defensa | **4** | Sólo los unitarios de defensa: **ninguna afirmación de la pared se mueve** |

Dos filas valen más que su número.

**M2 y M3 se controlan entre sí.** Un medidor trabado en alto rompe una afirmación y un
medidor trabado en cero rompe la otra. Si las dos cayeran con la misma mutación serían la
misma afirmación escrita dos veces, que es la forma de test vacuo del §4 del contrato.

**M4 no rompe `ThePilotActuallyPlays`, y eso corrige una suposición del plan.** El plan daba
por hecho que sin adelantar el blanco el piloto no le pegaría a nada. Falso: apuntando a la
lectura cruda igual llega a la oleada 2 y mata. A corta distancia el adelanto no decide el
impacto — decide la eficiencia, y la eficiencia sólo se nota cuando el pañol es escaso.

**M5 no mueve ninguna afirmación, y eso es exactamente lo que el plan avisó que había que
mirar en el reporte y no en el test.** Quitarle la defensa al piloto convierte tres semillas
que quedaban a la deriva en tres muertes. La suite no lo ve porque, a propósito, no afirma la
muerte en esas tres. **El efecto de la defensa es invisible para los tests y evidente en el
reporte** — que es la razón por la que este plan reporta números en vez de afirmarlos.
