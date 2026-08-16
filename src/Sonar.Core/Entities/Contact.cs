using Sonar.Core.Spatial;

namespace Sonar.Core.Entities;

public enum ContactKind
{
    /// <summary>Submarino o nave enemiga. Se mueve por sí misma y dispara.</summary>
    Enemy,

    /// <summary>
    /// Pecio, bajo rocoso o boya. No ataca, no evade y no se mueve por sí misma.
    /// Existe para dar referencia visual de que el submarino avanza — sin ella la
    /// anomalía de la defensa no se puede leer (ver spec §4.9).
    /// </summary>
    Inert,
}

public sealed record Contact
{
    public required ContactId Id { get; init; }
    public required ContactKind Kind { get; init; }
    public required Vec2 Position { get; init; }

    /// <summary>Velocidad propia, sin incluir la deriva del mundo. Cero para Inert.</summary>
    public required Vec2 Velocity { get; init; }

    public required bool Alive { get; init; }

    /// <summary>Tick en el que este contacto puede volver a disparar. Solo aplica a Enemy.</summary>
    public required int NextFireTick { get; init; }

    /// <summary>
    /// Tick en el que este enemigo escuchó al jugador por última vez. Es <b>todo</b> lo que
    /// sabe de él: como el submarino está clavado en el origen y el mundo deriva alrededor,
    /// una posición vieja envejece sola hacia atrás en el rumbo del jugador. No hace falta
    /// simular una creencia; la deriva ya la calcula.
    /// </summary>
    public int LastFixTick { get; init; }

    /// <summary>
    /// Su carácter, o null. <b>Null significa el comportamiento de siempre</b>: velocidad
    /// fija y puntería perfecta al origen. Es lo que permite que la IA nueva sea opt-in
    /// contacto por contacto y que los tests anteriores sigan valiendo sin tocarlos.
    /// </summary>
    public Personality? Personality { get; init; }

    public static Contact CreateInert(ContactId id, Vec2 position) => new()
    {
        Id = id,
        Kind = ContactKind.Inert,
        Position = position,
        Velocity = Vec2.Zero,
        Alive = true,
        NextFireTick = int.MaxValue,
    };
}
