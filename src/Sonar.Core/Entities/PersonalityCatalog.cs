using System.Collections.Generic;

namespace Sonar.Core.Entities;

/// <summary>
/// Las cinco personalidades de M1, con sus números. Los radios están elegidos contra el
/// `bandOuterRadii` de la configuración de trabajo:
/// A 0.10–0.28 · B 0.28–0.46 · C 0.46–0.64 · D 0.64–0.82 · E 0.82–1.00.
///
/// Es una lista de datos a propósito. Ver el spec del 2026-08-15, §4.
/// </summary>
public static class PersonalityCatalog
{
    /// <summary>No entra nunca. Orbita la banda E y tira de lejos, con paciencia y sin
    /// puntería: su torpedo tarda unos 30 s en llegar desde ahí y se ve venir. Peligroso
    /// por acumulación. Orbita porque está buscando un carril limpio entre sus compañeros.</summary>
    public static readonly Personality Picket = new()
    {
        Name = "PICKET",
        RadiusLoaded = 0.90, RadiusEmpty = 0.90,
        OrbitRate = 0.010, FiringRadius = 0.90,
        AimLead = 0.0, AimJitter = 0.0,
        HesitationMinTicks = 240, HesitationMaxTicks = 720,   // 4–12 s
        Rams = false,
    };

    /// <summary>La columna vertebral del ritmo. Cierra a banda C, dispara, y al quedar vacío
    /// se abre solo hasta la E. El alejarse es el aviso de que el jugador tiene una ventana.</summary>
    public static readonly Personality Stalker = new()
    {
        Name = "STALKER",
        RadiusLoaded = 0.55, RadiusEmpty = 0.85,
        OrbitRate = 0.003, FiringRadius = 0.55,
        AimLead = 0.0, AimJitter = 0.0,
        HesitationMinTicks = 120, HesitationMaxTicks = 360,   // 2–6 s
        Rams = false,
    };

    /// <summary>No dispara jamás: te busca y se te tira encima. La única amenaza que se mide
    /// en radio y no en torpedos, y por eso hace ruido al acercarse.</summary>
    public static readonly Personality Ram = new()
    {
        Name = "RAM",
        RadiusLoaded = 0.00, RadiusEmpty = 0.00,
        OrbitRate = 0.0, FiringRadius = 0.0,
        AimLead = 0.0, AimJitter = 0.0,
        HesitationMinTicks = 0, HesitationMaxTicks = 0,
        Rams = true,
    };

    /// <summary>Tira seguido y mal desde media distancia. No espera matar: quiere que gastes
    /// defensas y que hagas ruido, y el ruido es lo que le afina la puntería al PICKET. Es
    /// coordinación de manada sin escribir coordinación.</summary>
    public static readonly Personality Goad = new()
    {
        Name = "GOAD",
        RadiusLoaded = 0.60, RadiusEmpty = 0.60,
        OrbitRate = 0.006, FiringRadius = 0.60,
        AimLead = 0.0, AimJitter = 0.055,
        HesitationMinTicks = 30, HesitationMaxTicks = 120,    // 0.5–2 s
        Rams = false,
    };

    /// <summary>El único que compensa la deriva. Aparece tarde y su llegada dice "esto se
    /// puso serio" sin una línea de texto.</summary>
    public static readonly Personality Hunter = new()
    {
        Name = "HUNTER",
        RadiusLoaded = 0.50, RadiusEmpty = 0.70,
        OrbitRate = 0.003, FiringRadius = 0.60,
        AimLead = 1.0, AimJitter = 0.0,
        HesitationMinTicks = 120, HesitationMaxTicks = 240,   // 2–4 s
        Rams = false,
    };

    public static readonly IReadOnlyList<Personality> All =
        new[] { Picket, Stalker, Ram, Goad, Hunter };
}
