using Sonar.Core.Spatial;

namespace Sonar.Core.Commands;

public abstract record Command;

public sealed record FireCommand(TargetCoordinate Target) : Command;

public sealed record DeployDefenseCommand(TargetCoordinate Target) : Command;

public sealed record ReloadCommand : Command;

public sealed record ToggleSonarCommand : Command;

public sealed record StatusCommand : Command;

/// <summary>
/// Texto que no corresponde a ningún comando. No es un error: el §10 del GDD pide que
/// todo input quede registrado y que lo desconocido simplemente no haga nada.
/// </summary>
public sealed record UnrecognizedInput(string RawText) : Command;
