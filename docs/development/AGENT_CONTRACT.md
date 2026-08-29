# Contrato de agentes — SONAR

Reglas para cualquier agente de IA que trabaje en este repositorio. Vendor-neutral a
propósito: `CLAUDE.md`, `AGENTS.md` y `.cursorrules` son punteros de tres líneas a este
archivo, para que la regla viva en el repo y no en la herramienta del día.

Enmienda al §70.3/§70.7 del GDD, aprobada en preproducción.

---

## 1 · Idioma

- **Conversación, comentarios, documentación y mensajes de commit internos: español.**
- **El juego se publica en inglés únicamente.** Todo string que vea el jugador va en inglés:
  comandos, registros, HUD, narrativa.
- Los mensajes de commit van en inglés, en imperativo, con prefijo convencional
  (`feat(core):`, `test(controller):`, `chore:`, `docs:`).

No mezclar. Un comentario en inglés dentro del Core o un string en español en la UI son
ambos errores.

---

## 2 · La regla de capas

```
Sonar.Game (Godot)  ──▶  Sonar.Controller  ──▶  Sonar.Core
```

Una sola dirección, sin vuelta. **Ni `Sonar.Core` ni `Sonar.Controller` referencian Godot.**

Esa ausencia no es estilo: es el mecanismo de defensa. Si el Controller conociera Godot,
tipos de Godot se filtrarían hacia la simulación y el Core dejaría de ser testeable sin
abrir el editor. Verificado por grep, y es parte de la Definition of Done:

```bash
grep -rn "^using Godot" src/Sonar.Core/ src/Sonar.Controller/
```

Para conectar la escena de Godot, ver [godot-wiring.md](godot-wiring.md).

---

## 3 · Determinismo

La misma semilla y la misma secuencia de comandos deben producir el mismo resultado, tick a
tick. De eso depende que RETRY sea un reset puro y que los hallazgos de M1 sean comparables
entre sesiones.

Reglas concretas dentro de `Sonar.Core`:

- **Nada de `Dictionary` ni `HashSet`** para nada que se itere durante la simulación. Su
  orden de enumeración no está garantizado. Usa listas ordenadas por id ascendente. Con un
  puñado de entidades, un `Contains` lineal no cuesta nada. Verificado por grep.
- **Nada de `System.Random`.** El azar entra por `IRandomSource`, inyectado y sembrado.
- **El orden de actualización del tick es fijo** (TDD §3.2). Cambiarlo cambia el resultado y
  rompe la reproducibilidad respecto de corridas anteriores. Si hay que cambiarlo, es una
  decisión consciente que se anota, no un refactor.
- Los desempates se resuelven **por id ascendente**, nunca al azar.

Se promete reproducibilidad en la misma máquina. **No** se promete identidad bit a bit entre
plataformas: eso solo haría falta para replays compartibles, y no está decidido.

---

## 4 · Cómo se trabaja

### TDD, en este orden

1. Escribe el test **y córrelo para verlo fallar**. Un test que nunca se vio fallar no
   prueba nada.
2. Implementa lo mínimo para que pase.
3. Corre la suite completa.
4. Commit.

Saltarse el paso 1 no es ir más rápido: es no tener test.

### Verificación por mutación — la práctica que distingue a este repo

Antes de dar por cerrada cualquier propiedad que sostenga el diseño, **rompe la
implementación a propósito y confirma que los tests fallan.** Restaura después.

Existe porque ya se colaron tests vacuos varias veces. El patrón típico: un test que solo
afirma la *ausencia* de algo (`Assert.DoesNotContain`, `Assert.Empty`) pasa trivialmente
cuando el sistema entero está roto o cuando el escenario nunca ocurrió.

**Todo test que afirme una ausencia necesita un control positivo** que pruebe que el
escenario sí se produjo. Ejemplos reales en el repo:

| Propiedad | Control positivo |
|---|---|
| La zona muerta detiene torpedos | El mismo disparo contra un blanco justo afuera sí lo mata |
| Una defensa mata torpedos propios | El mismo disparo sin defensa por delante sí detona |
| Un contacto muerto deja de disparar | El mismo contacto vivo sí vuelve a disparar |
| Dos flujos de eventos son iguales | `Assert.NotEmpty` — dos flujos vacíos también son iguales |

Registra el resultado de la mutación al reportar la tarea: cuántos tests fallaron con la
implementación rota.

### Los planes se cuestionan

Los planes de `docs/superpowers/plans/` están escritos con cuidado, y aun así tienen errores:
suposiciones geométricas mal calculadas, conteos de tests desactualizados, tests vacuos.

**Si un plan pide algo que no calza con el código real, repórtalo. No adaptes el test para
que pase.** Un test ajustado hasta que pasa es peor que ningún test. Casos que ya ocurrieron:

- Tests que ponían un contacto en marcación 000 creyendo que el haz lo barre de inmediato,
  cuando el haz *arranca* ahí y por tanto es la última marcación en barrerse.
- Una regla de defensas que borraba la referencia inerte con la que el jugador debía
  observar la anomalía del §25.

---

## 5 · Lo que no se toca

### La anomalía de las defensas

Los contactos reciben la deriva del mundo. **Las defensas no.** No hay código especial: hay
una omisión deliberada, y esa omisión *es* la anomalía del §25 del GDD.

Si `DefenseAnomalyTests` falla porque alguien le agregó deriva a las defensas, la anomalía
dejó de existir. **No es un bug: es la mecánica.**

Por la misma razón, las defensas no destruyen contactos inertes: sin una referencia que sí
derive, la anomalía no se puede leer.

### El gating del sonar

La posición que llega a la presentación es la de la **lectura del último barrido**, no la
real. `ContactView` no expone la verdad, y hay un test que falla si alguien agrega un campo
que la filtre.

No dibujar trazas de posiciones sucesivas: convertiría la extrapolación en lectura pasiva y
vaciaría la habilidad central que M1 está validando.

### Terminología

Se dice **torpedo**, nunca misil, para ambos bandos. Los misiles lanzados desde submarino no
existen hasta los años cincuenta, y el §4 del GDD pide que la época se infiera de la
terminología.

---

## 6 · Notación de decisiones

Del §70.6 del GDD. Úsala en specs, TDDs y planes:

- `[DECIDED]` — resuelto. Cambiarlo requiere volver al documento donde se decidió.
- `[OPEN]` — pendiente. Nadie debe asumir una respuesta.
- `[ASSUMPTION]` — se está asumiendo algo que no se verificó. Se anota para poder revisarlo.
- `[RISK]` — algo que puede salir mal y hay que vigilar.

---

## 7 · Límites de rol

- **Instalaciones: da instrucciones, no instales.** El entorno lo maneja la persona.
- **Git: local por defecto, empuje a pedido.** Commitear en local va sin preguntar.
  **Empujar a `origin` sólo cuando la persona lo pide explícitamente**, nunca por iniciativa
  propia — un commit local se deshace con un `reset`, uno empujado ya lo vio otra gente.
  **Agregar o cambiar remotos, no**: eso lo hace la persona. `push --force`, nunca.
- **El frontend y el diseño visual son de la persona.** Un agente aporta *instrucciones de
  cableado* — contratos de datos, orden de llamadas, forma del snapshot — no código de UI ni
  decisiones estéticas. Ver [godot-wiring.md](godot-wiring.md) como referencia de formato.

---

## 8 · Toolchain

| Qué | Valor |
|---|---|
| .NET SDK | 10.0.110 |
| Target framework | `net9.0` — el que exige Godot 4.7 mono |
| Godot | 4.7.stable.mono, binario `godot-mono` (el binario `godot` es la build sin C#) |
| Tests | xUnit |

Trampas verificadas empíricamente:

- **`dotnet new sln` genera `.slnx` en el SDK 10** e imprime el mismo mensaje de éxito. El
  fallo es silencioso. Usa `--format sln`.
- **`dotnet new classlib` y `dotnet new xunit` generan `net10.0`.** Hay que corregir a
  `net9.0` a mano en cada proyecto.
- **`src/Sonar.Game` no se agrega a `Sonar.sln`.** `dotnet test` intentaría compilarlo sin
  los ensamblados de Godot. La suite corre sobre `Sonar.sln`; el juego se compila desde
  Godot o con `dotnet build src/Sonar.Game/Sonar.Game.csproj`.

---

## 9 · Dónde vive cada cosa

| Documento | Qué responde |
|---|---|
| `GAME DESIGN DOCUMENT.md` | Qué es el juego. La fuente |
| `docs/superpowers/specs/2026-08-11-sonar-preproduction-design.md` | Qué se decidió antes de escribir código, y por qué |
| `docs/tdd/M1-core-simulation.md` | Cómo funciona la simulación por dentro |
| `docs/superpowers/plans/` | Las tareas, una por una |
| `docs/development/godot-wiring.md` | Cómo conectar la escena de Godot |
| `docs/development/m1-findings.md` | Las ocho preguntas de M1 y sus respuestas |
| `project-state.yaml` | En qué estado está todo. **Actualizarlo es el primer paso de la Definition of Done de cada tarea** |

`project-state.yaml` se mantiene corto a propósito: un archivo largo se desactualiza.
