using Sonar.Core.Spatial;

namespace Sonar.Core.Events;

public abstract record GameEvent(int Tick);

// --- Sistemas ---
public sealed record SonarToggled(int Tick, bool Running) : GameEvent(Tick);
public sealed record StatusReported(int Tick, int AmmoLoaded, int AmmoReserve, int Defenses)
    : GameEvent(Tick);
public sealed record InputNotRecognized(int Tick, string RawText) : GameEvent(Tick);

/// <summary>
/// El haz completó una vuelta. No cambia ningún estado ni habilita ninguna acción: es un
/// latido, para que el silencio tenga una unidad contable y "hace dos barridos" sea una
/// frase que el jugador pueda pensar.
/// </summary>
public sealed record SweepCompleted(int Tick, int Revolution, int ReadingCount) : GameEvent(Tick);

// --- Combate propio ---
public sealed record TorpedoFired(int Tick, int TorpedoId, int BearingDegrees, char BandLetter)
    : GameEvent(Tick);
public sealed record TorpedoDetonated(int Tick, Vec2 Position, bool HitSomething) : GameEvent(Tick);
public sealed record ContactDestroyed(int Tick, int ContactId, Vec2 Position, DestructionCause Cause)
    : GameEvent(Tick);

/// <summary>
/// Detonación que no destruyó nada pero cayó cerca de un contacto vivo. No tiene efecto en M1:
/// se emite desde ahora para que el sistema de pistas lo consuma después sin tocar combate.
/// Es la base mecánica de la pista 2 (spec §4.10).
/// </summary>
public sealed record NearMiss(int Tick, int ContactId, double Distance) : GameEvent(Tick);

public sealed record FireRejected(int Tick, RejectionReason Reason) : GameEvent(Tick);

// --- Defensa ---
public sealed record DefenseDeployed(int Tick, int DefenseId, Vec2 Position) : GameEvent(Tick);
public sealed record DefenseRejected(int Tick, RejectionReason Reason) : GameEvent(Tick);

/// <summary>
/// La defensa se gastó. Hay que anunciarlo: la defensa se consume al usarse y sólo se puede
/// tener un número muy chico desplegado, así que si el jugador no se entera de que quedó
/// descubierto, la decisión de cuándo volver a poner una deja de existir.
/// </summary>
public sealed record DefenseDetonated(int Tick, int DefenseId, Vec2 Position, int TargetsDestroyed)
    : GameEvent(Tick);

// --- Munición ---
public sealed record ReloadStarted(int Tick) : GameEvent(Tick);
public sealed record ReloadCompleted(int Tick, int AmmoLoaded) : GameEvent(Tick);

// --- Amenaza enemiga ---
public sealed record EnemyTorpedoFired(int Tick, int TorpedoId, int SourceContactId) : GameEvent(Tick);
public sealed record EnemyTorpedoDestroyed(int Tick, int TorpedoId, Vec2 Position,
    DestructionCause Cause) : GameEvent(Tick);
public sealed record SubHit(int Tick, int HitsTaken) : GameEvent(Tick);
public sealed record SubDestroyed(int Tick, SubDeathCause Cause) : GameEvent(Tick);

public enum DestructionCause { DirectHit, Blast, Defense, TorpedoCollision }
public enum RejectionReason { NoAmmo, Reloading, NoneAvailable }

/// <summary>
/// Cómo terminó la corrida. Morir por descuido de rumbo y morir bajo fuego son dos lecciones
/// distintas, y la telemetría necesita poder separarlas.
/// </summary>
public enum SubDeathCause { Torpedoes, Collision }
