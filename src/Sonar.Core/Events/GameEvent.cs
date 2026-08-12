using Sonar.Core.Spatial;

namespace Sonar.Core.Events;

public abstract record GameEvent(int Tick);

// --- Sistemas ---
public sealed record SonarToggled(int Tick, bool Running) : GameEvent(Tick);
public sealed record StatusReported(int Tick, int AmmoLoaded, int AmmoReserve, int Defenses)
    : GameEvent(Tick);
public sealed record InputNotRecognized(int Tick, string RawText) : GameEvent(Tick);
