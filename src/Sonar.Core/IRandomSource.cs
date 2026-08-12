namespace Sonar.Core;

/// <summary>
/// Toda fuente de azar de la simulación pasa por aquí, inyectada y sembrada.
/// Core nunca construye generadores por su cuenta ni usa Random.Shared: eso rompería
/// el determinismo del que depende todo el diseño (ver TDD §8).
/// </summary>
public interface IRandomSource
{
    int NextInt(int minInclusive, int maxExclusive);
    double NextDouble();
}
