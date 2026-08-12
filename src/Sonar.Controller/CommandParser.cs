using System.Text.RegularExpressions;
using Sonar.Core.Commands;
using Sonar.Core.Spatial;

namespace Sonar.Controller;

/// <summary>
/// Texto crudo a comando. Vive en el Controller, no en Core: Core recibe comandos ya
/// construidos. Gramática en TDD §6.2.
/// </summary>
public sealed class CommandParser
{
    private static readonly Regex CoordinatePattern =
        new(@"^(?:(?:FIRE|F)\s+)?([A-Z])\s*(\d{1,3})$", RegexOptions.Compiled);

    private static readonly Regex DefensePattern =
        new(@"^(?:DEF|DEFEND)\s+([A-Z])\s*(\d{1,3})$", RegexOptions.Compiled);

    private readonly int _bandCount;

    public CommandParser(int bandCount) => _bandCount = bandCount;

    /// <summary>
    /// Devuelve null si el texto está vacío — no hay nada que registrar. Cualquier otro
    /// texto que no corresponda a un comando devuelve UnrecognizedInput, que no es un
    /// error: el §10 del GDD pide que todo input quede registrado y que lo desconocido
    /// simplemente no haga nada.
    /// </summary>
    public Command? Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var normalized = Regex.Replace(raw.Trim(), @"\s+", " ").ToUpperInvariant();

        switch (normalized)
        {
            case "RELOAD": return new ReloadCommand();
            case "SONAR": return new ToggleSonarCommand();
            case "STATUS": return new StatusCommand();
        }

        var defense = DefensePattern.Match(normalized);
        if (defense.Success && TryTarget(defense, out var defenseTarget))
            return new DeployDefenseCommand(defenseTarget);

        var fire = CoordinatePattern.Match(normalized);
        if (fire.Success && TryTarget(fire, out var fireTarget))
            return new FireCommand(fireTarget);

        return new UnrecognizedInput(raw);
    }

    private bool TryTarget(Match match, out TargetCoordinate target)
    {
        target = default;

        if (!Band.TryParseLetter(match.Groups[1].Value[0], _bandCount, out var band))
            return false;

        if (!int.TryParse(match.Groups[2].Value, out var degrees)) return false;

        // Una marcación fuera de rango no se toma módulo: tomarlo haría que C400
        // disparara silenciosamente a C040. El jugador escribió algo que no existe y
        // el sistema debe no hacer nada (TDD §6.2).
        if (degrees is < 0 or > 359) return false;

        target = new TargetCoordinate(new Bearing(degrees), band);
        return true;
    }
}
