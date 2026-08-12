# GAME DESIGN DOCUMENT

# SONAR HORROR

**Estado:** Preproducción\
**Documento:** Game Design Document (GDD)\
**Versión:** 0.1\
**Plataforma objetivo:** PC / Steam\
**Motor:** Godot\
**Lenguaje de gameplay/backend:** C# / .NET\
**Género:** Psychological Cosmic Horror / Submarine Combat / Narrative Arcade\
**Perspectiva:** Instrument/interface-based\
**Duración objetivo:** \~40–60 minutos para una primera partida normal\
**Duración objetivo con investigación:** \~2–3 horas para descubrir y completar el contenido secreto\
**Modelo comercial:** Juego premium, contenido completo al lanzamiento\
**Post-lanzamiento:** Fixes solamente; contenido adicional únicamente mediante eventual DLC

---

# 1. HIGH CONCEPT

El jugador opera un submarino aliado durante una misión contra Japón en 1945.

El submarino se encuentra atrapado después de un ataque. La iluminación y los sistemas visuales convencionales desaparecen, dejando al jugador prácticamente a oscuras.

Su mundo se reduce a:

- un sonar;
- una terminal;
- las comunicaciones de la tripulación;
- audio;
- las posiciones y trayectorias de los contactos.

El jugador debe utilizar comandos de terminal para operar armas, defensas, sistemas del submarino y otras funciones necesarias para completar una misión militar.

La campaña es una única operación continua. La dificultad aumenta progresivamente mientras el jugador domina las mismas mecánicas.

La misión normal puede completarse sin descubrir ninguna anomalía importante.

Sin embargo, durante la operación existen tres pistas ocultas e independientes. Si el jugador descubre y resuelve las tres, la historia se desvía hacia una segunda fase secreta.

En esa segunda fase, la lógica del combate comienza a comportarse de maneras cada vez más extrañas, culminando en una situación extrema de supervivencia tipo bullet hell.

El misterio apunta progresivamente a la existencia de una entidad cósmica que parece estar interviniendo en el conflicto y, posiblemente, ayudando a los Aliados.

La pregunta central deja de ser:

> "¿Podemos ganar la guerra?"

y pasa a ser:

> "¿Qué nos está ayudando a ganarla?"

---

# 2. DESIGN PILLARS

## 2.1. Story First

La historia es el eje principal del juego.

El gameplay debe reforzar la narrativa, no competir con ella.

La campaña debe sentirse como una única operación militar real y continua.

---

## 2.2. Gameplay Through Interface

El jugador no controla directamente un personaje.

Controla un sistema.

La interfaz diegética es el juego.

El jugador aprende a interpretar:

- sonar;
- trayectorias;
- contactos;
- comunicaciones;
- terminal;
- estados del submarino.

---

## 2.3. Simple Rules, Increasing Complexity

Las reglas fundamentales deben ser fáciles de comprender.

La dificultad surge al combinar:

- más contactos;
- mayor velocidad;
- múltiples amenazas;
- recursos;
- defensa;
- ataques simultáneos;
- sistemas desactivados;
- menor margen temporal;
- anomalías.

No se busca añadir continuamente nuevas mecánicas.

El jugador debe dominar un conjunto relativamente pequeño de herramientas.

---

## 2.4. Discovery Over Explanation

Las anomalías deben poder existir sin ser explicadas.

El juego no debe anunciar:

> SECRET FOUND

ni:

> COSMIC HORROR DETECTED

La historia secreta debe construirse mediante:

- observación;
- experimentación;
- conversaciones;
- inconsistencias;
- patrones;
- comportamiento de sistemas;
- conexiones entre eventos.

---

## 2.5. The First Playthrough Must Stand Alone

Un jugador que nunca descubre una pista debe poder disfrutar y completar el juego.

La experiencia normal debe sentirse como un juego completo.

El misterio es una segunda capa.

---

## 2.6. Mastery

El juego debe recompensar el dominio.

El jugador debe pasar de:

> "¿Qué comando tengo que escribir?"

a:

> "Sé exactamente qué hacer."

El tiempo de finalización funciona como una métrica natural de dominio y speedrunning.

---

# 3. PLAYER EXPERIENCE

## Primera partida

La experiencia esperada:

1. El jugador inicia el juego.
2. Aparece el sonar.
3. El sistema falla.
4. La pantalla queda negra.
5. El sistema se recupera.
6. Aparecen comunicaciones de la tripulación.
7. El jugador recibe contexto.
8. Aprende las mecánicas.
9. Ejecuta la misión.
10. Escala progresivamente la dificultad.
11. Destruye el objetivo final.
12. Completa la misión.
13. La tripulación comunica el tiempo de misión.
14. El juego termina.

El jugador debería poder pensar:

> "¿Eso era todo?"

Esta sensación es intencional.

---

## Segunda etapa de engagement

Un jugador que haya notado inconsistencias puede comenzar a experimentar.

Puede:

- explorar la terminal;
- probar inputs;
- investigar conversaciones;
- observar patrones;
- buscar anomalías;
- repetir situaciones;
- analizar el sonar.

---

## Experiencia completa

El jugador eventualmente puede:

1. descubrir las tres primeras pistas;
2. activar la fase secreta;
3. resolver la cuarta pista;
4. sobrevivir a la secuencia extrema;
5. descubrir la quinta pista;
6. alcanzar el final secreto;
7. reconstruir la historia oculta.

---

# 4. NARRATIVE PREMISE

## Contexto histórico

La historia ocurre durante la Segunda Guerra Mundial, en 1945.

El jugador controla un submarino aliado operando contra Japón.

El juego no necesita declarar explícitamente el año ni explicar exhaustivamente el contexto histórico.

La época debe poder inferirse mediante:

- comunicaciones;
- terminología;
- tecnología;
- órdenes;
- ambientación;
- contexto militar.

La misión ocurre antes del uso de las armas nucleares sobre Japón.

---

# 5. PRIMARY OBJECTIVE

El objetivo explícito del jugador es:

> **Completar la misión militar asignada.**

La misión culmina con la destrucción de una instalación/base naval japonesa.

Esta instalación es necesaria para que los Aliados puedan continuar con una operación histórica posterior.

Si el jugador fracasa:

- la misión militar falla;
- la historia presenta una línea temporal alternativa;
- se implica que los Aliados ganaron en Europa pero perdieron el control de Asia;
- el jugador puede utilizar `RETRY`.

---

# 6. THE RETRY SYSTEM

La repetición de la misión existe tanto como:

- sistema de gameplay;
- mecanismo narrativo.

Cuando el jugador pierde:

1. el sonar desaparece;
2. la pantalla queda negra;
3. aparece una entrada en el historial.

Ejemplo:

```text
Retry?
```

No se muestra autor.

No aparece:

```text
UNKNOWN:
Retry?
```

No aparece:

```text
SYSTEM:
Retry?
```

Simplemente:

```text
Retry?
```

El jugador puede escribir:

```text
RETRY
```

y la misión comienza nuevamente.

---

## Interpretación narrativa

Inicialmente el jugador puede interpretar `RETRY` como una simple decisión de reinicio.

Posteriormente puede preguntarse:

> ¿Quién escribió "Retry?"?

La existencia del comando puede formar parte del misterio.

La naturaleza exacta de la mecánica temporal/repetición queda como decisión narrativa del TDD/story bible.

---

# 7. CAMPAIGN STRUCTURE

No existen niveles independientes.

No existe una estructura:

```text
Level 1
Level 2
Level 3
```

Existe una única operación continua.

La progresión ocurre mediante cambios en las circunstancias.

Conceptualmente:

```text
INTRO
 ↓
INITIAL ATTACK
 ↓
SYSTEM RECOVERY
 ↓
MISSION CONTEXT
 ↓
EARLY COMBAT
 ↓
ESCALATION
 ↓
NEW ORDERS
 ↓
COMPLEX ENCOUNTERS
 ↓
CRISIS
 ↓
FINAL APPROACH
 ↓
BASE ASSAULT
 ↓
VICTORY
```

Los encuentros concretos serán definidos posteriormente.

---

# 8. OPENING SEQUENCE

Al salir del menú principal:

1. aparece inmediatamente la interfaz del sonar;
2. el sonar funciona normalmente durante aproximadamente cinco segundos;
3. ocurre una interrupción general;
4. todo titila;
5. los sistemas parecen apagarse/destruirse;
6. la pantalla se recupera;
7. aparece una conversación en el historial;
8. la tripulación explica el contexto inmediato;
9. comienza la misión.

El inicio funciona simultáneamente como:

- introducción narrativa;
- tutorial;
- presentación de interfaz;
- presentación de la terminal;
- introducción de la tripulación.

---

# 9. TUTORIAL

El tutorial será híbrido.

## Diegético

La tripulación explica progresivamente las funciones cuando son necesarias.

## Documental

El jugador dispone de un cheatsheet diegético que contiene:

- comandos necesarios;
- procedimientos;
- información operacional.

## Exploratorio

El jugador puede escribir cualquier cosa y experimentar.

La campaña normal nunca debería exigir descubrir accidentalmente un comando secreto.

Todo lo necesario para completar la misión normal debe estar disponible en el cheatsheet.

El cheatsheet puede contener comandos adicionales que no son necesarios.

---

# 10. TERMINAL

La terminal es el sistema de interacción universal.

El jugador puede escribir cualquier texto.

Ejemplo:

```text
FIRE G7
RELOAD
DEFENSE G4
SONAR
```

Pero también:

```text
HELLO
WHAT IS THIS
TEST
G7
RETRY
```

Todos los inputs quedan registrados en el historial.

---

## Input válido

Si el texto corresponde a un comando/interacción implementada:

```text
INPUT
 ↓
RECOGNIZED
 ↓
ACTION
```

---

## Input desconocido

Si no existe una interacción asociada:

```text
INPUT
 ↓
UNRECOGNIZED
 ↓
NO EFFECT
 ↓
LOGGED
```

No debe aparecer necesariamente:

```text
INVALID COMMAND
```

El input simplemente no hace nada.

Esto permite experimentación.

---

# 11. COMMAND SYSTEM

El conjunto de comandos representa el lenguaje operativo del mundo.

Categorías:

### Combat

- FIRE
- targeting/coordinate commands

### Defense

- despliegue de defensas;
- interacción con defensas.

### Systems

- SONAR;
- WEAPONS;
- RELOAD;
- recuperación de sistemas.

### Communication

- interacción con tripulación;
- inputs conversacionales.

### Mission

- comandos relacionados con objetivos.

### Hidden

- comandos descubiertos mediante exploración.

La lista definitiva de comandos es una decisión de diseño pendiente que deberá quedar documentada en el TDD.

---

# 12. SONAR

El sonar ocupa aproximadamente **70% de la pantalla**.

Es simultáneamente:

- herramienta de gameplay;
- principal elemento visual;
- instrumento diegético;
- fuente de información;
- vehículo narrativo.

El submarino del jugador permanece siempre en el centro del sonar.

El submarino puede moverse físicamente dentro de la historia, pero **su movimiento no es una mecánica de gameplay**.

---

# 13. SONAR SWEEP

El sonar realiza un barrido continuo y automático.

El jugador puede:

- detenerlo;
- reanudarlo;
- volver a activar el barrido automático.

El sonar puede detenerse automáticamente por razones:

- narrativas;
- de gameplay;
- relacionadas con daño;
- relacionadas con la investigación.

Cuando el juego detenga automáticamente el sonar, debe quedar claro mediante la presentación diegética que el jugador puede/debe reactivarlo manualmente.

---

# 14. SONAR INFORMATION

El sonar proporciona información relativamente completa.

Debe sentirse inspirado en un sonar real, pero diseñado alrededor de la diversión.

Los contactos pueden diferenciarse visualmente según su naturaleza:

- enemigos;
- misiles enemigos;
- misiles propios;
- defensas propias;
- otros contactos relevantes;
- contactos anómalos.

La diferenciación puede utilizar:

- forma;
- persistencia;
- intensidad;
- comportamiento;
- patrones de parpadeo;
- trayectoria.

No se utilizará un sistema de colores configurables por el jugador como mecánica principal.

---

# 15. SONAR MEMORY

El sonar debe permitir al jugador interpretar posiciones sucesivas.

El jugador observa:

```text
CONTACT A
POSITION 1
↓
POSITION 2
↓
POSITION 3
```

y deduce:

- dirección;
- velocidad;
- trayectoria;
- posible punto de intercepción.

La simulación debe ser suficientemente rápida para resultar satisfactoria visualmente.

---

# 16. COORDINATE SYSTEM

El juego utilizará una representación espacial basada en coordenadas.

Ejemplo:

```text
G7
```

El jugador puede escribir:

```text
FIRE G7
```

para lanzar un proyectil hacia esa posición.

El diseño puede contemplar más de un sistema de coordenadas, por ejemplo:

- alfanumérico;
- angular.

El cambio entre sistemas es una posible función de la terminal y queda pendiente de especificación final.

---

# 17. PROJECTILES

Los proyectiles tienen:

- posición;
- dirección;
- velocidad;
- tiempo de viaje;
- trayectoria;
- colisión.

Un proyectil no impacta instantáneamente.

Por lo tanto, si el enemigo se mueve:

```text
Enemy current position ≠ future impact position
```

El jugador debe anticipar dónde estará el objetivo.

Esta es una de las principales habilidades del gameplay.

---

# 18. IMPACT MODEL

Los impactos son directos.

No existe daño gradual en los objetivos convencionales.

Un impacto válido destruye inmediatamente al objetivo.

No hay HP visible.

---

# 19. ENEMY SURVIVAL

Los submarinos/objetivos enemigos convencionales son:

> **ONE HIT KILL**

Un impacto directo los destruye.

---

# 20. PLAYER SURVIVAL

El submarino del jugador es:

> **TWO HIT KILL**

Primer impacto:

- sobrevive;
- recibe daño crítico;
- uno o más sistemas se desactivan;
- debe restaurarlos manualmente.

Segundo impacto:

- destrucción;
- derrota;
- `Retry?`.

Esto constituye una anomalía potencial dentro de la narrativa, porque los objetivos enemigos convencionales no tienen el mismo margen de supervivencia.

---

# 21. DAMAGE STATES

El primer impacto puede desactivar sistemas como:

- sonar;
- armamento;
- reload;
- defensa;
- otros sistemas relevantes.

Los sistemas afectados pueden variar.

El jugador debe recuperarlos manualmente.

Ejemplo:

```text
T2:
Weapons are down.

T3:
Sonar's gone too.

T1:
Get them back online.
```

El jugador utiliza la terminal para restaurarlos.

El combate no se pausa.

---

# 22. AMMUNITION

La munición es limitada.

El jugador debe administrarla.

El objetivo no es hacer que la munición sea tan escasa que una mala decisión bloquee la partida.

Quedarse sin munición es una situación recuperable.

El jugador puede:

- sobrevivir;
- utilizar defensas;
- esperar;
- reabastecerse;
- recuperar recursos.

La administración de recursos debe crear decisiones, no soft-locks.

---

# 23. RELOAD

El reload es una mecánica importante.

T2 es responsable de la recarga.

La recarga puede ser iniciada mediante terminal.

La operación ocurre en tiempo real.

El jugador debe considerar:

- munición disponible;
- tiempo de reload;
- amenazas actuales;
- necesidad de disparar inmediatamente.

Los valores exactos serán determinados durante balance/testing.

---

# 24. DEFENSIVE SYSTEM

El jugador puede desplegar una defensa mediante terminal.

La defensa:

1. se coloca en una coordenada;
2. se desplaza hacia ella;
3. permanece allí;
4. interactúa con objetos que entren en contacto con ella.

Puede interactuar con:

- misiles enemigos;
- proyectiles propios;
- enemigos;
- otros objetos según diseño final.

Una colisión puede destruir ambos objetos.

---

# 25. DEFENSE AS A NARRATIVE ANOMALY

La defensa es también una potencial evidencia del misterio.

Teóricamente:

> Si el submarino se mueve, una defensa física debería mantener una relación espacial coherente con él.

Sin embargo, el sistema puede parecer moverse junto al submarino de una manera que no debería ser posible.

Esto debe poder pasar desapercibido inicialmente.

Posteriormente puede convertirse en evidencia de que las reglas físicas del submarino no son normales.

---

# 26. ENEMY DIFFICULTY

La dificultad aumenta mediante conocimiento y dominio.

No se pretende crear una gran cantidad de sistemas nuevos.

La complejidad puede aumentar mediante:

- más enemigos;
- enemigos más rápidos;
- diferentes trayectorias;
- ataques simultáneos;
- misiles;
- defensas;
- recursos;
- tiempos de reacción;
- interrupciones del sonar;
- combinaciones.

---

# 27. ENEMY COUNT

Valores iniciales provisionales:

| Situación                 | Contactos simultáneos |
| ------------------------- | --------------------: |
| Tutorial                  |                     1 |
| Inicio                    |                   1–3 |
| Desarrollo                |                   3–5 |
| Alta tensión              |                   5–7 |
| Máximo normal provisional |                  \~10 |
| Misterio                  |                 \~20+ |

Estos valores son **variables de balance**, no requisitos definitivos.

La cifra final será determinada mediante testing.

---

# 28. TIME

El gameplay ocurre en tiempo real.

Mientras el jugador escribe:

- enemigos se mueven;
- proyectiles avanzan;
- defensas permanecen activas;
- eventos continúan.

No existe pausa automática al abrir la terminal.

Momentos narrativos pueden modificar temporalmente la situación cuando sea necesario por diseño, pero esto debe sentirse como un evento del mundo, no como una regla arbitraria del gameplay.

---

# 29. SPEEDRUNNING

El tiempo de misión es una propiedad importante del juego.

El jugador no necesita ver permanentemente un cronómetro HUD.

El tiempo se presenta diegéticamente.

Al completar la misión:

```text
T1:
How long?

T2:
[MISSION TIME]

T1:
...
```

El juego puede almacenar internamente:

- tiempo total;
- récord personal;
- tiempos de runs;
- estadísticas.

El objetivo es que el jugador quiera:

> jugar mejor → cometer menos errores → completar la misión más rápido.

---

# 30. CREW

La tripulación consta de cuatro personas:

1. **Player**
2. **T1 — Captain**
3. **T2 — Reload / Armament**
4. **T3 — Support**

Los identificadores T1/T2/T3 aparecen en el historial.

No existe un único NPC que monopolice las conversaciones.

La comunicación representa conversaciones entre miembros de la tripulación.

---

# 31. T1 — CAPTAIN

Funciones:

- órdenes;
- contexto;
- dirección de la misión;
- decisiones operacionales;
- conversaciones.

T1 tendrá personalidad fuerte.

---

# 32. T2 — RELOAD / ARMAMENT

Funciones:

- recarga;
- munición;
- estado de armas;
- observaciones relacionadas con combate.

T2 tendrá personalidad fuerte.

---

# 33. T3 — SUPPORT

Funciones:

- comunicaciones;
- observaciones;
- información;
- potenciales anomalías;
- interacción con eventos.

T3 tendrá personalidad fuerte.

---

# 34. CREW CONVERSATIONS

Los tres personajes tienen conversaciones opcionales.

El jugador puede descubrir conversaciones secretas mediante exploración de la terminal.

No existe un menú explícito de "secret conversations".

La terminal debe permitir experimentar con inputs.

Las conversaciones pueden depender de:

- input;
- contexto;
- progreso;
- eventos;
- pistas descubiertas;
- respuestas anteriores.

---

# 35. SECRET INPUTS

Los inputs secretos deben surgir de información disponible dentro del juego.

No se debe exigir una contraseña arbitraria sin pistas.

El jugador debe poder llegar a una solución mediante:

- observación;
- deducción;
- exploración;
- conexiones entre información.

---

# 36. HISTORIAL

El historial cumple múltiples funciones:

### Operativa

Registra comandos y respuestas.

### Narrativa

Registra comunicaciones de la tripulación.

### Investigativa

Conserva inputs experimentales.

### Misterio

Permite descubrir conversaciones y eventos que de otro modo podrían pasar desapercibidos.

Todo input del jugador queda registrado.

---

# 37. STORY DESIGN PHILOSOPHY

El juego debe evitar exposición directa innecesaria.

La historia se divide en:

### Historia explícita

Lo que la tripulación conoce:

- misión;
- órdenes;
- combate;
- objetivo;
- contexto militar.

### Historia implícita

Lo que el jugador puede deducir:

- anomalías;
- inconsistencias;
- comportamiento extraño;
- intervención de la entidad;
- naturaleza del submarino;
- relación con el tiempo.

### Historia secreta

Lo que solamente se puede descubrir mediante investigación.

---

# 38. NORMAL ENDING

Si el jugador:

- no descubre las pistas;
- descubre solo algunas;
- activa algunas pero no completa las tres;
- no logra acceder a la fase secreta;

la misión continúa por la ruta normal.

El jugador destruye la base.

Los Aliados cumplen el objetivo.

La guerra continúa de acuerdo con la historia.

El final es deliberadamente agridulce.

El jugador sabe, por el contexto histórico y la época, lo que probablemente viene después.

El juego no necesita explicarlo explícitamente.

---

# 39. DEFEAT ENDING

Si el jugador pierde la misión:

- el sonar desaparece;
- la pantalla queda negra;
- aparece `Retry?`;
- el jugador puede escribir `RETRY`.

La derrota debe implicar una línea temporal donde el objetivo militar no se cumple.

Se sugiere que:

- Europa fue ganada por los Aliados;
- Asia no;
- Japón continúa la guerra;
- la historia toma un rumbo alternativo.

El detalle exacto de esta línea temporal queda pendiente de la story bible.

---

# 40. MYSTERY STRUCTURE

Existen cinco pistas principales.

## Pistas 1–3

Las tres ocurren independientemente durante la campaña.

Una no desbloquea a otra.

El jugador puede descubrirlas en cualquier orden.

Las tres deben ser:

1. activadas;
2. comprendidas/resueltas;
3. registradas como completadas.

Cuando las tres están completas:

```text
CLUE 1 ✓
CLUE 2 ✓
CLUE 3 ✓
```

internamente se habilita la fase 2.

El juego no debe mostrar necesariamente:

```text
3/3 CLUES
```

al jugador.

---

# 41. CLUE 1 — SONAR PATTERN

Concepto:

Durante un sweep aparecen numerosos puntos.

La disposición de esos puntos puede traducirse mediante algún sistema, potencialmente relacionado con Braille u otro método.

La traducción produce información utilizable.

Ejemplo conceptual:

```text
SONAR PATTERN
      ↓
PATTERN RECOGNITION
      ↓
TRANSLATION
      ↓
COORDINATE
      ↓
TERMINAL INPUT
      ↓
EVENT
```

El método exacto de codificación debe diseñarse posteriormente.

La pista debe requerir observación y deducción.

---

# 42. CLUE 2 — ANOMALOUS SHIP

Durante la misión aparece una nave/contacto que:

- no se comporta normalmente;
- puede no atacar;
- tiene una función narrativa oculta.

El jugador debe interactuar con ella de una manera específica.

Una posibilidad ya planteada es:

> destruirla o decidir no destruirla.

La condición exacta queda pendiente de diseño y testing narrativo.

---

# 43. CLUE 3 — RADIO INTERCEPTION

El jugador intercepta una comunicación de otro submarino.

La comunicación contiene información que puede conducir a:

- determinadas coordenadas;
- ataques;
- puntos aparentemente vacíos;
- acciones que no tienen sentido militar evidente.

El jugador debe actuar sobre esa información.

El resultado demuestra que los puntos "vacíos" tenían algún significado.

---

# 44. PHASE 2

Al completar las tres primeras pistas:

> la operación comienza a desviarse.

La mecánica fundamental no cambia.

El jugador sigue:

- leyendo sonar;
- calculando trayectorias;
- escribiendo coordenadas;
- disparando;
- defendiendo;
- recargando.

Pero el significado de lo que está viendo empieza a romperse.

---

# 45. PHASE 2 ANOMALIES

Posibles comportamientos:

- cantidades anormales de enemigos;
- contactos imposibles;
- sonar detenido;
- información contradictoria;
- objetivos que no parecen militares;
- trayectorias extrañas;
- sistemas que funcionan cuando deberían estar apagados;
- comportamientos que contradicen las reglas previamente establecidas.

El juego debe mantener la misma interacción fundamental.

La anomalía está en **qué está haciendo el sistema**, no en cambiar el juego por otro.

---

# 46. CLUE 4

Pista 4 ocurre dentro de la fase 2.

Puede utilizar una combinación de:

- múltiples contactos;
- identificación de determinados objetivos;
- información obtenida previamente;
- coordenadas;
- patrones;
- conversación;
- comportamiento anómalo.

Una idea central es que aparezcan alrededor de 15 contactos y el jugador deba atacar exactamente tres determinados.

La mecánica final queda abierta para diseño.

Completar la pista 4 habilita el camino hacia la pista 5.

---

# 47. CLUE 5 / EXTREME SURVIVAL

Después de la pista 4, el juego entra en una situación de dificultad extrema.

Puede aparecer un escenario tipo bullet hell.

El número de contactos puede aumentar significativamente, potencialmente alrededor de 20 o más.

El objetivo es sobrevivir utilizando las mismas mecánicas.

La dificultad debe ser:

- extrema;
- aprendible;
- consistente;
- basada en ejecución;
- no arbitraria.

La pista 5 puede no sentirse como una pista.

Puede ser simplemente:

> **sobrevivir a algo que debería ser imposible.**

---

# 48. SECRET ENDING

Al completar las condiciones necesarias:

- pistas 1–3;
- fase 2;
- pista 4;
- supervivencia/evento de pista 5;

el jugador accede al final secreto.

Este final revela información adicional sobre:

- el estado real del submarino;
- la entidad;
- la intervención durante la guerra;
- la naturaleza de los eventos;
- el significado de las anomalías;
- las implicaciones de que los Aliados hayan recibido ayuda de algo desconocido.

La explicación debe mantener un grado de ambigüedad.

---

# 49. COSMIC HORROR

El horror es:

> **Psychological Cosmic Horror.**

No se busca depender principalmente de:

- jumpscares;
- monstruos visibles;
- gore;
- imágenes explícitas.

El miedo surge de:

- inconsistencias;
- pérdida de confianza en el sistema;
- imposibilidad física;
- contradicciones;
- tiempo;
- identidad;
- causalidad;
- intervención de algo incomprensible.

---

# 50. THE ENTITY

La entidad:

- no se presenta inmediatamente;
- no tiene por qué aparecer físicamente;
- no debe explicarse completamente;
- parece intervenir en la operación;
- parece ayudar al jugador/Aliados;
- plantea una pregunta moral y existencial.

Pregunta central:

> **¿Qué significa que los Aliados hayan recibido ayuda de algo que no deberían haber invocado?**

La naturaleza exacta, motivación y origen de la entidad deben permanecer parcialmente ambiguos.

---

# 51. NARRATIVE ANOMALIES

Las anomalías deben reutilizar sistemas existentes.

Ejemplos:

### Submarine survival

Los enemigos mueren de un impacto.

El submarino sobrevive a uno.

¿Por qué?

### Defense movement

Una defensa debería permanecer físicamente donde fue colocada.

Sin embargo, parece acompañar al submarino.

### Retry

¿Quién escribe `Retry?`?

### Time

El tiempo percibido puede no coincidir con otros registros.

### Systems

Un sistema puede funcionar cuando debería estar inutilizado.

### Sonar

El sonar puede detectar cosas que no deberían existir.

---

# 52. AUDIO

El audio es fundamental.

Debe existir sin depender obligatoriamente de una biblioteca musical/sonora externa.

Se prioriza generación procedural cuando sea viable.

Elementos:

- sonar ping;
- sweep;
- detección;
- proyectiles;
- impactos;
- explosiones;
- alarmas;
- sistemas;
- radio;
- voz;
- ambiente;
- tensión.

Godot puede utilizar `AudioStreamGenerator` u otros sistemas adecuados.

La arquitectura exacta será decidida en TDD.

---

# 53. VISUAL DIRECTION

El presupuesto visual es deliberadamente mínimo.

La pantalla está dominada por:

- negro;
- sonar;
- sweep;
- puntos;
- trayectorias;
- terminal;
- historial.

El estilo visual debe recordar a instrumentos militares reales, no a una UI genérica de videojuego.

El sonar debe sentirse como:

> **un instrumento real que accidentalmente constituye todo el juego.**

---

# 54. UI LAYOUT

Distribución aproximada:

```text
┌─────────────────────────────────────────────┐
│                  TOP UI                     │
├─────────────────────────────┬───────────────┤
│                             │               │
│                             │   TERMINAL    │
│                             │   HISTORY     │
│                             │   DIALOGUE    │
│          SONAR              │               │
│                             │               │
│                             │               │
│                             │               │
│                             │               │
├─────────────────────────────┴───────────────┤
│              TERMINAL INPUT                 │
└─────────────────────────────────────────────┘
```

Aproximadamente:

- sonar: 70%;
- terminal: 10%;
- history/dialogue/extras: 20%;
- top UI: transversal.

Las proporciones finales se ajustarán durante UX testing.

---

# 55. GAME FLOW

```text
MAIN MENU
   ↓
SONAR
   ↓
SYSTEM FAILURE
   ↓
RECOVERY
   ↓
CREW INTRODUCTION
   ↓
TUTORIAL
   ↓
MISSION
   ↓
ESCALATION
   ↓
┌─────────────────────────────┐
│ Player investigates?        │
└──────────────┬──────────────┘
               │
       ┌───────┴───────┐
       │               │
      NO              YES
       │               │
       ▼               ▼
 NORMAL ROUTE     CLUES 1–3
       │               │
       │         ┌─────┴─────┐
       │         │           │
       │       <3/3         3/3
       │         │           │
       │         ▼           ▼
       │       NORMAL      PHASE 2
       │                     │
       │                   CLUE 4
       │                     │
       │                   CLUE 5
       │                     │
       │               SECRET END
       │
       ▼
  MISSION END
```

---

# 56. PLAYER MOTIVATION

El juego debe sostener varias motivaciones simultáneamente.

### First motivation

> Complete the mission.

### Second motivation

> Become better at operating the submarine.

### Third motivation

> Finish faster.

### Fourth motivation

> Understand the strange things that happened.

### Fifth motivation

> Discover everything.

### Sixth motivation

> Complete the secret challenge.

La primera partida no debe exigir las motivaciones posteriores.

---

# 57. REPLAYABILITY

Replayability surge principalmente de:

- dominio;
- optimización;
- speedrunning;
- experimentación;
- descubrimiento;
- conversaciones secretas;
- mystery solving.

No se requiere procedural generation como requisito fundamental.

La variación procedural puede considerarse posteriormente si mejora la experiencia sin perjudicar el diseño narrativo.

---

# 58. CONTENT COMPLETENESS

El juego debe considerarse completo en el lanzamiento cuando el jugador pueda:

- completar la misión normal;
- perder y utilizar RETRY;
- experimentar todas las mecánicas principales;
- completar la campaña normal;
- descubrir las cinco pistas;
- completar la fase 2;
- alcanzar el final secreto;
- acceder a todas las conversaciones secretas diseñadas;
- recorrer todas las branches previstas.

No se planea contenido narrativo adicional posterior al lanzamiento.

---

# 59. SCOPE PRINCIPLE

No se deben añadir sistemas únicamente para aumentar contenido.

Toda nueva mecánica debe justificar:

1. su contribución al gameplay;
2. su contribución narrativa;
3. su compatibilidad con el núcleo sonar/terminal;
4. su coste de implementación;
5. su necesidad para la experiencia.

---

# 60. SOFTWARE ARCHITECTURE INTENT

La arquitectura técnica deberá separar estrictamente:

```text
GODOT
Frontend / Presentation
       │
       ▼
CONTROLLER
Boundary / Translation
       │
       ▼
C# / .NET
Game Logic / Simulation
```

---

## Godot

Responsable de:

- rendering;
- sonar visual;
- UI;
- terminal visual;
- history visual;
- audio presentation;
- input collection;
- animation;
- presentation state.

Godot no debe conocer detalles internos del gameplay backend.

---

## Controller

Responsable de:

- recibir input desde Godot;
- traducirlo al dominio;
- invocar el backend;
- convertir resultados del backend en datos utilizables por Godot;
- controlar la frontera entre frontend y backend.

El Controller es conocido por Godot y por el backend.

---

## C# / .NET Backend

Responsable de:

- reglas del juego;
- simulación;
- combate;
- enemigos;
- proyectiles;
- defensa;
- recursos;
- comandos;
- historia;
- estado;
- pistas;
- retry;
- AI;
- time;
- validación de reglas.

El backend **no debe depender de Godot**.

Debe poder ejecutarse/testearse independientemente de la presentación.

---

# 61. DATA-DRIVEN DESIGN

Los valores de gameplay deberían, cuando resulte conveniente, ser configurables sin modificar lógica.

Ejemplos:

- velocidad de proyectiles;
- cantidad de munición;
- reload time;
- cantidad de enemigos;
- velocidad enemiga;
- detección;
- posiciones;
- duración de eventos;
- condiciones de pistas;
- tiempos;
- dificultad.

La estrategia concreta de configuración queda para el TDD.

---

# 62. DETERMINISM

Debe evaluarse si la simulación debe ser:

- determinista;
- parcialmente determinista;
- no determinista.

Esta decisión es especialmente relevante para:

- speedrunning;
- testing;
- replay;
- debugging;
- mystery consistency.

---

# 63. DESIGN-TO-TECHNICAL TODO

Esta sección **no es el TDD**.

Es el inventario de decisiones que el TDD deberá resolver.

---

## 63.1 Architecture

- [ ] Definir estructura exacta de proyectos Godot/C#.
- [ ] Definir solución `.sln`.
- [ ] Definir assemblies.
- [ ] Definir dependencias entre assemblies.
- [ ] Definir boundary Controller.
- [ ] Definir interfaces públicas.
- [ ] Definir ownership de estado.
- [ ] Definir lifecycle.
- [ ] Definir initialization order.
- [ ] Definir shutdown.
- [ ] Definir dependency injection, si se utiliza.
- [ ] Definir composition root.
- [ ] Definir módulos.
- [ ] Definir namespaces.
- [ ] Definir project references.
- [ ] Definir reglas de dependencia.
- [ ] Definir cómo se evita que C# dependa accidentalmente de Godot.

---

## 63.2 Game Loop

- [ ] Definir tick model.
- [ ] Definir fixed timestep vs variable timestep.
- [ ] Definir simulation update order.
- [ ] Definir rendering update order.
- [ ] Definir input processing order.
- [ ] Definir command processing timing.
- [ ] Definir collision update timing.
- [ ] Definir sonar update timing.
- [ ] Definir AI update timing.
- [ ] Definir event processing order.
- [ ] Definir comportamiento durante frames largos.
- [ ] Definir catch-up behavior.

---

## 63.3 Time

- [ ] Definir authoritative game clock.
- [ ] Definir mission timer.
- [ ] Definir simulation time.
- [ ] Definir narrative time.
- [ ] Definir relationship entre clocks.
- [ ] Definir precisión del timer.
- [ ] Definir pause policy.
- [ ] Definir slowdown policy.
- [ ] Definir cómo se manejan eventos narrativos durante tiempo real.
- [ ] Definir cómo se almacena el tiempo para speedrunning.

---

## 63.4 Domain Model

- [ ] Definir `Submarine`.
- [ ] Definir `Enemy`.
- [ ] Definir `Projectile`.
- [ ] Definir `Defense`.
- [ ] Definir `SonarContact`.
- [ ] Definir `Weapon`.
- [ ] Definir `Ammo`.
- [ ] Definir `CrewMember`.
- [ ] Definir `Mission`.
- [ ] Definir `MissionObjective`.
- [ ] Definir `GameState`.
- [ ] Definir `SystemState`.
- [ ] Definir `DamageState`.
- [ ] Definir `ClueState`.
- [ ] Definir `ConversationState`.
- [ ] Definir `RetryState`.

---

## 63.5 Coordinate System

- [ ] Definir world coordinate representation.
- [ ] Definir sonar coordinate representation.
- [ ] Definir alphanumeric grid.
- [ ] Definir angular coordinates.
- [ ] Definir conversion.
- [ ] Definir precision.
- [ ] Definir out-of-bounds behavior.
- [ ] Definir coordinate parsing.
- [ ] Definir coordinate display.
- [ ] Definir rounding.

---

## 63.6 Sonar

- [ ] Definir sonar radius.
- [ ] Definir sweep speed.
- [ ] Definir sweep resolution.
- [ ] Definir detection frequency.
- [ ] Definir detection persistence.
- [ ] Definir fading.
- [ ] Definir contact classification.
- [ ] Definir signal representation.
- [ ] Definir noise.
- [ ] Definir interference.
- [ ] Definir anomalous contacts.
- [ ] Definir sonar disabled state.
- [ ] Definir manual restart.
- [ ] Definir automatic sweep restart.
- [ ] Definir contact update frequency.
- [ ] Definir visual trajectory representation.
- [ ] Definir sonar performance budget.

---

## 63.7 Combat

- [ ] Define projectile abstraction.
- [ ] Define projectile velocity.
- [ ] Define projectile acceleration if any.
- [ ] Define projectile lifetime.
- [ ] Define collision model.
- [ ] Define hit detection.
- [ ] Define simultaneous collisions.
- [ ] Define friendly fire.
- [ ] Define projectile/projectile collision.
- [ ] Define projectile/defense collision.
- [ ] Define projectile/enemy collision.
- [ ] Define destruction ordering.
- [ ] Define explosion behavior.
- [ ] Define visual/audio feedback.

---

## 63.8 Damage

- [ ] Define player first-hit behavior.
- [ ] Define system damage selection.
- [ ] Define deterministic vs random damage.
- [ ] Define system disable duration if any.
- [ ] Define manual restoration commands.
- [ ] Define second-hit destruction.
- [ ] Define enemy destruction.
- [ ] Define simultaneous player/enemy destruction.
- [ ] Define death state transition.
- [ ] Define post-death simulation behavior.

---

## 63.9 Weapons

- [ ] Define attack weapon(s).
- [ ] Define defensive weapon/system(s).
- [ ] Define utility weapons/tools.
- [ ] Define ammunition types.
- [ ] Define capacities.
- [ ] Define consumption.
- [ ] Define reload.
- [ ] Define reload interruption.
- [ ] Define reload failure.
- [ ] Define resource restoration.
- [ ] Define weapon state machine.
- [ ] Define unavailable weapon behavior.

---

## 63.10 Defense

- [ ] Define defense placement.
- [ ] Define defense movement.
- [ ] Define defense persistence.
- [ ] Define defense collision.
- [ ] Define defense destruction.
- [ ] Define defense capacity.
- [ ] Define defense ammunition/resource cost.
- [ ] Define defense interaction with friendly projectiles.
- [ ] Define defense interaction with enemy projectiles.
- [ ] Define anomalous movement behavior.

---

## 63.11 Enemy AI

- [ ] Define enemy states.
- [ ] Define detection.
- [ ] Define movement.
- [ ] Define target selection.
- [ ] Define attack timing.
- [ ] Define missile behavior.
- [ ] Define defensive behavior.
- [ ] Define special enemies.
- [ ] Define movement patterns.
- [ ] Define maximum concurrent enemies.
- [ ] Define spawn/despawn rules.
- [ ] Define sonar visibility.
- [ ] Define AI update frequency.
- [ ] Define deterministic behavior.
- [ ] Define mystery-phase behavior.

---

## 63.12 Terminal

- [ ] Define terminal input model.
- [ ] Define command parser.
- [ ] Define tokenizer.
- [ ] Define normalization.
- [ ] Define case sensitivity.
- [ ] Define whitespace behavior.
- [ ] Define argument parsing.
- [ ] Define command registry.
- [ ] Define command aliases.
- [ ] Define unknown input behavior.
- [ ] Define command history.
- [ ] Define command timestamps.
- [ ] Define command execution timing.
- [ ] Define asynchronous commands.
- [ ] Define terminal output model.
- [ ] Define command errors.
- [ ] Define secret commands.
- [ ] Define context-sensitive commands.
- [ ] Define conversation inputs.

---

## 63.13 Conversations

- [ ] Define dialogue data format.
- [ ] Define speaker identification.
- [ ] Define T1/T2/T3 IDs.
- [ ] Define dialogue state.
- [ ] Define branching.
- [ ] Define conditions.
- [ ] Define flags.
- [ ] Define optional conversations.
- [ ] Define secret conversations.
- [ ] Define input-triggered conversations.
- [ ] Define timing.
- [ ] Define interruptions.
- [ ] Define concurrent combat dialogue.
- [ ] Define persistence across retry.
- [ ] Define whether dialogue state resets on retry.

---

## 63.14 Narrative State

- [ ] Define mission state machine.
- [ ] Define narrative event system.
- [ ] Define event triggers.
- [ ] Define one-time events.
- [ ] Define repeatable events.
- [ ] Define failed events.
- [ ] Define clue flags.
- [ ] Define secret phase unlock.
- [ ] Define ending conditions.
- [ ] Define narrative persistence.
- [ ] Define timeline representation.

---

## 63.15 Clue System

- [ ] Define clue representation.
- [ ] Define activation.
- [ ] Define resolution.
- [ ] Define completion state.
- [ ] Define clue 1.
- [ ] Define clue 2.
- [ ] Define clue 3.
- [ ] Define phase 2 trigger.
- [ ] Define clue 4.
- [ ] Define clue 5.
- [ ] Define failure conditions.
- [ ] Define replay behavior.
- [ ] Define hidden flags.
- [ ] Define anti-brute-force rules.

---

## 63.16 Retry

- [ ] Define death transition.
- [ ] Define reset scope.
- [ ] Define persistent state.
- [ ] Define clue persistence.
- [ ] Define conversation persistence.
- [ ] Define discovered secrets persistence.
- [ ] Define mission reset.
- [ ] Define RNG reset.
- [ ] Define timer reset.
- [ ] Define `Retry?` event.
- [ ] Define `RETRY` command.
- [ ] Define behavior after invalid retry input.

---

## 63.17 Save System

- [ ] Decide whether game saves automatically.
- [ ] Define save points.
- [ ] Define manual save policy.
- [ ] Define mission state persistence.
- [ ] Define discovery persistence.
- [ ] Define conversation persistence.
- [ ] Define settings persistence.
- [ ] Define versioning.
- [ ] Define migration.
- [ ] Define corruption handling.
- [ ] Define Steam Cloud support.
- [ ] Define save compatibility after patches.

---

## 63.18 RNG

- [ ] Decide whether combat is deterministic.
- [ ] Define RNG source.
- [ ] Define seeds.
- [ ] Define seed persistence.
- [ ] Define reproducibility.
- [ ] Define random enemy behavior.
- [ ] Define random system damage.
- [ ] Define random anomaly timing.
- [ ] Define whether mystery events can vary.

---

## 63.19 Audio

- [ ] Define audio architecture.
- [ ] Define audio buses.
- [ ] Define volume categories.
- [ ] Define sonar sound generation.
- [ ] Define projectile audio.
- [ ] Define impact audio.
- [ ] Define alarm audio.
- [ ] Define radio processing.
- [ ] Define procedural synthesis.
- [ ] Define voice architecture.
- [ ] Define spatial audio.
- [ ] Define dynamic mixing.
- [ ] Define audio state transitions.
- [ ] Define audio behavior during sonar failure.

---

## 63.20 UI

- [ ] Define UI architecture.
- [ ] Define sonar rendering.
- [ ] Define terminal rendering.
- [ ] Define history rendering.
- [ ] Define top-level UI.
- [ ] Define input focus.
- [ ] Define scrolling.
- [ ] Define text rendering.
- [ ] Define font.
- [ ] Define responsive layout.
- [ ] Define resolution handling.
- [ ] Define ultrawide behavior.
- [ ] Define minimum resolution.
- [ ] Define accessibility options.
- [ ] Define visual effects.
- [ ] Define flashing intensity.
- [ ] Define readability.

---

## 63.21 Input

- [ ] Define keyboard controls.
- [ ] Define mouse usage.
- [ ] Define terminal focus.
- [ ] Define input buffering.
- [ ] Define text input behavior.
- [ ] Define key repeat.
- [ ] Define input remapping.
- [ ] Define controller support, if any.
- [ ] Define keyboard layouts.
- [ ] Define special characters.
- [ ] Define coordinate input shortcuts.

---

## 63.22 Accessibility

- [ ] Define text scaling.
- [ ] Define font readability.
- [ ] Define contrast.
- [ ] Define flashing effects toggle.
- [ ] Define audio cue alternatives.
- [ ] Define subtitle behavior.
- [ ] Define color-independent contact identification.
- [ ] Define reduced motion.
- [ ] Define input accessibility.

---

## 63.23 Performance

- [ ] Define target FPS.
- [ ] Define supported hardware.
- [ ] Define CPU budget.
- [ ] Define rendering budget.
- [ ] Define sonar rendering budget.
- [ ] Define maximum simultaneous contacts.
- [ ] Define maximum projectiles.
- [ ] Define maximum UI history size.
- [ ] Define audio voice limits.
- [ ] Define memory budget.
- [ ] Define profiling methodology.

---

## 63.24 Testing

- [ ] Unit test architecture.
- [ ] Integration test architecture.
- [ ] Simulation tests.
- [ ] Command parser tests.
- [ ] Terminal tests.
- [ ] Coordinate tests.
- [ ] Projectile tests.
- [ ] Collision tests.
- [ ] Damage tests.
- [ ] Reload tests.
- [ ] Sonar tests.
- [ ] AI tests.
- [ ] Narrative state tests.
- [ ] Clue tests.
- [ ] Retry tests.
- [ ] Save/load tests.
- [ ] Determinism tests.
- [ ] End-to-end tests.

---

## 63.25 Debugging

- [ ] Debug command system.
- [ ] Debug sonar contacts.
- [ ] Spawn enemy command.
- [ ] Spawn projectile command.
- [ ] Teleport/coordinate debug.
- [ ] Force damage.
- [ ] Force system failure.
- [ ] Complete clue.
- [ ] Reset clue.
- [ ] Trigger narrative event.
- [ ] Force ending.
- [ ] Fast-forward time.
- [ ] Inspect game state.
- [ ] Inspect RNG state.

---

## 63.26 Development Tools

- [ ] Define developer console.
- [ ] Define debug overlays.
- [ ] Define scenario runner.
- [ ] Define test scenarios.
- [ ] Define automated combat simulations.
- [ ] Define balance data editing workflow.
- [ ] Define dialogue testing workflow.
- [ ] Define clue testing workflow.
- [ ] Define replay/debug workflow.

---

## 63.27 Build System

- [ ] Define .NET version.
- [ ] Define Godot version.
- [ ] Define package management.
- [ ] Define build configuration.
- [ ] Define Debug build.
- [ ] Define Development build.
- [ ] Define Release build.
- [ ] Define platform targets.
- [ ] Define build artifacts.
- [ ] Define reproducible builds.
- [ ] Define dependency locking.

---

## 63.28 CI/CD

- [ ] Define repository structure.
- [ ] Define branch strategy.
- [ ] Define pull request requirements.
- [ ] Define automated tests.
- [ ] Define linting.
- [ ] Define formatting.
- [ ] Define static analysis.
- [ ] Define build validation.
- [ ] Define release pipeline.
- [ ] Define artifact storage.
- [ ] Define version tagging.

---

## 63.29 Code Quality

- [ ] Define C# coding standard.
- [ ] Define naming conventions.
- [ ] Define file organization.
- [ ] Define namespace conventions.
- [ ] Define nullability policy.
- [ ] Define exception policy.
- [ ] Define logging policy.
- [ ] Define comments/documentation standard.
- [ ] Define dependency restrictions.
- [ ] Define Godot/C# boundary enforcement.

---

## 63.30 Data and Content

- [ ] Define data serialization format.
- [ ] Define enemy definitions.
- [ ] Define weapon definitions.
- [ ] Define dialogue definitions.
- [ ] Define narrative events.
- [ ] Define clue definitions.
- [ ] Define mission configuration.
- [ ] Define balance configuration.
- [ ] Define localization format.
- [ ] Define validation of content data.

---

## 63.31 Localization

- [ ] Decide supported languages.
- [ ] Define string extraction.
- [ ] Define text IDs.
- [ ] Define terminal localization.
- [ ] Define dialogue localization.
- [ ] Define dynamic text.
- [ ] Define coordinate/input localization constraints.
- [ ] Define font support.
- [ ] Define text expansion handling.

---

## 63.32 Steam

- [ ] Define Steam integration.
- [ ] Define Steam achievements.
- [ ] Define Steam Cloud.
- [ ] Define stats.
- [ ] Define leaderboards if desired.
- [ ] Define overlay behavior.
- [ ] Define launch configuration.
- [ ] Define build deployment.
- [ ] Define beta branch strategy.
- [ ] Define Steam Deck compatibility target.

---

## 63.33 Analytics / Privacy

- [ ] Decide whether telemetry exists.
- [ ] Define what data is collected.
- [ ] Define whether speedrun statistics are local only.
- [ ] Define privacy requirements.
- [ ] Define opt-in/out.
- [ ] Define data retention.
- [ ] Define third-party services if any.

---

## 63.34 Security

- [ ] Define save integrity requirements.
- [ ] Define Steam API handling.
- [ ] Define external network dependencies.
- [ ] Define secrets management.
- [ ] Define third-party dependency auditing.
- [ ] Define release artifact verification.

---

## 63.35 Release

- [ ] Define minimum supported OS.
- [ ] Define hardware requirements.
- [ ] Define installation behavior.
- [ ] Define first-launch flow.
- [ ] Define settings defaults.
- [ ] Define crash recovery.
- [ ] Define localization packaging.
- [ ] Define Steam deployment.
- [ ] Define release candidate process.
- [ ] Define QA acceptance criteria.

---

# 64. BALANCE TODO

The following values are intentionally **not fixed by the GDD**:

- projectile speed;
- enemy speed;
- sonar sweep speed;
- detection range;
- detection frequency;
- reload duration;
- ammunition capacity;
- defense capacity;
- number of defenses;
- enemy count;
- enemy spawn rate;
- attack frequency;
- bullet-hell density;
- timing windows;
- first-hit system damage;
- reactivation times;
- mission duration;
- expected first-run time;
- expected optimized time;
- difficulty curve.

These must be determined through prototyping and playtesting.

---

# 65. TESTING PHILOSOPHY

The game should be tested at three distinct levels.

## Mechanical

> Is the game fun to operate?

Questions:

- Are commands satisfying?
- Is projectile travel readable?
- Is sonar fast enough?
- Is enemy movement predictable enough?
- Is resource management meaningful?

## Narrative

> Does the story work without forcing the player to understand it?

Questions:

- Does the normal ending make sense?
- Are anomalies subtle enough?
- Are clues solvable?
- Is the entity mysterious?

## Mastery

> Does the player become better?

Questions:

- Can experienced players meaningfully reduce mission time?
- Does execution improve with practice?
- Does the bullet hell feel impossible initially but learnable?
- Can players develop strategies?

---

# 66. SUCCESS CRITERIA

The project succeeds if:

### Normal player

Can understand the basic controls, complete the mission and feel satisfied.

### Skilled player

Can substantially improve their execution and completion time.

### Curious player

Can discover hidden conversations and anomalies.

### Dedicated player

Can solve all five clues.

### Expert player

Can survive the secret phase.

### Narrative-focused player

Can reconstruct the hidden story.

The game should support all of these without requiring one playstyle from every player.

---

# 67. CORE EXPERIENCE STATEMENT

The final experience should feel like:

> **Operating a strange WWII submarine through a terminal and sonar, gradually mastering a deceptively simple combat system while an apparently ordinary military mission slowly reveals that something impossible has been helping you survive.**

The player should initially believe:

> "I am operating this submarine."

Later:

> "Something is wrong with this submarine."

Eventually:

> "Something is operating this submarine with me."

And the final question should remain:

> **"Was it helping us, or were we helping it?"**

---

# 68. DOCUMENT RELATIONSHIP

The intended production documentation pipeline is:

```text
GDD
 │
 │ defines WHAT and WHY
 ▼
TDD
 │
 │ defines HOW
 ▼
Architecture
 │
 │ defines concrete technical structure
 ▼
Tickets
 │
 │ define implementable work
 ▼
Implementation
 │
 ▼
Testing
 │
 ▼
Release
```

The GDD intentionally does **not** prescribe the final implementation details.

The TDD must resolve every relevant item in the `TDD / Technical Decisions TODO` section before implementation of the corresponding systems begins.

The resulting tickets must be traceable back to:

- GDD requirement;
- TDD decision;
- acceptance criteria.

---

# 69. CURRENT DESIGN PRINCIPLE

The single most important principle for the project is:

> **The player should be able to finish the game without ever understanding what is really happening.**

But the game should leave enough evidence that a curious player can eventually reconstruct an entirely different story from the same events.

The normal game is therefore not a tutorial for the mystery.

**The normal game is the mystery's evidence.**

## 70. AI-Assisted Development & AI Project Governance

### 70.1 Objetivo

Definir cómo las herramientas de inteligencia artificial participarán en el desarrollo de **Sonar Horror**, asegurando que:

- El repositorio sea completamente **agnóstico a cualquier proveedor o modelo de IA**.
- Ninguna IA sea un requisito para compilar, ejecutar, testear o distribuir el juego.
- Las instrucciones, memorias, contexto y reglas utilizadas por las IA **no formen parte del producto final ni contaminen el código fuente del juego**.
- Cualquier IA pueda incorporarse o reemplazarse sin modificar la arquitectura del proyecto.
- El trabajo realizado por IA sea **auditable, reproducible y revisable por humanos**.
- Una IA pueda comprender el proyecto a partir de archivos estructurados y no dependa exclusivamente de una conversación previa.
- Los tickets generados para desarrollo sean suficientemente precisos para que una IA pueda ejecutarlos siguiendo procedimientos definidos.
- Las decisiones arquitectónicas y de diseño permanezcan en documentación versionada y no en la memoria de una herramienta específica.
- El proyecto pueda continuar desarrollándose completamente de forma manual si todas las herramientas de IA desaparecen.

------

### 70.2 Principio fundamental: AI-agnostic repository

El repositorio Git debe ser independiente de la IA utilizada.

No se deberá asumir:

- ChatGPT.
- Claude.
- Gemini.
- Copilot.
- Cursor.
- Windsurf.
- Codex.
- Cualquier otro agente.
- Cualquier modelo local.

El proyecto debe poder abrirse en un entorno limpio y comprenderse sin acceso a ninguna IA.

La IA debe considerarse una **herramienta externa de desarrollo**, no una dependencia del videojuego.

**Regla:**

> El repositorio contiene el conocimiento necesario para desarrollar el juego, pero no contiene una dependencia hacia la herramienta utilizada para interpretar ese conocimiento.

------

### 70.3 Separación entre proyecto e inteligencia artificial

Debe existir una separación conceptual entre:

```
SONAR HORROR
│
├── Game Repository
│   ├── Godot project
│   ├── C# backend
│   ├── Controller
│   ├── Tests
│   ├── Documentation
│   └── Development scripts
│
└── AI Development Environment
    ├── Instructions
    ├── Memories
    ├── Agent configuration
    ├── Context generation
    ├── Prompts
    ├── AI-specific tools
    └── Temporary AI state
```

El segundo grupo **no pertenece al producto**.

Puede existir en una ubicación externa, por ejemplo:

```
~/sonar-horror-ai/
```

mientras que:

```
~/sonar-horror/
```

contiene exclusivamente el proyecto.

La ubicación exacta queda pendiente de decisión durante el TDD/Technical Setup.

------

### 70.4 AI Context Layer

Se deberá definir una capa de contexto que permita a una IA comprender el proyecto sin depender de conversaciones anteriores.

Esta capa deberá contener, como mínimo:

```
AI CONTEXT
│
├── Project overview
├── Current architecture
├── Current development state
├── Coding conventions
├── Architectural constraints
├── Design constraints
├── Known decisions
├── Known unresolved decisions
├── Current milestone
├── Current ticket
├── Relevant documentation
└── Development procedures
```

La IA deberá poder reconstruir su contexto leyendo estos documentos.

No se deberá asumir:

> “La IA ya sabe lo que hablamos anteriormente.”

------

### 70.5 Source of Truth hierarchy

Se deberá establecer una jerarquía explícita de autoridad.

Propuesta:

```
1. Explicit project decisions
2. GDD
3. TDD
4. Architecture Decision Records
5. Technical documentation
6. Current ticket
7. Tests
8. Existing implementation
9. AI assumptions
```

Cuando exista una contradicción, una fuente de mayor prioridad prevalece sobre una inferior.

Especialmente:

> La IA nunca debe modificar una decisión de diseño simplemente porque considere que existe una solución técnica "mejor".

Si encuentra una contradicción deberá reportarla.

------

### 70.6 AI must not invent requirements

La IA deberá distinguir entre:

- requisito definido;
- decisión tomada;
- decisión pendiente;
- inferencia;
- sugerencia.

Por ejemplo:

```
[DECIDED]
The player submarine is always visually centered.

[DECIDED]
Combat occurs in real time.

[OPEN]
Exact maximum simultaneous enemies.

[ASSUMPTION]
Initial prototype will support 10 simultaneous entities.

[PROPOSAL]
Use object pooling for projectile entities.
```

Una IA no deberá convertir:

```
[OPEN]
```

en:

```
[DECIDED]
```

sin autorización.

------

### 70.7 AI Instructions outside Git

Las instrucciones específicas para cada IA deberán mantenerse fuera del repositorio principal.

Por ejemplo:

```
~/sonar-horror-ai/
├── agents/
│   ├── coding-agent/
│   ├── design-agent/
│   ├── testing-agent/
│   └── documentation-agent/
│
├── memory/
├── prompts/
├── workflows/
├── context/
└── sessions/
```

Estos archivos pueden contener:

- instrucciones;
- preferencias de trabajo;
- memoria;
- prompts;
- contexto generado;
- configuración de agentes;
- historial de tareas;
- herramientas auxiliares.

No deberán contener información que sea necesaria para ejecutar el videojuego.

------

### 70.8 Git repository must remain portable

El proyecto deberá poder ser clonado:

```
git clone <repository>
```

y funcionar sin:

- cuenta de ChatGPT;
- API key;
- API de OpenAI;
- API de Anthropic;
- API de Google;
- agente instalado;
- memoria externa;
- prompt específico;
- servicio externo de IA.

La IA puede **acelerar** el desarrollo, pero nunca puede ser necesaria para construir el producto.

------

### 70.9 AI-readable documentation

La documentación deberá estar diseñada tanto para humanos como para agentes.

Se deberán preferir formatos estructurados y fácilmente procesables:

```
.md
.yaml
.json
.csv
```

sobre formatos difíciles de consultar automáticamente.

El GDD principal podrá mantenerse como Markdown, pero deberá dividirse en documentos especializados.

Propuesta:

```
docs/
├── gdd/
│   ├── 00_overview.md
│   ├── 01_vision.md
│   ├── 02_gameplay.md
│   ├── 03_core_loop.md
│   ├── 04_sonar.md
│   ├── 05_terminal.md
│   ├── 06_combat.md
│   ├── 07_defense.md
│   ├── 08_progression.md
│   ├── 09_story.md
│   ├── 10_secrets.md
│   ├── 11_characters.md
│   ├── 12_audio.md
│   ├── 13_ui.md
│   ├── 14_difficulty.md
│   ├── 15_endings.md
│   ├── 16_replayability.md
│   └── 70_ai_development.md
│
├── tdd/
│
├── adr/
│
├── tickets/
│
└── development/
```

Esto permite que una IA cargue solamente el contexto necesario.

------

### 70.10 Machine-readable project state

Además de documentación humana, se deberá definir un estado estructurado del proyecto.

Por ejemplo:

```
project:
  name: sonar_horror
  status: pre_production

milestone:
  current: prototype

systems:
  sonar: planned
  terminal: planned
  combat: planned
  defense: planned
  dialogue: planned

architecture:
  frontend: godot
  backend: dotnet
  controller: planned

open_decisions:
  - exact_enemy_limit
  - exact_reload_system
  - audio_generation_strategy
```

El formato definitivo queda pendiente de decisión técnica.

El objetivo es que un agente pueda responder:

> “¿En qué estado está actualmente el proyecto?”

sin tener que leer todo el GDD.

------

### 70.11 Ticket-driven AI development

La IA no deberá recibir instrucciones vagas como:

> “Implementa el sonar.”

En su lugar deberá trabajar a partir de tickets.

Cada ticket deberá especificar:

```
ID
Title
Purpose
Context
Requirements
Dependencies
Affected systems
Acceptance criteria
Tests required
Relevant documentation
Constraints
Implementation notes
Definition of Done
```

Ejemplo conceptual:

```
SH-SONAR-001

Implement automatic sonar sweep.

Requirements:
- Sweep rotates continuously.
- Player can stop it.
- Player can restart automatic sweep.
- Entities become detectable only while sweep passes them.

Acceptance:
- Sweep completes one rotation in X seconds.
- Detection occurs only inside the sweep.
- Stopping the sweep freezes its position.
- Restarting resumes automatic rotation.
```

La IA deberá implementar **el ticket**, no reinterpretar el diseño completo del juego.

------

### 70.12 AI workflow

Se deberá definir un workflow estándar:

```
READ
 ↓
UNDERSTAND
 ↓
CHECK CONSTRAINTS
 ↓
IDENTIFY DEPENDENCIES
 ↓
PLAN
 ↓
IMPLEMENT
 ↓
TEST
 ↓
REVIEW
 ↓
DOCUMENT
 ↓
REPORT
```

La IA no deberá comenzar directamente escribiendo código.

Antes deberá:

1. leer el ticket;
2. leer documentación relevante;
3. revisar arquitectura;
4. identificar dependencias;
5. detectar contradicciones;
6. proponer el plan;
7. implementar;
8. ejecutar tests;
9. revisar cambios;
10. reportar lo realizado.

------

### 70.13 Human approval boundaries

Se deberán definir explícitamente las acciones que requieren aprobación humana.

Como mínimo:

**IA puede hacer automáticamente:**

- crear código;
- modificar código relacionado con un ticket;
- crear tests;
- ejecutar tests;
- generar documentación;
- refactorizar dentro del scope autorizado.

**IA debe solicitar aprobación para:**

- cambiar arquitectura;
- cambiar una decisión del GDD;
- agregar una dependencia externa;
- eliminar sistemas;
- cambiar APIs públicas;
- cambiar contratos entre Godot y C#;
- modificar configuración de build/release;
- modificar CI/CD;
- modificar estructura fundamental del proyecto;
- cambiar reglas de gameplay;
- introducir contenido no especificado.

------

### 70.14 AI change boundaries

Cada ejecución de IA deberá tener un scope.

Ejemplo:

```
Allowed:
src/combat/**
tests/combat/**
docs/tdd/combat.md

Forbidden:
src/story/**
src/ui/**
project.godot
architecture/**
```

Esto evita que un agente que está implementando misiles modifique accidentalmente el sistema de diálogos.

------

### 70.15 AI-generated code ownership

Todo código generado por IA deberá cumplir exactamente las mismas reglas que código escrito manualmente.

No deberá existir:

```
AI code
Human code
```

como categorías arquitectónicas.

Una vez integrado:

> Todo código pertenece al proyecto y debe cumplir los mismos estándares.

------

### 70.16 AI verification

La IA nunca deberá ser considerada una fuente suficiente de validación.

Una implementación no estará terminada porque:

> “La IA dice que funciona.”

Debe existir evidencia:

```
Code
+
Tests
+
Build
+
Runtime validation
+
Acceptance criteria
```

------

### 70.17 AI hallucination prevention

Se deberán establecer reglas explícitas:

- No inventar APIs.
- No inventar clases existentes.
- No asumir comportamiento de Godot.
- No asumir comportamiento de .NET.
- No asumir que una librería está instalada.
- No inventar requisitos.
- No eliminar código desconocido sin investigar.
- No modificar comportamiento no relacionado.
- No declarar tests exitosos sin ejecutarlos.

Cuando no tenga información suficiente:

```
UNKNOWN
```

deberá ser una respuesta válida.

------

### 70.18 AI memory

La memoria persistente de la IA deberá separarse de la documentación oficial.

Por ejemplo:

```
AI memory:
"User prefers concise ticket summaries."
```

no tiene autoridad sobre:

```
docs/gdd/gameplay.md
```

La memoria sirve para **facilitar colaboración**, no para definir el producto.

Las decisiones importantes siempre deberán terminar en documentación versionada.

------

### 70.19 AI session recovery

Una nueva IA deberá poder continuar el trabajo.

Por ejemplo:

```
Agent A
    ↓
implements SH-SONAR-014
    ↓
commits
    ↓
Agent B
    ↓
reads project state
    ↓
continues SH-COMBAT-021
```

No debe existir dependencia de:

> “La misma IA que empezó el proyecto tiene que terminarlo.”

------

### 70.20 AI handoff

Cada tarea completada deberá producir un pequeño handoff:

```
Completed:
- ...

Changed:
- ...

Tests:
- ...

Known issues:
- ...

Decisions:
- ...

Next recommended ticket:
- ...
```

Esto permitirá cambiar de agente sin perder contexto.

------

### 70.21 AI tools

Se deberá decidir antes de producción:

- qué agentes se utilizarán;
- cuáles tendrán acceso al filesystem;
- cuáles tendrán acceso al terminal;
- cuáles podrán ejecutar comandos;
- cuáles podrán modificar archivos;
- cuáles podrán hacer commits;
- cuáles podrán hacer push;
- cuáles podrán acceder a Internet;
- cuáles podrán acceder a documentación externa;
- cuáles podrán ejecutar Godot;
- cuáles podrán ejecutar tests;
- cuáles podrán modificar Git;
- cuáles solamente podrán leer.

**Principio recomendado: mínimo privilegio.**

Una IA solamente debe tener los permisos necesarios para la tarea actual.

------

### 70.22 Git & AI

Se deberá decidir:

- si la IA puede crear commits;
- si puede hacer amend;
- si puede hacer rebase;
- si puede hacer merge;
- si puede hacer push;
- si puede crear branches;
- naming convention de branches;
- formato de commits;
- relación entre commits y tickets;
- si los commits generados por IA requieren alguna marca;
- estrategia de rollback.

Mi recomendación inicial:

```
AI → puede crear commits locales
AI → no hace push automáticamente
Human → revisa y hace push
```

------

### 70.23 AI and testing

Cada sistema desarrollado con IA deberá definir:

```
Unit tests
Integration tests
Gameplay tests
Regression tests
Manual validation
```

La IA deberá ser capaz de ejecutar automáticamente todo aquello que sea automatizable.

Los tests no deberán depender de una IA.

------

### 70.24 AI and game balancing

La IA puede ayudar a analizar:

- dificultad;
- tiempos;
- frecuencia de enemigos;
- consumo de munición;
- duración de partidas;
- distribución de encuentros;
- estadísticas de testing.

Pero no deberá cambiar automáticamente el balance definitivo.

Los valores deberán provenir de:

```
Design decision
↓
Prototype
↓
Playtesting
↓
Data
↓
Balance decision
```

------

### 70.25 AI and narrative

La IA podrá ayudar a:

- detectar inconsistencias;
- verificar continuidad;
- analizar branches;
- encontrar diálogos contradictorios;
- generar herramientas para validar el árbol narrativo.

Pero la IA no deberá modificar arbitrariamente:

- canon;
- misterio;
- pistas;
- endings;
- personalidad de personajes;
- información que el jugador conoce.

El canon debe existir como documentación explícita.

------

### 70.26 AI and secrets

Los secretos del juego deberán estar documentados de forma que el equipo pueda desarrollarlos, pero deberá existir una separación entre:

```
Development documentation
```

y

```
Player-facing information
```

La IA que trabaja en gameplay puede necesitar conocer el secreto completo aunque el jugador nunca deba verlo.

------

### 70.27 AI-safe secret handling

Se deberá decidir si información extremadamente sensible del diseño narrativo:

- estará en el mismo repositorio;
- estará en documentación separada;
- estará cifrada;
- estará disponible para todos los agentes;
- estará disponible solamente para determinados agentes.

Esto es particularmente relevante para:

- Pistas 1–5.
- Fase 2.
- Bullet hell.
- Entidad.
- Naturaleza de la anomalía.
- Explicación del submarino destruido.
- Relación entre la entidad y la victoria aliada.
- Final secreto.

------

### 70.28 AI documentation generation

La IA podrá generar documentación automáticamente, pero deberá existir una fuente de verdad.

Por ejemplo:

```
Code
 ↓
Generated API documentation
```

pero nunca:

```
AI hallucination
 ↓
Documentation
 ↓
Architecture
```

La documentación generada deberá poder regenerarse.

------

### 70.29 Context minimization

Los agentes no deberán recibir todo el proyecto en cada tarea.

Se deberá implementar eventualmente un sistema:

```
Ticket
 ↓
Relevant systems
 ↓
Relevant GDD
 ↓
Relevant TDD
 ↓
Relevant ADR
 ↓
Relevant source files
```

Esto reduce:

- costo;
- ruido;
- contradicciones;
- errores;
- contexto innecesario.

------

### 70.30 AI project bootstrap

Se deberá crear eventualmente un comando/script capaz de preparar el contexto de un agente.

Conceptualmente:

```
./tools/ai/context.sh SH-SONAR-014
```

que podría producir:

```
Project overview
Relevant GDD sections
Relevant TDD sections
Relevant ADRs
Current architecture
Ticket
Dependencies
Current implementation
Tests
Known issues
```

El mecanismo exacto queda como decisión técnica del TDD.

------

### 70.31 AI Development Contract

Antes de comenzar producción se deberá crear un documento equivalente a:

```
AI_DEVELOPMENT_CONTRACT.md
```

que establezca las reglas obligatorias para cualquier agente.

Una IA deberá poder leer ese archivo y conocer:

> “Estas son las reglas bajo las cuales trabajo en Sonar Horror.”

------

### 70.32 AI-independent final build

El build final de Steam deberá ser completamente independiente de cualquier sistema de IA.

El juego no deberá:

- llamar APIs de IA;
- enviar datos a una IA;
- descargar modelos;
- requerir conexión con un proveedor de IA;
- depender de servicios de IA.

La IA es exclusivamente una herramienta de desarrollo.

------

### 70.33 Decisions required before implementation

Antes de comenzar producción deberán quedar decididos, como mínimo:

#### Repository

- estructura del repositorio;
- estructura de documentación;
- estrategia Git;
- branches;
- commits;
- versionado;
- releases;
- tags.

#### AI environment

- ubicación de memoria;
- ubicación de instrucciones;
- estructura de agentes;
- sistema de contexto;
- sistema de handoff;
- permisos;
- herramientas;
- sandboxing.

#### Documentation

- formato del GDD;
- formato del TDD;
- formato de ADR;
- formato de tickets;
- formato de estado del proyecto;
- nomenclatura;
- metadata;
- relaciones entre documentos.

#### Agent workflow

- cómo inicia una tarea;
- cómo obtiene contexto;
- cómo planifica;
- cómo implementa;
- cómo testea;
- cómo reporta;
- cómo termina una tarea.

#### Git

- qué puede hacer una IA;
- qué no puede hacer;
- commit policy;
- branch policy;
- review policy;
- rollback policy.

#### Quality

- testing;
- static analysis;
- formatting;
- build validation;
- acceptance criteria;
- code review.

#### Security

- credentials;
- API keys;
- secrets;
- filesystem permissions;
- network access;
- Steam credentials;
- production credentials.

#### Context

- source of truth;
- project memory;
- AI memory;
- canonical documentation;
- unresolved decisions;
- temporary assumptions.

------

### 70.34 Definition of Done — AI

Una tarea asistida por IA solamente se considera terminada cuando:

```
[ ] Requirements understood
[ ] Relevant documentation reviewed
[ ] No unresolved contradiction
[ ] Implementation completed
[ ] Tests created/updated
[ ] Tests pass
[ ] Build passes
[ ] Acceptance criteria satisfied
[ ] No unrelated changes
[ ] Documentation updated
[ ] Ticket updated
[ ] Handoff generated
[ ] Human review completed
```

------

### 70.35 Principle: AI is replaceable

La decisión arquitectónica fundamental de este apartado es:

> **La IA es reemplazable. El proyecto no.**

Si mañana desaparece el proveedor de IA utilizado durante el desarrollo, otro agente debe poder continuar leyendo:

```
GDD
+
TDD
+
ADR
+
Tickets
+
Source Code
+
Tests
+
Project State
```

sin necesitar acceso a la conversación original.

------

## 70.36 TODO — AI/TDD decisions

Esta sección deberá permanecer como **TODO explícito del TDD** y no debe considerarse completa solamente por haber definido los principios anteriores.

El TDD deberá resolver concretamente:

### AI architecture

-  ¿Dónde viven exactamente los archivos externos al repositorio?
-  ¿Cómo se carga el contexto?
-  ¿Cómo se genera el contexto por ticket?
-  ¿Qué formato tendrá el project state?
-  ¿Cómo se almacenará AI memory?
-  ¿Cómo se versionará AI memory?
-  ¿Cómo se evita que memoria contradiga documentación?
-  ¿Cómo se realizará handoff entre agentes?
-  ¿Cómo se recuperará el contexto después de una sesión perdida?

### Agent permissions

-  ¿Puede leer todo el repositorio?
-  ¿Puede escribir todo el repositorio?
-  ¿Puede ejecutar comandos?
-  ¿Puede ejecutar Godot?
-  ¿Puede ejecutar .NET?
-  ¿Puede crear commits?
-  ¿Puede hacer push?
-  ¿Puede acceder a Internet?
-  ¿Puede instalar dependencias?
-  ¿Puede modificar configuración del sistema?
-  ¿Puede acceder a secretos?
-  ¿Puede acceder a Steam tooling?

### Agent roles

-  Coding agent.
-  Testing agent.
-  Code review agent.
-  Documentation agent.
-  Narrative consistency agent.
-  Game design analysis agent.
-  Build/release agent.
-  ¿Se utilizarán realmente agentes separados?

### Context system

-  ¿Cómo se identifican documentos relevantes?
-  ¿Cómo se resuelven conflictos?
-  ¿Cómo se detectan documentos obsoletos?
-  ¿Cómo se marca una decisión como definitiva?
-  ¿Cómo se representan decisiones pendientes?
-  ¿Cómo se representan hipótesis?
-  ¿Cómo se representa deuda técnica?
-  ¿Cómo se representa trabajo en progreso?

### Ticket system

-  Formato definitivo.
-  ID convention.
-  Dependencies.
-  Acceptance criteria.
-  Definition of Done.
-  Required tests.
-  Required documentation.
-  Scope restrictions.
-  AI execution instructions.
-  Human approval points.

### Git integration

-  Branch naming.
-  Commit naming.
-  Ticket ↔ commit relationship.
-  PR structure.
-  AI-generated commit policy.
-  Review policy.
-  Revert strategy.
-  Release strategy.

### Automation

-  Context generation.
-  Ticket initialization.
-  Test execution.
-  Build validation.
-  Documentation validation.
-  Architecture validation.
-  Ticket status updates.
-  AI handoff generation.

### Security

-  Secret management.
-  API key handling.
-  Local credentials.
-  Steam credentials.
-  Network permissions.
-  Filesystem sandbox.
-  Sensitive narrative information.
-  Production environment isolation.

### Reproducibility

-  ¿Puede un agente nuevo continuar una tarea?
-  ¿Puede cambiarse de proveedor?
-  ¿Puede desarrollarse sin IA?
-  ¿Puede reconstruirse el contexto desde Git?
-  ¿Puede reproducirse un build?
-  ¿Puede reconstruirse el estado del proyecto?
-  ¿Puede auditarse qué cambió y por qué?

------

### 70.37 Architectural requirement

Finalmente, esta sección establece una restricción arquitectónica para todo el proyecto:

> **Ninguna decisión técnica de Sonar Horror podrá depender de las capacidades particulares de una IA determinada.**

Esto incluye el código **Godot + C#/.NET**, el controller, los tests, las herramientas de desarrollo, la documentación, Git, CI/CD y el proceso de release.

La IA deberá interactuar con ellos como una capa externa.

En términos conceptuales:

```
                    ┌──────────────────────────┐
                    │       AI TOOLING         │
                    │                          │
                    │ ChatGPT / Claude / etc.  │
                    └────────────┬─────────────┘
                                 │
                          AI Context Layer
                                 │
                          Tickets / Docs
                                 │
                    ┌────────────▼─────────────┐
                    │     HUMAN / AI DEV       │
                    └────────────┬─────────────┘
                                 │
                              Git
                                 │
              ┌──────────────────▼──────────────────┐
              │          SONAR HORROR REPO          │
              │                                     │
              │  GDD ── TDD ── ADR ── Tickets       │
              │             │                       │
              │             ▼                       │
              │       Godot Frontend                │
              │             │                       │
              │        Controller                   │
              │             │                       │
              │       C#/.NET Backend               │
              │             │                       │
              │          Tests                      │
              └─────────────────────────────────────┘
                                 │
                                 ▼
                         FINAL GAME BUILD
                                 │
                                 ▼
                              STEAM
```

**Esta sería la decisión madre del punto 70:** *la IA no forma parte de la arquitectura del juego; forma parte de la arquitectura del proceso de desarrollo.*
