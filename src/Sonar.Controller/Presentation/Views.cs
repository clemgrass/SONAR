using System.Collections.Generic;
using Sonar.Core.Spatial;

namespace Sonar.Controller.Presentation;

/// <summary>
/// Contacto tal como el jugador puede verlo: la posición es la de la <b>lectura</b> del
/// último barrido, no la real. El gating vive en el contrato de datos, no en una
/// convención — la capa de presentación no puede dibujar la verdad porque no la recibe.
/// </summary>
public sealed record ContactView(
    int Id, Vec2 Position, int BearingDegrees, char BandLetter, int AgeTicks);

/// <summary>Torpedo propio. Posición real, cada tick: es tuyo, sabes dónde está.</summary>
public sealed record TorpedoView(int Id, Vec2 Position, int BearingDegrees, double RunToRadius);

/// <summary>Torpedo enemigo. Posición real: se detecta por ruido, no por barrido.</summary>
public sealed record EnemyTorpedoView(int Id, Vec2 Position);

public sealed record DefenseView(int Id, Vec2 Position);

public sealed record WorldSnapshot(
    int Tick,
    double SweepDegrees,
    bool SweepRunning,
    IReadOnlyList<ContactView> Contacts,
    IReadOnlyList<TorpedoView> Torpedoes,
    IReadOnlyList<EnemyTorpedoView> EnemyTorpedoes,
    IReadOnlyList<DefenseView> Defenses,
    int AmmoLoaded,
    int AmmoReserve,
    int DefensesAvailable,
    bool ReloadInProgress,
    int HitsTaken,
    bool Destroyed);
