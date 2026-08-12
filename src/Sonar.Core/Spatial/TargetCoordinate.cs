namespace Sonar.Core.Spatial;

/// <summary>
/// Objetivo escrito por el jugador: una marcación y una banda. Por ejemplo C047.
/// La banda nombra una distancia nominal — el centro de la banda — no un rango.
/// </summary>
public readonly record struct TargetCoordinate(Bearing Bearing, Band Band)
{
    /// <summary>Punto en espacio de simulación al que apunta esta coordenada.</summary>
    public Vec2 ToPosition(BandLayout layout) =>
        Bearing.ToUnitVector() * layout.CenterRadius(Band);

    public override string ToString() => $"{Band.Letter}{Bearing.Degrees:D3}";
}
