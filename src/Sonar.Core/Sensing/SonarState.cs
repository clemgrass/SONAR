using System;
using System.Collections.Generic;

namespace Sonar.Core.Sensing;

public sealed record SonarState
{
    /// <summary>Ángulo actual del haz en grados, [0, 360).</summary>
    public required double SweepDegrees { get; init; }

    public required bool SweepRunning { get; init; }

    /// <summary>Lecturas vigentes, siempre ordenadas por ContactId ascendente.</summary>
    public required IReadOnlyList<Reading> Readings { get; init; }

    /// <summary>Vueltas completas del haz desde el arranque.</summary>
    public required int Revolution { get; init; }

    public static SonarState CreateInitial() => new()
    {
        SweepDegrees = 0,
        SweepRunning = true,
        Readings = Array.Empty<Reading>(),
        Revolution = 0,
    };
}
