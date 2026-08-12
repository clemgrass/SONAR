# SONAR — Diseño de preproducción

**Fecha:** 2026-08-11
**Estado:** aprobado, listo para planificación
**Fuente:** `GAME DESIGN DOCUMENT.md` v0.1
**Alcance:** decisiones de diseño abiertas, arquitectura núcleo, repositorio y proceso, milestones de prototipo y demo

---

## 1. Para qué existe este documento

El GDD define **qué** y **por qué**. Su §63 enumera unas 35 áreas de decisión técnica sin resolver
y su §70 plantea el gobierno del desarrollo asistido por IA. Este spec resuelve el subconjunto que
debe estar cerrado **antes de escribir código**, y deja explícitamente marcado lo que no puede
decidirse en papel.

Está escrito para ser el documento de entrega a un modelo de menor capacidad que ejecutará el
desarrollo. Debe poder leerse sin acceso a la conversación que lo originó.

### Terminología

`[DECIDED]` **Se dice `torpedo`, no `missile`,** para ambos bandos, en el código y en el texto del
juego. El GDD usa "misiles" en §14 y §26, pero los misiles lanzados desde submarino no existen
hasta los años cincuenta. Un submarino de 1945 dispara torpedos, y el §4 del GDD pide que la época
se infiera de la terminología.

### Notación

Sigue el §70.6 del GDD:

| Marca | Significado |
|---|---|
| `[DECIDED]` | Decisión cerrada. No se revierte sin autorización humana explícita. |
| `[OPEN]` | Pendiente deliberado. Se resuelve con datos, no con criterio. |
| `[ASSUMPTION]` | Supuesto de trabajo, reversible y barato. |
| `[RISK]` | Riesgo identificado con mitigación asociada. |

**Un agente no puede convertir `[OPEN]` en `[DECIDED]`.** Puede proponer, debe reportar
contradicciones, y debe detenerse si una instrucción choca con una decisión de este documento.

---

## 2. Alcance y lo que queda fuera

### Dentro

- Huecos de diseño que condicionan todo lo demás (§4)
- Espina arquitectónica: ensamblados, fronteras, reloj, determinismo (§5)
- Repositorio, documentación, tickets, git, proceso de agente (§6)
- Milestones M1 (validación) y M2 (demo) (§7)

### Fuera, y por qué

| Área | Razón |
|---|---|
| Valores de balance | El §64 del GDD establece que salen de prototipado. Fijarlos aquí sería ficción. |
| Story bible, canon, diálogos | Contenido autoral. Depende de que M1 valide el núcleo. |
| Contenido codificado de las pistas | Ídem. Aquí solo se fija su **forma mecánica**. |
| Parser, diálogo, UI, audio en detalle | Derivación mecánica una vez fijado lo anterior. Specs posteriores. |

**Justificación del recorte:** el GDD mismo (§64) dice que velocidad de proyectil, período de
barrido, densidad del bullet hell y curva de dificultad deben determinarse mediante prototipado y
playtesting. Cerrar esas variables en un documento previo produciría trabajo que se descarta.

---

## 3. Contexto de desarrollo

`[DECIDED]` **Un solo desarrollador humano más agentes de IA.**

Consecuencia directa: el aparato del §70 pensado para equipos —roles de agente separados,
handoffs formales, política de PR, review humano obligatorio— se recorta. Lo que se conserva es
lo que evita que **un** agente se desvíe, no lo que coordina personas. El detalle está en §6.4.

`[DECIDED]` Perfil técnico: ingeniero en computación, tres juegos terminados en Godot con
GDScript, C# es el punto más débil del stack.

Consecuencia: **la frontera Godot↔C# se especifica de forma explícita y ejemplificada**, no
asumida. Es el área de menor experiencia y la de mayor consecuencia arquitectónica.

---

## 4. Decisiones de diseño

### 4.1 Modelo de información del sonar

`[DECIDED]` **Gating por clase de contacto.** El barrido filtra lo ajeno, no lo propio.

| Clase | Regla |
|---|---|
| Contacto enemigo | Solo se actualiza cuando el haz pasa. La lectura envejece y se atenúa. |
| Torpedo y defensa propios | Visibles siempre. Son tuyos: sabes dónde están. |
| Torpedo enemigo | Detectado por ruido, no por barrido. Continuo, menos preciso. |
| Detonación e impacto | Se oye. Confirmación inmediata y exacta. |

**Estas cuatro reglas son infraestructura narrativa, no solo mecánica.** Constituyen el
vocabulario que la fase 2 viola de a una (§51 del GDD):

- un contacto que se actualiza sin que pase el haz;
- una defensa propia que desaparece del scope;
- un torpedo enemigo que no hace ruido;
- un impacto propio que nadie confirma.

El jugador debe conocer estas reglas en los huesos. Romperlas antes de que estén interiorizadas
produce la lectura de "bug", no de anomalía.

**Alternativas descartadas.** Gating estricto sobre todo: convierte el §47 en memorización pura.
Sin gating: contradice el §2.2, vacía el §15 y deja el §13 sin consecuencia mecánica.

### 4.2 Sistema de coordenadas

`[DECIDED]` El modelo canónico de targeting es **polar**, único, no negociable por notación.

`[DECIDED]` **Marcación 000–359 grados + banda de distancia identificada por letra.**
Formato de entrada `<letra><marcación>`, ejemplo `C047`.

El rango de letras se deriva del número de bandas, que es `[OPEN]`. A–E (cinco bandas) es el valor
de trabajo de este documento y el que usan todos los ejemplos; si M1 concluye ocho, el rango pasa a
A–H sin que cambie nada más del modelo.

`[DECIDED]` **El sonar etiqueta cada contacto detectado con su lectura** (ej. `047 C`).

Con 360 marcaciones el jugador no puede estimar el ángulo a ojo, y eso es deliberado: la
habilidad pasa a ser **extrapolar lecturas sucesivas** (`047 → 051 → 055 → predecir 063`).
Realiza el §15 con números concretos en vez de intuición visual, y convierte el sonar en un
instrumento que se lee en lugar de un dibujo que se mira.

`[DECIDED]` **Solo la notación polar alfanumérica sale al inicio.** El formatter se mantiene
separado del modelo canónico desde el día uno, de modo que agregar notaciones —cartesiana,
marcación pura— no requiera refactor.

**Nota de diseño sobre notaciones futuras.** Si se agregan, todas deben resolver contra el mismo
sector canónico. Una notación cartesiana sería deliberadamente **lossy a larga distancia**: celdas
vecinas en el borde resuelven al mismo sector. Eso no es un defecto a ocultar sino canon —es una
grilla de ploteo aproximada dibujada sobre los retornos reales— y da material de diálogo a T2.

`[OPEN]` Número de bandas (5 u 8), relleno de ceros en la marcación (`C047` vs `C47`), radio de
impacto directo, radio de detonación. → M1.

### 4.3 Modelo de torpedo

`[DECIDED]` El torpedo va **vivo desde la distancia mínima de armado** y viaja por la marcación.

1. Choca con **lo primero que se le cruce**, sea lo que sea.
2. Si no choca con nada, corre hasta la distancia que nombra la letra.
3. Al agotar la corrida, **detona con radio de explosión**.

`[DECIDED]` **Zona muerta.** Ninguna banda empieza pegada al submarino. Existe un radio interior
donde ningún torpedo puede estar armado.

Es históricamente exacto —los torpedos de la época tenían corrida mínima de seguridad— y produce
un agujero táctico real: cuando un contacto entra ahí, el arma principal deja de existir y hay que
resolver con defensas. Pánico sin mecánica nueva.

#### Por qué la letra no es decorativa

Las dos configuraciones fallan de maneras **opuestas**:

| Configuración | Perdona | Castiga |
|---|---|---|
| Corrida larga (`E`) | no saber la distancia | error de marcación |
| Corrida fijada (`C`) | grados de marcación, vía radio | error de distancia |

Cada disparo plantea la misma pregunta: *¿qué conozco mejor de este contacto, su rumbo o su
distancia?* Un contacto de frente mantiene marcación y varía distancia → corrida larga. Uno que
cruza de lado mantiene distancia y barre grados → corrida fijada. Leer eso es el §15 aplicado.

Sin el radio de detonación, `E` domina siempre y la letra se vuelve decorativa. El radio es la
única regla añadida y es lo que hace que la elección exista.

`[DECIDED]` **Cero aleatoriedad en el combate.** Ni dispersión, ni punto de apagado aleatorio, ni
daño variable.

Razones: §2.6 (maestría), §29 (speedrun), §47 (bullet hell aprendible), §62 (consistencia del
misterio). Y la decisiva: **un sistema que ya se comporta de forma impredecible no puede señalar
anomalías**, y ese es el recurso narrativo central del juego.

`[OPEN]` `SALVO` — abanico de tres torpedos por tres de munición. Especificado, no comprometido.
Depende de si la munición realmente aprieta. → M1.

### 4.4 RETRY y persistencia

`[DECIDED]` **Reset puro.** La misión vuelve a cero por completo. No hay banderas de progreso
guardadas.

`[DECIDED]` **Las tres pistas son eventos que siempre ocurren, en puntos fijos de la misión.**
No dependen del azar ni de condiciones previas. Lo que depende del jugador es notarlas, entender
qué hacer con ellas, y lograr las tres en una misma corrida.

`[DECIDED]` **La persistencia vive en el cuaderno del jugador, no en el save.** El conocimiento es
lo único que cruza corridas. Referencia de diseño: Outer Wilds. Encaja con el §69 mejor que un
sistema de banderas: las banderas dan progreso, el conocimiento da comprensión.

`[DECIDED]` Corolario: **el jugador puede ejecutar una pista sin volver a descubrirla.** Si ya
decodificó el patrón, cuando llegue la ventana escribe la respuesta directamente. Lo que marca el
ritmo son las ventanas, no el puzzle.

`[DECIDED]` **La simulación debe ser determinista.** Deja de ser opcional: si los eventos no
ocurrieran igual en cada corrida, el conocimiento del jugador no serviría y el diseño colapsa.
Cierra el §62 en *determinista*. Detalle técnico en §5.4.

`[ASSUMPTION]` El historial de terminal se limpia con el reset. Récords y estadísticas sobreviven
por no ser estado de misión; el §29 los requiere para que exista speedrun. Reversible.

### 4.5 Entrada de comandos

`[DECIDED]` **La coordenada pelada dispara.** `C047` lanza el torpedo. `FIRE C047` sigue siendo
válido y es la forma que enseña el cheatsheet (§9). Reduce el comando de 9 caracteres a 4 sin
quitarle nada al principiante. El §10 ya listaba `G7` pelado como input válido.

`[DECIDED]` Conjunto de comandos — cinco familias más superficie de escritura libre:

| Familia | Comandos |
|---|---|
| Combate | `C047` · `FIRE C047` · `SALVO C047` `[OPEN]` |
| Defensa | `DEF C047` |
| Sistemas | `RELOAD` · `SONAR` · `REPAIR <sistema>` |
| Estado | `STATUS` |
| Conversación | cualquier otro texto — se registra, puede disparar diálogo, normalmente no hace nada (§10) |

Todo lo necesario para la campaña normal cabe en el cheatsheet (§9). Ninguno de estos comandos
existe solo para agregar contenido (§59).

`[OPEN]` Repetición y ajuste fino: Enter en vacío repite el último disparo; `+`/`-` corrigen la
marcación unos grados. Muy rápido y satisfactorio de dominar, pero puede trivializar el cálculo de
intercepción. → M1.

### 4.6 Confirmación de impacto

`[DECIDED]` **Todo impacto se confirma siempre, de inmediato, apunte donde apunte el barrido.**
Un impacto invisible castiga al jugador por acertar.

Justificación diegética: una detonación submarina es ruidosísima; los hidrófonos la registran al
instante. La posición es exacta porque el torpedo propio siempre es visible —se sabe dónde estaba
al estallar—. Ambas cosas se derivan de reglas ya establecidas en §4.1, no son excepciones.

`[DECIDED]` La confirmación es de **destrucción**, no solo de detonación. Un torpedo contra casco
no suena como uno en agua abierta, y las tripulaciones distinguían perfectamente. Da material de
diálogo a T2.

### 4.7 Registros del historial

`[DECIDED]` **Cuatro registros con marca visual propia, el cuarto reservado.**

| Registro | Ejemplo | Uso |
|---|---|---|
| Jugador | `> C047` | lo que escribe |
| Instrumento | `  IMPACTO · 047 C` | respuesta del sistema |
| Tripulación | `T2: Se está partiendo.` | diálogo |
| **Sin autor** | `Retry?` | **reservado** |

El §6 insiste en que `Retry?` no lleve `SYSTEM:` ni `UNKNOWN:`. Esa ausencia solo significa algo si
todo lo demás **sí** está firmado. Si el instrumento responde sin firma, `Retry?` se diluye entre
las respuestas del sistema y se pierde el mejor gancho narrativo del juego.

`[DECIDED]` El cuarto registro es un **recurso narrativo con presupuesto**: cada aparición gasta
parte de su efecto. Se usa en el `Retry?` y a lo sumo dos o tres momentos más, siempre en fase 2.

`[OPEN]` Forma exacta de distinguirlos —color, indentación, peso, mayúsculas—. → diseño de UI, con
pruebas de legibilidad reales.

### 4.8 Idioma y voz

`[DECIDED]` **Inglés únicamente**, comandos y diálogo. Autenticidad para un submarino aliado del
45, mercado de Steam para terror de nicho, y evita que la localización rompa las pistas.

`[RISK]` El autor no es angloparlante nativo. Escribir tres personajes con personalidad fuerte en
segundo idioma pierde matiz, humor y ritmo de una forma que no se detecta desde adentro.

> **Mitigación, presupuestada desde ahora:** una pasada de un hablante nativo sobre todo el
> diálogo antes de cerrar contenido. No improvisar esto al final del proyecto.

`[DECIDED]` **Texto más textura vocal no lingüística.** Sin diálogo hablado. Sí presencia humana:
respiración, gritos ahogados, murmullo de radio, voces que se oyen y no se entienden.

Cuesta poco, no se localiza, y para terror funciona mejor —una voz que no alcanzas a descifrar
asusta más que una clara—. Compatible con el §52, que ya priorizaba generación procedural.

### 4.9 Anomalía de la defensa (§25)

`[DECIDED]` **La anomalía sale gratis del sistema de coordenadas.** Al ser polares desde el
submarino, una defensa en `C047` permanece en `C047` aunque el submarino avance. Es relativa por
construcción: se programa el comportamiento normal y el raro aparece solo.

Pero requiere dos piezas para que el jugador pueda **leerla**. Sin ellas la anomalía existe en el
diseño y no llega nunca al jugador, porque el submarino está siempre centrado (§12) y nada sugiere
que navegue.

`[DECIDED]` **1 — T1 navega en voz alta.** Rumbos, velocidades y cambios de curso a lo largo de
toda la misión. Coste cero: es diálogo que ya iba a existir, con contenido náutico.

`[DECIDED]` **2 — Contactos inertes de referencia.** Clase nueva en el dominio: pecio, bajo
rocoso, boya. No atacan, no evaden, no se mueven por sí mismos, y por tanto **derivan por el
scope** de forma consistente con el rumbo cantado.

Sin la pieza 2 no hay contra qué comparar. Con las dos, la anomalía se arma sola en la cabeza del
jugador —*el pecio deriva, la defensa no*— sin que nadie se la señale (§2.4).

La clase inerte paga su costo dos veces: además da al sonar cosas que detectar que no son
amenazas, lo que hace el scope creíble como instrumento en vez de un espera-enemigos.

### 4.10 Forma mecánica de las pistas

`[DECIDED]` Las cinco reutilizan sistemas existentes (§51). El **contenido codificado** pertenece
a la story bible, no a este spec.

| Pista | Forma mecánica |
|---|---|
| 1 | Contacto que **parpadea en Morse**. Sustituye el Braille del §41. |
| 2 | **Casi-impacto deliberado** sobre un contacto que rompe la regla 1. |
| 3 | **Coordenadas por radio sobre agua vacía.** Reutiliza el sistema de confirmación. |
| 4 | Los tres objetivos entre quince son **los que rompen las reglas del instrumento**. |
| 5 | **Sobrevivir.** Ejecución pura, aprendible gracias al determinismo. |

**Pista 1 — por qué Morse y no Braille.** El §14 ya establece "patrones de parpadeo" como
diferenciador legítimo de contactos: el canal visual existe y la pista solo lo usa con
significado. Braille es conocimiento de fuera del mundo sin justificación diegética en 1945 —esta
tripulación no tiene razón para leerlo— y eso choca con el §35, que exige que las soluciones
surjan de información disponible dentro del juego.

**Pista 2 — por qué el casi-impacto.** El §42 pide interacción "de una manera específica". Un
torpedo cuya corrida detona cerca sin alcanzar el objetivo requiere entender la banda, calcular el
radio y decidir no matar: tres cosas que no se hacen sin querer. Es una forma de decir *"te veo y
no voy a disparar"* usando solo el lenguaje del instrumento.

> **Requisito de implementación derivado.** El casi-impacto solo funciona si el juego lo
> **reconoce**. Hace falta una ventana de proximidad: la detonación debe caer lo bastante cerca del
> contacto como para ser claramente intencional, y lo bastante lejos como para no destruirlo — es
> decir, fuera del radio de detonación pero dentro de una ventana mayor. Sin esa ventana explícita
> el gesto es indistinguible de un fallo. El ancho de la ventana es `[OPEN]` y va a M1.

**Pista 4 — cierra el §69 de forma literal.** Su solución es *haber entendido el instrumento*, no
haber encontrado un secreto. Las cuatro reglas de §4.1 se vuelven la llave de identificación: quien
las aprendió ve tres contactos destacar entre quince; quien no, ve quince puntos idénticos.

`[DECIDED]` **Anti-fuerza-bruta resuelto sin mecanismo extra** (§63.15). Hay 1800 coordenadas
posibles y la munición es limitada por corrida. El reset puro ya lo cubre.

---

## 5. Arquitectura

### 5.1 Ensamblados y regla de referencias

```
Sonar.Game          proyecto Godot — nodos, escenas, render, audio, input
   │                referencia → Sonar.Controller, GodotSharp
   ▼
Sonar.Controller    class library .NET pura — parser, tick, snapshots, eventos
   │                referencia → Sonar.Core
   │                SIN referencia a Godot
   ▼
Sonar.Core          class library .NET pura — dominio, simulación, reglas
                    SIN referencia a nada
```

Más `Sonar.Core.Tests` y `Sonar.Controller.Tests`.

`[DECIDED]` **El §63.1 pregunta cómo evitar que C# dependa accidentalmente de Godot. La respuesta
es estructural, no disciplinaria:** `Sonar.Core.csproj` y `Sonar.Controller.csproj` no tienen la
referencia a GodotSharp. Un `using Godot;` ahí no compila.

`[DECIDED]` **El Controller tampoco referencia Godot.** Es el punto contraintuitivo y el
importante: si lo hiciera, tipos de Godot podrían filtrarse hacia Core a través de él. Godot conoce
al Controller; el Controller no conoce a Godot. Una sola dirección.

### 5.2 Comunicación entre capas

**Hacia abajo — entrada:**

```
Godot capta texto crudo
   → Controller parsea → Command
      → Core valida y aplica en frontera de tick
```

`[DECIDED]` Los comandos se aplican **en frontera de tick**, nunca a mitad. Es lo que mantiene la
simulación reproducible.

**Hacia arriba — salida:**

| Canal | Contenido | Consumidor |
|---|---|---|
| `WorldSnapshot` | estado continuo: posiciones, ángulo del barrido, lecturas | render del sonar |
| `Event[]` | cosas que pasaron: detonación, muerte, daño, línea de diálogo | historial, audio |

`[DECIDED]` Ambos **inmutables**. El snapshot es función pura del estado del tick.

### 5.3 El reloj

`[DECIDED]` **Core nunca ve tiempo de pared.** Godot llama a `Controller.Advance(delta)` desde
`_Process`; el Controller acumula y llama a `Core.Tick()` en pasos fijos de duración constante.

Desacopla la simulación de los FPS, permite correr Core headless a cualquier velocidad en tests, y
es la mitad del determinismo. La otra mitad es que **todo el RNG se inyecta sembrado**: Core no
construye generadores por su cuenta.

### 5.4 Determinismo — alcance de la promesa

`[DECIDED]` **Se promete reproducibilidad en la misma máquina:** misma semilla, misma corrida,
mismos eventos en los mismos tiempos. Se consigue con timestep fijo, RNG sembrado e inyectado, y
orden de actualización estable.

`[DECIDED]` **No se promete identidad bit a bit entre plataformas.** Eso exige aritmética de punto
fijo o evitar toda función trascendental, y es un proyecto dentro del proyecto.

`[OPEN]` Determinismo bit a bit. Solo haría falta para **compartir replays** o verificar
speedruns por semilla en vez de por video. Con esta arquitectura, subirlo después significa cambiar
el tipo numérico dentro de Core, no rehacer nada.

### 5.5 Guardarraíl para el agente — qué tiene prohibido la capa Godot

**Prohibido:**

- mutar estado de simulación
- decidir si un disparo acierta
- medir tiempo de juego o de misión
- generar números aleatorios
- conocer reglas: bandas, armado, munición, pistas

**Permitido:**

- dibujar el snapshot
- reaccionar a eventos con sonido y animación
- pasar texto crudo hacia abajo

`[DECIDED]` **Prueba de que la frontera está bien:** toda la campaña debe poder jugarse y ganarse
sin abrir Godot, desde un test que empuja comandos y lee eventos. Si eso no se puede, hay lógica
que se filtró hacia arriba.

---

## 6. Repositorio y proceso

### 6.1 Estructura

```
sonar/
├── src/
│   ├── Sonar.Core/
│   ├── Sonar.Controller/
│   └── Sonar.Game/           proyecto Godot
├── tests/
│   ├── Sonar.Core.Tests/
│   └── Sonar.Controller.Tests/
├── docs/
│   ├── gdd/                  el GDD partido en piezas cargables
│   ├── tdd/
│   ├── adr/
│   ├── tickets/
│   ├── story/                canon, diálogos, contenido de pistas
│   └── development/          contrato de agente, convenciones
├── project-state.yaml
├── global.json               fija el SDK de .NET
└── Sonar.sln
```

`[DECIDED]` `docs/story/` contiene el misterio completo en claro. Es un repositorio privado de una
persona; cifrarlo (§70.27) sería ceremonia. Lo único que importa: **ningún archivo de `docs/` entra
al build de Steam**, verificado en el pipeline de release.

### 6.2 Enmienda aprobada al §70.3 y §70.7 del GDD

`[DECIDED]` **Las instrucciones de agente viven dentro del repositorio, en formato neutral.**

El GDD pedía que vivieran fuera (`~/sonar-horror-ai/`) para que el repositorio fuera agnóstico a la
IA. El principio se conserva; la implementación cambia, por dos razones:

1. Documentación que no se versiona junto al código que restringe **se desactualiza sin aviso**.
2. Un `git clone` en una máquina nueva no la trae, y el agente arranca ciego — justo lo que el
   §70.19 quiere evitar.

**Forma acordada:**

- `docs/development/AGENT_CONTRACT.md` — neutral, no nombra ningún proveedor.
- `CLAUDE.md`, `AGENTS.md`, `.cursorrules` — tres líneas cada uno apuntando al anterior.

Un `CONTRIBUTING.md` no hace que un repositorio dependa de un humano concreto; un contrato de
agente neutral tampoco lo hace depender de una IA. Lo reemplazable son los punteros. Cumple §70.2,
§70.8 y §70.35.

### 6.3 Formato de ticket

`[DECIDED]` Siete campos. El §70.11 listaba trece; se conservan los que cambian lo que el agente
hace.

```yaml
id: SH-COMBAT-021
title: Detonación de fin de corrida con radio
why: spec §4.3 · gdd §17
depends_on: [SH-COMBAT-018]
scope:
  allow: [src/Sonar.Core/Combat/**, tests/Sonar.Core.Tests/Combat/**]
  deny:  [src/Sonar.Game/**, src/Sonar.Core/Narrative/**]
requirements:   # numeradas y verificables
acceptance:     # ejecutable — "estos tests pasan"
```

`scope` es el campo importante: impide que un agente implementando torpedos toque el sistema de
diálogos.

Eliminados: *implementation notes* (invita al ticket a escribir el código), *definition of done*
(vive en el contrato, no por ticket), y cuatro campos que repetían información de otros.

### 6.4 Recortes al §70

| Recortado | Razón |
|---|---|
| Roles de agente separados con permisos distintos | Un agente con tarea acotada alcanza. Los roles coordinan personas que no existen. |
| Documentos formales de handoff | El ticket, el historial de git y `project-state.yaml` **son** el handoff. Duplicarlo lo desactualiza. |
| Política de PR y review obligatorio | No hay segundo humano. Revisar antes de push es hábito, no proceso. |
| Documentación narrativa cifrada o segregada | Repositorio privado de una persona. |

| Conservado | Razón |
|---|---|
| `scope` de archivos en el ticket | Cambia el comportamiento del agente de forma medible. |
| ADRs | Evita que se vuelvan a discutir decisiones ya tomadas. |
| `project-state.yaml` | Responde "dónde estamos" sin leer todo el corpus. |
| `[DECIDED]`/`[OPEN]`/`[ASSUMPTION]` | La mejor idea del §70. |

`[DECIDED]` Mantener `project-state.yaml` **corto**, y actualizarlo como primer paso de la
Definition of Done. Si crece, se desactualiza.

### 6.5 Git, versiones, CI

`[DECIDED]` **Git** — una rama por ticket (`sh-combat-021`), commit referenciando el id, el agente
commitea local, el humano revisa y empuja. Sin marcas especiales en commits generados por IA: el
§70.15 ya decidió que no existen dos categorías de código.

`[DECIDED]` **Versiones** — Godot y el SDK de .NET se fijan exactos en el setup, se anotan en
`project-state.yaml` y se congelan con `global.json`.

> Este spec **no fija números de versión a propósito**. Hay que verificar qué versión de .NET
> requiere la versión de Godot elegida, contra la documentación oficial en el momento del setup, no
> de memoria. Es tarea del primer ticket.

`[DECIDED]` **Tests** — xUnit. El más importante desde el día uno: **correr la misma semilla dos
veces y comparar un hash del estado**. Barato, y detecta cualquier regresión de determinismo en el
commit que la introduce en vez de meses después, cuando una pista deje de funcionar sin causa
aparente.

`[DECIDED]` **CI** — si el repositorio va a GitHub, una acción que ejecute `dotnet test` en cada
push. Protege lo único que no se puede verificar a ojo.

---

## 7. Milestones

### 7.1 M1 — Validación

`[DECIDED]` **Pregunta existencial: ¿extrapolar lecturas es satisfactorio o es tarea de
matemáticas?**

Todo el diseño descansa en que leer `047 → 051 → 055` y predecir `063` se sienta como pilotear un
instrumento. Si se siente como aritmética bajo presión, el juego no funciona y ninguna cantidad de
narrativa lo arregla.

`[DECIDED]` **M1 tiene autoridad para invalidar decisiones de este documento.** Es su función.
Descubrirlo aquí cuesta semanas; descubrirlo después de escribir la story bible cuesta el proyecto.

**Entra:**

- Core — contactos enemigos e inertes, barrido con gating por clase, modelo de torpedo completo,
  zona muerta, defensas, munición y recarga, **ataque enemigo y supervivencia del jugador**,
  tick determinista, RNG sembrado
- Controller — parser, snapshot, eventos, acumulador de paso fijo
- Game — una escena, sonar con `_Draw()`, caja de texto, log plano
- Tests — determinismo, combate, parser

**No entra:** narrativa, diálogo, tripulación, pistas, fase 2, finales, **estados de daño y
`REPAIR`**, audio, menús, guardado, ajustes, cualquier cosa que se parezca a arte.

> **Corrección de alcance — 2026-08-11.** La primera versión de este spec omitía el ataque enemigo
> en M1. Era un error de acotamiento: el GDD lo establece en §14 (misiles enemigos), §20 (jugador de
> dos impactos), §24 (defensa contra ellos) y §26 (eje de dificultad).
>
> Sin amenaza, M1 no puede responder su pregunta 8, no ejercita la tercera clase de información de
> §4.1 —el torpedo enemigo detectado por ruido, una de las cuatro reglas que la demo debe enseñar—
> y da números optimistas en las preguntas 2, 3, 6 y 7, porque el tiempo por comando y la densidad
> jugable dependen de estar bajo presión.
>
> `[DECIDED]` **Alcance del daño en M1: dos impactos y se acaba la corrida (§20), sin desactivación
> de sistemas.** Los estados de daño del §21 y el comando `REPAIR` van a un milestone posterior con
> su propia pasada de diseño, porque el §63.8 pregunta si el daño es determinista o aleatorio y la
> prohibición de azar de §4.3 obliga a diseñar una selección determinista que todavía no existe.

Es deliberadamente feo. Pulir el render es señal de haberse salido del milestone.

`[DECIDED]` Se construye **sobre la arquitectura real**, no como maqueta desechable. El Core es lo
que se está validando y ya es testeable headless. Lo desechable es el front de Godot.

**Preguntas que debe responder:**

| # | Pregunta | Medición |
|---|---|---|
| 1 | ¿Extrapolar es satisfactorio? | proxy: ¿sube la tasa de acierto entre sesiones? |
| 2 | Tiempo medio por comando | instrumentado y registrado, no a ojo |
| 3 | Máximo de contactos jugable | subir hasta que se rompa |
| 4 | Período del barrido | probar 2,5 / 3,5 / 5 s |
| 5 | Número de bandas y radios | 5 contra 8 |
| 6 | ¿Hace falta repetir y ajustar? | jugar sin eso y ver si se echa de menos |
| 7 | ¿La munición aprieta? | si no, `SALVO` no tiene por qué existir |
| 8 | ¿La fase extrema se dispara o se sobrevive? | densidad alta sin munición suficiente |

`[DECIDED]` **Criterio de salida: las ocho preguntas con respuesta anotada y los números en el
archivo de balance.** No termina cuando "se siente bien" — eso no es criterio, es sensación, y con
ella el milestone se estira indefinidamente.

Si la respuesta a la pregunta 1 es negativa, **M1 está igualmente completo**: cumplió su función.

### 7.2 M2 — Demo de Steam

`[DECIDED]` Una vez M1 valide el núcleo como divertido, **el prototipo evoluciona a la demo de
Steam**.

`[DECIDED]` **M1 y M2 son milestones separados con criterios de salida distintos.** Si validación y
presentación comparten milestone, el pulido contamina la fase de validación y se deja de responder
las ocho preguntas.

**M2 agrega lo que una demo necesita y un prototipo no:**

- poder salir del juego, ajustes mínimos, resolución
- algo de audio — una demo silenciosa de un juego cuyo §52 declara el audio fundamental
  representaría mal el producto
- estabilidad
- pipeline de build y depósito de Steam
- suficiente presencia de la tripulación para que la demo venda el juego correcto

`[DECIDED]` **La demo debe enseñar las cuatro reglas del instrumento y no contener ni una sola
violación.**

Un jugador que sale de la demo sabiendo que los enemigos solo se actualizan con el barrido, que lo
propio se ve siempre, que los torpedos enemigos se oyen y que los impactos se confirman, es un
jugador al que el juego completo puede romperle el estómago un año después.

La demo es la primera mitad del truco. Y se puede dataminar sin spoilear nada, porque lo que enseña
es **cómo funciona el mundo**, no qué le pasa.

`[OPEN]` Qué porción de campaña cubre la demo: secuencia de apertura más combate temprano, o un
escenario de entrenamiento autocontenido. El segundo cuesta contenido que no se reutiliza.

`[ASSUMPTION]` La demo se construye desde el mismo código con una bandera de build, no como fork.

`[RISK]` Publicar una demo implica página de Steam activa y por tanto compromiso de marketing con
calendario. Puede adelantar la necesidad de infraestructura —menús, ajustes, audio— respecto al
orden natural de desarrollo. A considerar al planificar.

---

## 8. Registro de decisiones abiertas

| Ref | Decisión | Se resuelve en |
|---|---|---|
| §4.2 | Número de bandas (y por tanto rango de letras), relleno de ceros, radios de impacto y detonación | M1 |
| §4.10 | Ancho de la ventana de proximidad que reconoce el casi-impacto de la pista 2 | M1 |
| §4.3 | `SALVO` | M1 |
| §4.5 | Repetición y ajuste fino | M1 |
| §4.7 | Forma visual exacta de los cuatro registros | diseño de UI |
| §5.4 | Determinismo bit a bit entre plataformas | solo si se quieren replays compartibles |
| §7.2 | Porción de campaña que cubre la demo | planificación de M2 |

## 9. Riesgos

| Riesgo | Mitigación |
|---|---|
| Diálogo escrito por no nativo pierde matiz (§4.8) | Pasada de nativo presupuestada antes de cerrar contenido |
| Extrapolar puede no ser divertido (§7.1) | M1 existe precisamente para descubrirlo antes de la narrativa |
| Compromiso de marketing por la demo (§7.2) | Reconocido; afecta orden de trabajo, a resolver en planificación |
| Frontera Godot↔C# es el área de menor experiencia (§3) | Especificada de forma explícita; enforcement estructural en §5.1 |

## 10. Trazabilidad al GDD

| Sección de este spec | GDD |
|---|---|
| §4.1 Modelo de información | §12, §13, §14, §15, §51 |
| §4.2 Coordenadas | §16, §63.5 |
| §4.3 Torpedo | §17, §18, §19, §22, §63.7 |
| §4.4 RETRY | §6, §39, §62, §63.16, §63.18 |
| §4.5 Comandos | §9, §10, §11, §59, §63.12 |
| §4.6 Confirmación | §51, §63.7 |
| §4.7 Registros | §6, §36 |
| §4.8 Idioma y voz | §52, §63.31 |
| §4.9 Defensa | §24, §25, §12 |
| §4.10 Pistas | §35, §40–§43, §46, §47, §63.15 |
| §5 Arquitectura | §60, §61, §62, §63.1–63.3 |
| §6 Repositorio y proceso | §70 completo, con enmienda a §70.3/§70.7 |
| §7 Milestones | §64, §65 |
