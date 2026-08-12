using Sonar.Core.Entities;
using Sonar.Core.Spatial;

namespace Sonar.Core.Sensing;

/// <summary>
/// Lo último que el barrido registró de un contacto. Se guarda una sola lectura por
/// contacto, la más reciente: interpretar posiciones sucesivas es trabajo del jugador,
/// no del juego mostrándole una traza (TDD §5.2).
/// </summary>
public sealed record Reading
{
    public required ContactId ContactId { get; init; }
    public required Vec2 Position { get; init; }
    public required int ObservedAtTick { get; init; }
}
