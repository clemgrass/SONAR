using System;
using System.Collections.Generic;
using Sonar.Core.Spatial;

namespace Sonar.Core;

/// <summary>
/// Todos los valores ajustables de la simulación (§61 del GDD). Los valores concretos son
/// salida de M1; lo que este tipo garantiza son las relaciones entre ellos.
/// </summary>
public sealed record SimulationConfig
{
    public double TickDurationSeconds { get; init; } = 1.0 / 60.0;
    public int MaxCatchUpTicks { get; init; } = 5;

    public double DeadZoneRadius { get; init; }
    public IReadOnlyList<double> BandOuterRadii { get; init; } = Array.Empty<double>();

    public double SweepPeriodSeconds { get; init; }

    public double TorpedoSpeed { get; init; }
    public double HitRadius { get; init; }
    public double BlastRadius { get; init; }
    public double NearMissRadius { get; init; }

    public double EnemyTorpedoSpeed { get; init; }
    public int EnemyFireIntervalTicks { get; init; }
    public int EnemyFireStaggerTicks { get; init; }

    /// <summary>
    /// Lo que tarda un enemigo en recargar tras disparar. Tiene su propio número —y no
    /// reusa <see cref="ReloadTicks"/>— porque la cadencia enemiga es una perilla de
    /// dificultad, no una simetría de reglas: con ambos lados al mismo reloj el jugador
    /// queda encadenado a esquivar y nunca tiene una ventana para calcular un disparo.
    /// </summary>
    public int EnemyReloadTicks { get; init; }

    public double SubHitRadius { get; init; }

    /// <summary>
    /// Dentro de este radio un enemigo oye al jugador aunque esté en silencio absoluto: el
    /// sonido se atenúa con la distancia. Es lo que hace que callarse proteja de los lejanos
    /// y nunca de los cercanos, y lo que hace que acercarse sea la forma de apuntar.
    /// </summary>
    public double PassiveHearingRadius { get; init; }

    public double DefenseRadius { get; init; }
    public int DefenseCapacity { get; init; }

    public int MagazineSize { get; init; }
    public int AmmoReserve { get; init; }
    public int ReloadTicks { get; init; }

    public BandLayout BuildBandLayout() => new(DeadZoneRadius, BandOuterRadii);

    /// <summary>
    /// Lanza si alguna invariante no se cumple. Es un error de configuración: debe fallar
    /// al arrancar, no producir comportamiento raro en tiempo de ejecución.
    /// </summary>
    public void Validate()
    {
        Positive(TickDurationSeconds, nameof(TickDurationSeconds));
        Positive(SweepPeriodSeconds, nameof(SweepPeriodSeconds));
        Positive(TorpedoSpeed, nameof(TorpedoSpeed));
        Positive(EnemyTorpedoSpeed, nameof(EnemyTorpedoSpeed));
        Positive(HitRadius, nameof(HitRadius));
        Positive(BlastRadius, nameof(BlastRadius));
        Positive(NearMissRadius, nameof(NearMissRadius));
        Positive(SubHitRadius, nameof(SubHitRadius));
        Positive(PassiveHearingRadius, nameof(PassiveHearingRadius));
        Positive(DefenseRadius, nameof(DefenseRadius));
        Positive(DeadZoneRadius, nameof(DeadZoneRadius));

        if (MaxCatchUpTicks <= 0) throw Bad(nameof(MaxCatchUpTicks), "debe ser mayor a cero.");
        if (DefenseCapacity < 0) throw Bad(nameof(DefenseCapacity), "no puede ser negativo.");
        if (MagazineSize <= 0) throw Bad(nameof(MagazineSize), "debe ser mayor a cero.");
        if (AmmoReserve < 0) throw Bad(nameof(AmmoReserve), "no puede ser negativo.");
        if (ReloadTicks <= 0) throw Bad(nameof(ReloadTicks), "debe ser mayor a cero.");
        if (EnemyFireIntervalTicks <= 0) throw Bad(nameof(EnemyFireIntervalTicks), "debe ser mayor a cero.");

        // Cero no es sólo inútil, es peligroso: recrea en silencio el defecto de la andanada
        // simultánea (§1.1) que WaveDirector arregla escalonando NextFireTick. Con stagger
        // en cero todo el escalonamiento colapsa y una oleada entera dispara en el mismo tick.
        if (EnemyFireStaggerTicks <= 0) throw Bad(nameof(EnemyFireStaggerTicks), "debe ser mayor a cero.");
        if (EnemyReloadTicks <= 0) throw Bad(nameof(EnemyReloadTicks), "debe ser mayor a cero.");

        // El enemigo nunca puede ciclar más rápido que el jugador. Es la invariante que
        // protege el nerfeo: si alguien sube ReloadTicks y se olvida de este, el stunlock
        // vuelve solo. Falla al arrancar en vez de reaparecer como "el juego se siente mal".
        if (EnemyReloadTicks < ReloadTicks)
            throw Bad(nameof(EnemyReloadTicks),
                $"({EnemyReloadTicks}) no puede ser menor a la recarga del jugador ({ReloadTicks}).");

        // Invariantes anti-tunneling: nada puede avanzar más por tick que su propio radio
        // de detección, o atravesaría su objetivo sin registrarlo (TDD §3.4).
        var ownTravel = TorpedoSpeed * TickDurationSeconds;
        var enemyTravel = EnemyTorpedoSpeed * TickDurationSeconds;

        if (HitRadius <= ownTravel)
            throw Bad(nameof(HitRadius),
                $"({HitRadius}) debe superar el avance por tick del torpedo propio ({ownTravel}).");
        if (HitRadius <= enemyTravel)
            throw Bad(nameof(HitRadius),
                $"({HitRadius}) debe superar el avance por tick del torpedo enemigo ({enemyTravel}).");
        if (SubHitRadius <= enemyTravel)
            throw Bad(nameof(SubHitRadius),
                $"({SubHitRadius}) debe superar el avance por tick del torpedo enemigo ({enemyTravel}).");
        if (DefenseRadius <= enemyTravel)
            throw Bad(nameof(DefenseRadius),
                $"({DefenseRadius}) debe superar el avance por tick del torpedo enemigo ({enemyTravel}).");

        if (BlastRadius < HitRadius)
            throw Bad(nameof(BlastRadius), $"({BlastRadius}) no puede ser menor a HitRadius ({HitRadius}).");
        if (NearMissRadius <= BlastRadius)
            throw Bad(nameof(NearMissRadius), $"({NearMissRadius}) debe superar a BlastRadius ({BlastRadius}).");

        // El submarino debe ser alcanzable dentro de la zona donde sus propios torpedos
        // no pueden estar armados. Es lo que hace peligroso que algo se acerque.
        if (SubHitRadius >= DeadZoneRadius)
            throw Bad(nameof(SubHitRadius),
                $"({SubHitRadius}) debe ser menor al radio de zona muerta ({DeadZoneRadius}).");

        // Todo lo que está lo bastante cerca como para matar por embestida tiene que estar
        // lo bastante cerca como para oír. Como arriba ya se exige SubHitRadius < DeadZoneRadius,
        // este piso lo garantiza por transitividad: un enemigo encima del casco nunca puede
        // estar sordo, que sería justo lo contrario de "acercarse es la forma de apuntar".
        if (PassiveHearingRadius <= DeadZoneRadius)
            throw Bad(nameof(PassiveHearingRadius),
                $"({PassiveHearingRadius}) debe superar el radio de zona muerta ({DeadZoneRadius}).");

        // Delega la validación de la geometría de bandas, que ya la implementa.
        BuildBandLayout();

        if (BandOuterRadii[^1] > 1.0)
            throw Bad(nameof(BandOuterRadii), "el radio exterior de la última banda no puede superar 1.0.");
    }

    private static void Positive(double value, string name)
    {
        if (value <= 0) throw Bad(name, $"debe ser mayor a cero, pero es {value}.");
    }

    private static ArgumentException Bad(string name, string message) =>
        new($"{name} {message}", name);
}
