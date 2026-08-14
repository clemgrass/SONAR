namespace Sonar.Game;

/// <summary>
/// Lo que el jugador lee antes de escribir START, con el mundo detenido.
///
/// Está en un archivo aparte porque es **contenido**, no cableado: se va a reescribir muchas
/// veces mirando cómo se traba la gente, y no conviene tener que abrir Main.cs para eso.
///
/// En inglés, como todo lo que ve el jugador. Sale de golpe y sin ritmo: es texto de
/// referencia para leer al propio paso, no una secuencia con dramaturgia.
/// </summary>
public static class Briefing
{
    public static readonly string[] Lines =
    {
        "SONAR M1 — VALIDATION PROTOTYPE",
        "",
        "YOU ARE THE SONAR OPERATOR. YOU NEVER SEE THE SEA.",
        "ALL YOU SEE IS WHAT THE BEAM LAST SWEPT.",
        "",
        "READING THE SCOPE",
        "  THE LINE TURNING AROUND THE CIRCLE IS THE BEAM.",
        "  A MARK IS WHERE A CONTACT WAS WHEN THE BEAM LAST CROSSED IT.",
        "  NOT WHERE IT IS NOW. IT HAS MOVED SINCE.",
        "  MARKS FADE AS THEY AGE. A FAINT MARK IS AN OLD ONE.",
        "  THE OUTER RING IS GRADUATED IN DEGREES.",
        "  THE INNER CIRCLES ARE RANGE BANDS, A THROUGH E.",
        "  NOTHING IS LABELLED. YOU READ THE BEARING AND THE BAND YOURSELF.",
        "",
        "FIRING",
        "  TYPE A BAND LETTER AND THREE DIGITS OF BEARING, LIKE C047.",
        "  THE TORPEDO RUNS ALONG THAT BEARING AND DETONATES AT THAT BAND.",
        "  IT DESTROYS THE FIRST THING IT CROSSES ON THE WAY.",
        "  IT TAKES SECONDS TO ARRIVE. AIM WHERE THE CONTACT WILL BE.",
        "",
        "COMMANDS",
        "  C047      FIRE ON BEARING 047, BAND C",
        "  DEF C047  DROP A DEFENCE THERE. ONE AT A TIME, SPENT WHEN IT GOES OFF",
        "  RELOAD    LOAD ONE TORPEDO. THREE TUBES. THE WORLD KEEPS RUNNING",
        "  SONAR     STOP OR RESTART THE BEAM",
        "  STATUS    REPORT AMMUNITION AND DEFENCES",
        "  RETRY     START OVER, ONCE YOU ARE DEAD",
        "",
        "STAYING ALIVE",
        "  TWO TORPEDO HITS AND YOU ARE GONE.",
        "  A CONTACT THAT REACHES YOU KILLS YOU OUTRIGHT. DO NOT LET THEM CLOSE.",
        "",
        "THE DEMO",
        "  CONTACTS COME IN WAVES: ONE, THEN TWO, THEN FOUR, THEN EIGHT.",
        "  A WAVE ONLY ARRIVES ONCE THE LAST ONE IS GONE.",
        "  IT IS NOT MEANT TO BE WON. HOW FAR YOU GET IS THE MEASUREMENT.",
        "",
        "TYPE START WHEN YOU ARE READY.",
    };
}
