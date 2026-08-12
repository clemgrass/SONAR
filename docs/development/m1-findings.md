# M1 — Hallazgos

M1 termina cuando las ocho preguntas tienen respuesta anotada aquí y los números están en
`config/simulation.working.json`. **No termina cuando "se siente bien"** — eso no es un
criterio, es una sensación, y con ella el milestone se estira indefinidamente.

Si la respuesta a la pregunta 1 es negativa, el milestone está igualmente completo: cumplió
su función, que era avisar a tiempo.

| # | Pregunta | Respuesta | Evidencia |
|---|---|---|---|
| 1 | ¿Extrapolar lecturas es satisfactorio? | | tasa de acierto entre sesiones |
| 2 | Tiempo medio por comando | | `user://m1-session.csv` |
| 3 | Máximo de contactos jugable | | |
| 4 | Período del barrido | | probado 2,5 / 3,5 / 5 s |
| 5 | Número de bandas y radios | | probado 5 y 8 |
| 6 | ¿Hace falta repetir y ajustar? | | |
| 7 | ¿La munición aprieta? | | |
| 8 | ¿La fase extrema se dispara o se sobrevive? | | |

## Cómo se mide

- **Pregunta 2** sale directo de `avg_compose_seconds` en `user://m1-session.csv`, que se
  escribe al cerrar el juego. Cuenta desde la primera tecla tras el último Enter.
- **Pregunta 1** no tiene medida directa. El proxy es `hit_rate` en el mismo archivo, leído
  a lo largo de varias sesiones: si no sube con la práctica, la habilidad no existe.
- Las demás se responden jugando y anotando. Cambiar un valor en
  `config/simulation.working.json` y volver a jugar es el ciclo entero.

## Decisiones que M1 puede invalidar

M1 tiene autoridad para revertir decisiones del spec. Si algo de esto aparece, anótalo aquí
y vuelve al spec antes de escribir narrativa:

- [ ] La extrapolación se siente como aritmética, no como pilotear un instrumento
- [ ] El tiempo por comando no cabe en el ritmo del combate
- [ ] El gating por clase hace el bullet hell ingobernable
- [ ] La banda como ventana no produce decisiones interesantes

## Notas de sesión

<!-- Una entrada por sesión: fecha, qué valores se probaron, qué se observó. -->
