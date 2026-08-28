# IA enemiga — ruido, personalidades y fuego amigo

Fecha: 2026-08-15 · Estado: aprobado en diseño, pendiente de plan de implementación

Tercer spec del proyecto. El [primero](2026-08-11-sonar-preproduction-design.md) decidió qué
es el juego; el [segundo](2026-08-13-m1-gauntlet-and-telemetry-design.md) decidió qué le
faltaba a la demo para responder sus ocho preguntas con datos. Éste rehace **lo único que la
demo tiene sin diseñar**: el enemigo.

Notación del §70.6 del GDD: `[DECIDED]`, `[OPEN]`, `[ASSUMPTION]`, `[RISK]`.

---

## 1. Contexto

El playtest dejó una queja concreta: *«es demasiado sencillo y hasta lo hace injusto, siendo
disparado demasiadas veces al mismo tiempo»*.

La IA actual es una línea: cada enemigo apunta al origen y dispara. No decide, no se
equivoca, no se mueve más que en línea recta. Pero **una parte grande de la injusticia no es
la simplicidad de la IA sino un cable suelto**, y conviene separarlas antes de diseñar nada.

### 1.1 El defecto medido

Corriendo el `WaveDirector` real:

| Oleada | Entra en tick | Queda armada en | Espera | Torpedos a la vez |
|---|---|---|---|---|
| 1 | 600 | 1500 | 15.0 s | 1 |
| 2 | 901 | 1500 | 10.0 s | 2 |
| 3 | 1202 | 1500 | 5.0 s | 4 |
| 4 | 1503 | 1500 | **cero** | **8** |
| 5 | 1804 | 1500 | **cero** | **16** |

Dos defectos encadenados:

1. **`NextFireTick` es un tick absoluto, no relativo a la aparición.** `WaveDirector.Spawn`
   escribe `NextFireTick = _config.EnemyFireIntervalTicks` (1500). Como cada oleada entra más
   tarde, el plazo se consume solo, y de la oleada 4 en adelante ya venció antes de que el
   contacto existiera: **nacen armados y disparan en el tick en que aparecen**.

2. **El escalonamiento existe pero no está conectado.** `ContactSpawner` reparte los disparos
   (`+ i * EnemyFireStaggerTicks`), tiene tests, y el TDD §4.6 explica por qué hace falta.
   `WaveDirector` nunca lo llama: construye el contacto a mano. En la demo esa clase es
   código muerto.

`[DECIDED]` **`ContactSpawner` se borra y su lógica se muda a `WaveDirector`.** Tener dos
dueños del spawn *es* la causa del defecto. Sus tests se mudan con ella.

---

## 2. Alcance

Entra:

- El modelo de conocimiento enemigo (§3) y las cinco personalidades (§4)
- Movimiento gobernado y espera aleatoria (§5, §6)
- Fuego amigo evitado (§7) y receta de oleadas (§8)
- El arreglo del cableado (§1.1), la recarga de 3 s y la munición del HUD (§9)
- Telemetría por personalidad (§10)

No entra:

`[DECIDED]` **La tripulación sigue fuera**, por lo mismo que en el spec anterior.

`[DECIDED]` **No hay mecánicas nuevas para el jugador.** Ningún comando nuevo, ningún recurso
nuevo. Todo lo de acá son reglas sobre entidades que ya existen. Es una restricción que puso
el jugador y este spec la respeta entera.

---

## 3. El modelo de conocimiento — el ruido

`[DECIDED]` **Cada enemigo sabe de ti en proporción al ruido que haces.**

Cada contacto enemigo guarda **un solo dato nuevo**: `LastFixTick`, el tick en que te escuchó
por última vez. Nada más. No hay creencia simulada ni filtro de Kalman.

### 3.1 De dónde sale la puntería

El submarino del jugador está clavado en el origen y **el mundo deriva a su alrededor**
(§4 del TDD). Un contacto viejo, entonces, envejece solo hacia atrás en el rumbo del jugador:

```
antigüedad   = tickActual − LastFixTick
puntoDeMira  = derivaDelMundo × antigüedad × dt × (1 − AimLead)
```

Con `AimLead = 0` el enemigo le dispara a **dónde estabas**. Con `AimLead = 1` compensa la
deriva entera y le apunta al origen — es decir, a ti.

`[DECIDED]` Esto es deliberadamente el **espejo del problema del jugador**. Tú ves los
contactos donde *estaban*; ellos te disparan a donde *estabas*. El mismo error en los dos
sentidos, y "un enemigo mejor" pasa a significar algo medible: uno que extrapola mejor.

### 3.2 Qué te delata

`[DECIDED]` Cada uno de estos eventos pone `LastFixTick = tickActual` en **todos** los
enemigos vivos dentro del arena (radio ≤ 1.0), sin importar dónde esté el haz en ese momento:

| Fuente | Cadencia |
|---|---|
| Vuelta completa del haz | cada 7 s mientras el sonar esté encendido |
| Disparar un torpedo | al instante |
| Recargar | al instante |
| Soltar una defensa | al instante |

`[DECIDED]` **La cercanía refresca sin que hagas nada.** Un enemigo a menos de `0.46`
(el borde exterior de la banda B) te oye pasivamente aunque estés callado: su `LastFixTick`
es siempre el tick actual. El sonido se atenúa con la distancia.

Eso hace tres cosas de una: el silencio protege de los lejanos y no de los cercanos, el
STALKER que logra entrar **deja de fallar** —acercarse *es* su forma de apuntar— y se cierra
el agujero de campear en silencio.

### 3.3 Qué pasa cuando te callas

`[RISK]` Si el silencio te vuelve seguro, la estrategia óptima es apagar el sonar y no hacer
nada. Un juego cuya mejor jugada es no jugar está roto, y la demo mediría una guardia
aburrida en vez de medir si extrapolar es satisfactorio.

`[DECIDED]` **Callarse te desplaza y te ciega, las dos cosas.**

- Los enemigos que te pierden **convergen sobre tu última posición conocida**, que la deriva
  va dejando atrás. Se apiñan sobre agua vacía mientras tú te alejas.
- Sin barrido no ves nada, así que no puedes disparar a lo que no puedes ubicar.
- Las oleadas siguen llegando y hay que hundirlas para avanzar, así que no se puede ganar
  escondido.

`[ASSUMPTION]` Ver a tres submarinos apiñarse sobre el pedazo de agua donde ya no estás es la
mejor recompensa visual que puede dar este juego: el jugador entiende, sin que nadie se lo
explique, que los enemigos tienen una idea equivocada del mundo — igual que él.

---

## 4. Las personalidades

`[DECIDED]` **Una personalidad es un registro de datos, no una clase.** Sin herencia, sin
polimorfismo, sin un método `Update` por tipo. Es la diferencia entre dejar cinco listas y
dejar listo el **molde**: la sexta personalidad del juego final tiene que ser una entrada de
configuración, no código nuevo.

```csharp
public sealed record Personality
{
    public required string Name { get; init; }
    public required double RadiusLoaded { get; init; }    // dónde quiere estar con torpedo
    public required double RadiusEmpty { get; init; }     // dónde quiere estar sin torpedo
    public required double OrbitRate { get; init; }       // componente tangencial
    public required double FiringRadius { get; init; }    // desde dónde tira (0 = nunca)
    public required double AimLead { get; init; }         // 0 = a dónde estabas, 1 = a ti
    public required double AimJitter { get; init; }       // error extra, independiente
    public required int HesitationMinTicks { get; init; }
    public required int HesitationMaxTicks { get; init; }
    public required bool Rams { get; init; }
}
```

`[DECIDED]` **Una embestida siempre suena.** No hay un parámetro `NoisyApproach` aparte,
porque en el roster entero sería siempre igual a `Rams` y un campo que nunca difiere de otro
es una mentira sobre lo que el sistema puede hacer. Que el kamikaze avise no es una perilla:
es la regla que impide que mate con algo que no se pudo anticipar.

`[DECIDED]` El roster de M1 son cinco. Bandas según `bandOuterRadii`:
A `0.10–0.28` · B `0.28–0.46` · C `0.46–0.64` · D `0.64–0.82` · E `0.82–1.00`.

| | RadiusLoaded | RadiusEmpty | Orbit | FiringRadius | AimLead | Duda | Embiste |
|---|---|---|---|---|---|---|---|
| **PICKET** | 0.90 · E | 0.90 | alto | 0.95 | 0 | 4–12 s | no |
| **STALKER** | 0.55 · C | 0.85 · E | bajo | 0.62 | 0 | 2–6 s | no |
| **RAM** | 0.00 | 0.00 | nada | 0.00 · nunca dispara | — | — | **sí** |
| **GOAD** | 0.60 · C | 0.60 | medio | 0.66 | 0 · jitter alto | 0.5–2 s | no |
| **HUNTER** | 0.50 · C | 0.70 · D | bajo | 0.60 | **1** | 2–4 s | no |

`[DECIDED]` **`FiringRadius` tiene que ser mayor que `RadiusLoaded`, no igual.** El gobierno
integra `radial = hacia_afuera × (radioPreferido − radioActual)`, así que la aproximación al
radio preferido es asintótica **desde afuera** y nunca lo cruza: medido sobre 100 s, el PICKET
converge a 0.900000000 y el STALKER a 0.550000000, los dos estrictamente por fuera. Un radio de
compromiso igual al preferido no se alcanzaría jamás y esa personalidad no dispararía nunca.
La primera versión de esta tabla los tenía iguales, y como nada leía el campo el error no se
notó hasta que se conectó.


### 4.1 Qué hace a cada una distinta

**PICKET · el francotirador.** No entra nunca. Orbita la banda E y tira de lejos. Su torpedo
tarda ~30 s en llegar desde ahí, así que se ve venir. Peligroso por acumulación. Enseña que
el borde también mata, y cuesta torpedos propios a la distancia donde peor se apunta.

**STALKER · el acechador.** La columna vertebral del ritmo. Cierra a banda C, dispara, y
—porque su radio preferido cambia al quedar vacío— **se abre solo hasta la E para recargar**.
El alejarse *es* el aviso de que el jugador tiene una ventana.

**RAM · el kamikaze.** Raro. No dispara jamás: te busca y se te tira encima, y el contacto
mata al instante. La única amenaza que se mide en radio y no en torpedos.

`[DECIDED]` **El RAM hace ruido al acercarse.** Un kamikaze silencioso es
exactamente la injusticia que hay que sacar: mata algo que no se pudo anticipar. Con ruido,
la muerte por embestida siempre viene con un aviso que se pudo leer.

**GOAD · el provocador.** Media distancia, tira seguido y mal. No espera matar: quiere que
gastes defensas y que **hagas ruido**, y el ruido es lo que le afina la puntería al PICKET.
Es coordinación de manada sin escribir coordinación — dos personalidades tontas producen una
táctica que ninguna de las dos conoce.

**HUNTER · el que sí extrapola.** El único con `AimLead = 1`. Mientras haya uno en el
tablero, las estadísticas de fallo no protegen a nadie. Aparece en la oleada 5 y su llegada
dice "esto se puso serio" sin una línea de texto.

`[DECIDED]` **Queda fuera de M1 el que se disfraza de pecio** — chatarra hasta que dispara.
Es la mejor de todas para el terror, pero necesita pecios en pantalla y el jugador los dejó
fuera de esta demo. Anotada para el juego final.

---

## 5. Movimiento — una sola regla para las cinco

`[DECIDED]` Los contactos **dejan de ser balísticos**. Hoy Core integra una velocidad fija
puesta al nacer; ahora hay una fase de gobierno que la recalcula cada tick:

```
radioPreferido = tieneTorpedo ? RadiusLoaded : RadiusEmpty
centro         = puntoDeMira            // su estimación, NO el origen
hacia_afuera   = versor del centro al contacto
radioActual    = distancia(contacto, centro)
radial   = hacia_afuera × (radioPreferido − radioActual)   // negativo ⇒ se acerca
tangente = perpendicular(hacia_afuera) × OrbitRate
velocidad = clamp(radial + tangente) − derivaDelMundo
```

`[DECIDED]` **El centro del gobierno es la estimación, no el origen.** Con eso la
convergencia del §3.3 —apiñarse sobre el agua vacía que el jugador dejó atrás— no necesita
ninguna regla aparte: es el mismo cálculo, orbitando un punto equivocado. Un enemigo se
mueve siempre respecto de dónde *cree* que estás.

`[DECIDED]` Se conservan dos cosas que ya están y ya tienen tests:

- **La resta de la deriva**, para que el cierre neto sea el pedido.
- **El techo de velocidad**: ningún casco cierra a más de la mitad de la velocidad de su
  propio torpedo. Así el torpedo se lee como lo rápido de la escena.

`[DECIDED]` **El ciclo de acecho no necesita máquina de estados.** Sale de que el radio
preferido dependa de si hay torpedo cargado. PICKET y GOAD usan la misma regla con los dos
números iguales, así que no se mueven del sitio. Un solo mecanismo produce los tres
comportamientos.

`[ASSUMPTION]` El movimiento tiene que leerse con **puntos separados por 7 segundos**, que es
lo único que el jugador ve. Por eso no hay maniobras evasivas rápidas: serían invisibles y
sólo se sentirían como marcas saltando sin sentido. La inteligencia se nota en *dónde eligen
estar*, no en que se muevan errático.

---

## 6. La espera aleatoria

`[DECIDED]` Entre "puedo disparar" y "disparo" hay una espera sorteada por personalidad.

El argumento fuerte **no es el realismo**: es que el escalonamiento arregla la primera
andanada, pero con recargas idénticas los enemigos **se vuelven a sincronizar solos** a las
pocas vueltas y la pared reaparece. La espera aleatoria es lo único que lo impide para
siempre.

`[DECIDED]` El ancho de la ventana es **un rasgo de carácter**, no un número global:

| | Ventana | Carácter |
|---|---|---|
| GOAD | 0.5 – 2 s | impulsivo, tira apenas puede |
| HUNTER | 2 – 4 s | preciso, espera el momento |
| STALKER | 2 – 6 s | metódico |
| PICKET | 4 – 12 s | paciente, apunta largo |

`[DECIDED]` Distribución **uniforme**, del `IRandomSource` sembrado. Es el segundo consumidor
real después del `WaveDirector`, y se consume en orden de id para no romper el determinismo.

`[DECIDED]` **El escalonamiento determinista se conserva para el primer disparo de cada
oleada.** El azar hace improbable la coincidencia, no imposible, y el momento de entrada de
una oleada es justo donde la equidad importa más. Garantía donde importa, azar donde da vida.

`[ASSUMPTION]` **El que duda más apunta peor.** La puntería se degrada con la antigüedad del
contacto, así que mientras el PICKET se toma sus 12 segundos, la posición que tiene se le
pudre en las manos. Su paciencia es lo que lo hace fallar. Nadie programa eso: sale del cruce
de dos reglas ya decididas.

`[DECIDED]` **Se pierde la posibilidad de contar.** Hoy un jugador podría aprender "cada 14
segundos"; con azar no puede. Se acepta a propósito: la maestría se mueve de *contar
segundos* a *leer postura*. El aviso sigue siendo legible —está en rango, ya recargó—, sólo
varía el instante exacto.

---

## 7. Fuego amigo — raro porque lo evitan

Hoy es **imposible**. `ResolveDirectHits` sólo deja que impacten los torpedos propios, y
`ResolveTorpedoCollisions` sólo cruza propio contra enemigo. El TDD §4.5 lo marca
`[DECIDED]`: *«ni fuego amigo entre enemigos ni choque con pecios»*.

`[DECIDED]` **Se revierte esa decisión.** Un torpedo enemigo puede destruir a otro submarino
enemigo y a otro torpedo enemigo. M1 tiene autoridad para invalidar decisiones del spec; lo
que no puede es cambiarlas en silencio. El test `EnemyTorpedo_PassesThroughOtherEnemies`
afirma exactamente lo contrario y se reescribe.

`[DECIDED]` **Los pecios siguen siendo inmunes.** Que los torpedos atraviesen la chatarra
mantiene intacta la anomalía de defensas del §25, que necesita que ese pecio sobreviva como
referencia observable.

### 7.1 Cómo se consigue "rarísimo" sin ningún dado

`[DECIDED]` Antes de disparar, el enemigo mira si hay un compañero —submarino o torpedo—
dentro del corredor de tiro, **proyectado linealmente a lo largo del vuelo del torpedo**. Si
lo hay, aguanta y vuelve a mirar el tick siguiente. Se conocen perfectamente, así que casi
nunca se equivocan.

`[DECIDED]` El ancho del corredor es un valor de configuración, `FriendlyLaneRadius`, y
arranca en `HitRadius` (`0.040`): el enemigo evita exactamente aquello con lo que chocaría.
Subirlo los vuelve más cautos y baja el volumen de fuego; bajarlo produce más accidentes. Es
la perilla con la que se afina qué tan "rarísimo" resulta el §7 en la práctica.

`[DECIDED]` `AimJitter` arranca en **cero para todas menos GOAD**. El error sistemático de la
deriva (§3.1) es la fuente principal de fallo porque es *legible* — falla siempre hacia
atrás, en el rumbo del jugador. El jitter es ruido puro y sólo se usa donde el carácter lo
pide: el GOAD tira mal a propósito.

Los accidentes ocurren igual porque **esa proyección es lineal y los submarinos viran** hacia
su radio preferido (§5). Cuando alguien cambia de rumbo después del lanzamiento, la
predicción falla. Raro, gratis, y por una razón física.

`[ASSUMPTION]` Es la misma idea que atraviesa el diseño entero: **aquí todos extrapolan y a
todos se les escapa algo.** El jugador con los contactos, los enemigos con el jugador, y
ahora los enemigos entre ellos.

### 7.2 El regalo

`[RETRACTED 2026-08-16]` Esta sección afirmaba que ocho enemigos rodeando al jugador se
taparían la línea entre ellos, que sólo dispararía el que tuviera carril limpio, y que eso
**reemplazaba la "manada con turnos"** descartada. **Es falso, y se midió al implementarlo.**

En un anillo al mismo radio nadie tapa a nadie: el carril va radial hacia el centro, así que
el vecino a 45° queda a `R·sin45°` del carril —0.636 para R=0.90— contra un ancho de 0.040. Y
un enemigo a radio menor tapa apenas un cono de `±asin(0.040/r)`, unos ±4°: un 2% del círculo.
Con ocho enemigos, la probabilidad de que un tirador esté tapado ronda el 15%, no la mayoría.

`[DECIDED]` **El chequeo de carril se conserva igual**, porque su trabajo principal sí lo
hace: convierte el fuego amigo de imposible en raro-pero-posible, sin ningún dado.

`[DECIDED]` **Lo que reparte el fuego es otra cosa, y ya está.** La queja original —demasiados
disparos simultáneos— la resuelven el escalonamiento de entrada relativo a la aparición (§1.1),
la duda aleatoria por personalidad (§6) que impide que se re-sincronicen, y la receta de oleada
(§8), que mezcla cadencias distintas e incluye una personalidad que no dispara nunca.

`[ASSUMPTION]` El PICKET orbita por lectura, no por buscar carril: una marca que se mueve
sobre la banda E se distingue de una que se queda quieta. La justificación anterior no se
sostiene.

---

## 8. La oleada es una receta

`[DECIDED]` La dificultad deja de subir duplicando la cantidad —que es lo que convierte la
oleada 4 en una pared— y pasa a subir **cambiando la mezcla**, que es lo que gobierna cuántos
torpedos pueden estar en el agua a la vez.

| Oleada | Composición | Qué enseña |
|---|---|---|
| 1 | 1 STALKER | el ciclo acercarse-disparar-abrirse |
| 2 | 1 STALKER · 1 PICKET | el borde también mata |
| 3 | 2 STALKER · 1 PICKET · 1 GOAD | te obligan a hacer ruido |
| 4 | + 1 RAM | mira el radio, no sólo los torpedos |
| 5+ | + 1 HUNTER | esto se puso serio |

`[OPEN]` La progresión más allá de la 5 se define con datos. La duplicación cruda sigue
disponible como tope, pero la mezcla es la perilla preferida.

---

## 9. Los arreglos que vinieron aparte

`[DECIDED]` **Recarga del jugador: 3 s** (180 ticks). La enemiga baja de 14 s a **10 s**
(600 ticks) porque ahora la espera aleatoria carga con parte de la variedad, y 14 + 12 daba
ciclos de hasta 26 s que dejaban a los enemigos inofensivos. Sigue siendo más del triple que
la del jugador, y la invariante `EnemyReloadTicks >= ReloadTicks` la protege.

`[DECIDED]` **La munición del HUD.** Hoy dice `ammo 3/12`, que se lee como "te quedan 3 de
12". No es eso: son 3 en los tubos y 12 en la santabárbara — dos números sin relación,
separados por una barra que inventa una. Pasa a:

```
TUBES ●●○    RESERVE 12
```

`[ASSUMPTION]` Los puntos comunican de un vistazo cuántos tubos hay listos sin obligar a leer
un número. Si no se ve bien en el panel real, `TUBES 2/3` es equivalente y sin ambigüedad.

---

## 10. Telemetría

`[DECIDED]` **Cada línea del JSONL que involucre a un enemigo lleva su personalidad.** Con
cinco personalidades y ~20 testers, sin esto los datos son el promedio de cinco cosas
distintas y no responden nada.

Lo que hay que poder contestar:

- Qué personalidad mata más, y a qué distancia
- Cuántos torpedos falla cada una, **y por cuánto** — el margen es el dato, no el sí/no
- Cuánto tiempo estuvo el jugador en silencio, y qué le costó
- Si alguien descubrió apagar el sonar, y si le sirvió
- Cuántos torpedos propios murieron chocando con torpedos enemigos perdidos
- Cuántas veces un enemigo aguantó el tiro por tener un compañero en la línea

`[DECIDED]` El último es el que dice si el §7 funciona. Es invisible en pantalla: sin
medirlo, no hay forma de saber si los enemigos se están coordinando o si simplemente no
disparan.

---

## 11. Dónde vive cada cosa

| Pieza | Capa | Por qué |
|---|---|---|
| `LastFixTick` y la puntería | Core | es regla de simulación |
| `Personality` (el record) | Core | lo consumen las reglas |
| Fase de gobierno del movimiento | Core | corre dentro del tick |
| Chequeo de carril limpio | Core | decide si se crea un torpedo |
| Receta de oleadas | Controller | es guion de misión, como el `WaveDirector` |
| Asignación de personalidad al spawn | Controller | idem |
| HUD de munición | Game | es presentación |
| Personalidad en el JSONL | Game | el `SessionRecorder` ya vive ahí |

`[DECIDED]` Ni Core ni Controller referencian Godot. Esa ausencia sigue siendo el
enforcement, verificada por grep.

---

## 12. Riesgos

`[RISK]` **La letalidad cambia mucho y en dos direcciones.** La deriva en 7 s son `0.084` y
el casco mide `0.045`: con el sonar encendido, la antigüedad media al disparar ronda los
3.5 s y da un error de ~`0.042`, apenas por debajo del radio. Eso predice **alrededor de un
50% de aciertos** cuando el jugador suena, cayendo a casi cero cuando se calla y lejos.

Es el objetivo, no el problema — dos impactos matan, así que 50% con tres o cuatro enemigos
disparando sigue siendo mortal. Lo que había que evitar no era el fallo sino que el fallo
fuera *plano*. **Pero es aritmética, no medición.** El radio de impacto queda como perilla
explícita para ajustar en el playtest.

`[RISK]` **Los contactos dejan de ser balísticos**, y es el cambio estructural más grande del
plan: toca el bucle principal de `Simulation.Tick`. Todo test que asuma velocidad constante
hay que revisarlo, no sólo hacerlo pasar.

`[RISK]` **Cinco personalidades dividen la muestra.** Sin la telemetría del §10 no se va a
poder decir cuál funciona, sólo si la mezcla entera funciona.

`[RISK]` **El fuego amigo puede resultar demasiado raro o demasiado común**, y no hay forma
de saberlo sin correrlo. Por eso el contador del §10 no es opcional.

`[OPEN]` Si un torpedo enemigo perdido destruye un torpedo propio, ¿se siente como mala
suerte o como parte del ruido de la batalla? Los torpedos enemigos se detectan por ruido y se
ven siempre, así que se puede ver venir — pero eso es una hipótesis y hay que medirla.

---

## 13. Qué queda desactualizado

Al cerrar la implementación hay que tocar:

- **TDD §4.5** — se invierte la regla de fuego amigo (§7 de este spec)
- **TDD, IA enemiga** — `NextFireTick` pasa a ser relativo; entra la personalidad
- **`docs/development/godot-wiring.md`** — el HUD de munición cambia de forma
- **`project-state.yaml`** — `enemy_threat` deja de ser "IA mínima"
- **`docs/development/m1-findings.md`** — una entrada nueva con los números de tuning
