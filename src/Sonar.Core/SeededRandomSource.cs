namespace Sonar.Core;

/// <summary>
/// Generador xorshift64* sembrado. Se implementa a mano en lugar de usar System.Random
/// porque el algoritmo de System.Random no está garantizado entre versiones de .NET,
/// y una corrida guardada debe reproducirse igual tras actualizar el runtime.
/// </summary>
public sealed class SeededRandomSource : IRandomSource
{
    private ulong _state;

    public SeededRandomSource(int seed)
    {
        // El estado de xorshift nunca puede ser cero.
        _state = (ulong)seed ^ 0x9E3779B97F4A7C15UL;
        if (_state == 0) _state = 0x9E3779B97F4A7C15UL;
    }

    private ulong NextRaw()
    {
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;
        return _state * 0x2545F4914F6CDD1DUL;
    }

    /// <summary>Devuelve un valor en [0, 1).</summary>
    public double NextDouble() => (NextRaw() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>Devuelve un entero en [minInclusive, maxExclusive).</summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        var range = (ulong)(maxExclusive - minInclusive);
        return minInclusive + (int)(NextRaw() % range);
    }
}
