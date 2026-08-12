namespace Sonar.Core;

/// <summary>
/// Huella FNV-1a de 64 bits del estado del mundo. Los valores en coma flotante se cuantizan
/// antes de mezclarse: el hash debe detectar divergencia real, no ruido de último bit.
/// Ver TDD §8.2.
/// </summary>
public static class StateHash
{
    private const ulong Offset = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    /// <summary>Resolución de cuantización. Por debajo de esto dos valores son el mismo.</summary>
    private const double Quantum = 1e-9;

    public static long Compute(WorldState state)
    {
        var hash = Offset;

        MixInt(ref hash, state.Tick);
        MixInt(ref hash, state.NextEntityId);

        MixInt(ref hash, state.Sub.AmmoLoaded);
        MixInt(ref hash, state.Sub.AmmoReserve);
        MixInt(ref hash, state.Sub.DefensesAvailable);
        MixInt(ref hash, state.Sub.HitsTaken);
        MixInt(ref hash, state.Sub.Destroyed ? 1 : 0);
        MixInt(ref hash, state.Sub.ReloadInProgress ? 1 : 0);
        MixInt(ref hash, state.Sub.ReloadCompletesAtTick);

        MixDouble(ref hash, state.Sonar.SweepDegrees);
        MixInt(ref hash, state.Sonar.SweepRunning ? 1 : 0);

        foreach (var contact in state.Contacts)
        {
            MixInt(ref hash, contact.Id.Value);
            MixInt(ref hash, (int)contact.Kind);
            MixInt(ref hash, contact.Alive ? 1 : 0);
            MixDouble(ref hash, contact.Position.X);
            MixDouble(ref hash, contact.Position.Y);
        }

        foreach (var torpedo in state.Torpedoes)
        {
            MixInt(ref hash, torpedo.Id.Value);
            MixInt(ref hash, (int)torpedo.Side);
            MixDouble(ref hash, torpedo.Position.X);
            MixDouble(ref hash, torpedo.Position.Y);
        }

        foreach (var defense in state.Defenses)
        {
            MixInt(ref hash, defense.Id.Value);
            MixInt(ref hash, defense.Alive ? 1 : 0);
            MixDouble(ref hash, defense.Position.X);
            MixDouble(ref hash, defense.Position.Y);
        }

        foreach (var reading in state.Sonar.Readings)
        {
            MixInt(ref hash, reading.ContactId.Value);
            MixInt(ref hash, reading.ObservedAtTick);
            MixDouble(ref hash, reading.Position.X);
            MixDouble(ref hash, reading.Position.Y);
        }

        return unchecked((long)hash);
    }

    private static void MixInt(ref ulong hash, int value)
    {
        unchecked
        {
            for (var i = 0; i < 4; i++)
            {
                hash ^= (byte)(value >> (i * 8));
                hash *= Prime;
            }
        }
    }

    private static void MixDouble(ref ulong hash, double value)
    {
        var quantized = (long)System.Math.Round(value / Quantum);
        unchecked
        {
            for (var i = 0; i < 8; i++)
            {
                hash ^= (byte)(quantized >> (i * 8));
                hash *= Prime;
            }
        }
    }
}
