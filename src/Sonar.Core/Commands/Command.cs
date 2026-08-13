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

/// <summary>
/// Reinicio puro: no persiste nada (spec §4.4). Lo atiende el Controller y no la simulación,
/// porque con el submarino destruido el bucle se congela y nunca lo consumiría.
/// </summary>
public sealed record RetryCommand : Command;

/// <summary>
/// Arranca la corrida. Hasta que llegue, el mundo está detenido: la demo tiene que poder
/// explicarse sola a alguien que la abre sin nadie al lado, y eso exige un momento sin reloj
/// corriendo en el que leer las instrucciones.
/// </summary>
public sealed record StartCommand : Command;
