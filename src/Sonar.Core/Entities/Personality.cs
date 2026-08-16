namespace Sonar.Core.Entities;

/// <summary>
/// El carácter de un submarino enemigo, como datos y no como clase. No hay herencia, ni
/// polimorfismo, ni un método por tipo: son números que las reglas leen.
///
/// Es deliberado y es lo que hace que agregar la sexta personalidad para el juego final sea
/// una entrada de configuración en vez de código nuevo. Ver el spec del 2026-08-15, §4.
/// </summary>
public sealed record Personality
{
    public required string Name { get; init; }

    /// <summary>A qué distancia de su estimación quiere estar cuando tiene torpedo.</summary>
    public required double RadiusLoaded { get; init; }

    /// <summary>
    /// Y cuando está vacío. Que estos dos números difieran <b>es</b> el ciclo de acecho:
    /// se acerca a disparar, y al quedar vacío se abre solo a recargar. No hace falta
    /// ninguna máquina de estados.
    /// </summary>
    public required double RadiusEmpty { get; init; }

    /// <summary>Componente tangencial del movimiento. Cero va derecho, alto rodea.</summary>
    public required double OrbitRate { get; init; }

    /// <summary>Distancia desde la que se decide a disparar. Cero significa que nunca dispara.</summary>
    public required double FiringRadius { get; init; }

    /// <summary>
    /// 0 le dispara a dónde estabas; 1 compensa la deriva entera y te apunta a ti. Es la
    /// única diferencia entre el HUNTER y el resto, y se siente como otra criatura.
    /// </summary>
    public required double AimLead { get; init; }

    /// <summary>
    /// Error extra, en unidades de arena, independiente de la antigüedad. Arranca en cero
    /// para todas menos el GOAD: el error de la deriva es legible —falla siempre hacia atrás
    /// en el rumbo del jugador— y el jitter es ruido puro.
    /// </summary>
    public required double AimJitter { get; init; }

    /// <summary>
    /// Cuánto tarda en decidirse, sorteado uniforme en cada disparo. El ancho de esta
    /// ventana dice más del carácter que casi cualquier otro número: un impulsivo y un
    /// paciente se sienten distintos aunque disparen desde el mismo sitio.
    /// </summary>
    public required int HesitationMinTicks { get; init; }
    public required int HesitationMaxTicks { get; init; }

    /// <summary>
    /// Si mata por contacto en vez de por torpedo. <b>Una embestida siempre suena</b>: no hay
    /// un parámetro aparte para eso porque en el roster entero valdría siempre lo mismo que
    /// éste, y un campo que nunca difiere de otro es una mentira sobre lo que el sistema
    /// puede hacer. Que el kamikaze avise no es una perilla — es la regla que impide que mate
    /// con algo que no se pudo anticipar.
    /// </summary>
    public required bool Rams { get; init; }
}
