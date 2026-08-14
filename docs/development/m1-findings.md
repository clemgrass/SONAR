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
