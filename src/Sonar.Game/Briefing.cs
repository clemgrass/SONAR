namespace Sonar.Game;

/// <summary>
/// Lo que el jugador lee antes de escribir START, con el mundo detenido.
///
/// Está en un archivo aparte porque es **contenido**, no cableado: se va a reescribir muchas
/// veces mirando dónde se traba la gente, y no conviene abrir Main.cs para eso.
///
/// En inglés, como todo lo que ve el jugador. Sale de golpe y sin ritmo: es texto de
/// referencia para leer al propio paso, no una secuencia con dramaturgia.
///
/// **Restricción dura: tiene que caber en el panel sin scrollear.** Con el panel de 450×650
/// a cuerpo 15 entran unas 34 líneas de hasta ~46 caracteres. Una versión anterior se pasaba
/// de largo y arrancaba desplazada al final, dejando arriba del pliegue justo lo único que
/// hay que entender sí o sí — que la marca es dónde el contacto **estaba**. Además las líneas
/// largas se envolvían y rompían las columnas de la tabla de comandos. Si agregas líneas,
/// mide.
/// </summary>
public static class Briefing
{
    public static readonly string[] Lines =
    {
        "SONAR M1 — VALIDATION PROTOTYPE",
        "YOU ARE THE SONAR OPERATOR.",
        "",
        "A MARK IS WHERE A CONTACT *WAS* WHEN THE",
        "BEAM LAST CROSSED IT. NOT WHERE IT IS NOW.",
        "MARKS FADE AS THEY AGE.",
        "",
        "THE OUTER RING IS DEGREES. THE CIRCLES",
        "ARE RANGE BANDS A TO E. NOTHING IS",
        "LABELLED — YOU READ THEM YOURSELF.",
        "",
        "A TORPEDO TAKES SECONDS TO ARRIVE AND",
        "KILLS THE FIRST THING IT CROSSES.",
        "",
        "COMMANDS",
        "  C047      FIRE ON BEARING 047, BAND C",
        "  DEF C047  DROP A DEFENCE. ONE AT A TIME",
        "  RELOAD    LOAD ONE TORPEDO. THREE TUBES",
        "  SONAR     STOP OR RESTART THE BEAM",
        "  STATUS    REPORT AMMO AND DEFENCES",
        "  RETRY     START OVER, ONCE YOU ARE DEAD",
        "",
        "TWO TORPEDO HITS AND YOU ARE GONE.",
        "A CONTACT THAT REACHES YOU KILLS OUTRIGHT.",
        "WAVES DOUBLE: ONE, TWO, FOUR, EIGHT.",
        "IT IS NOT MEANT TO BE WON.",
        "",
        "TYPE START WHEN YOU ARE READY.",
    };
}
