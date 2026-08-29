using System.IO;
using Sonar.Core;
using Xunit;

namespace Sonar.Autopilot.Tests.Support;

/// <summary>
/// La configuración real de disco, la misma que carga el juego. Sólo la usa el test de la
/// pared: lo que ese test mide ES esta configuración. Los unitarios usan TestConfig.
/// </summary>
internal static class WorkingConfig
{
    public static SimulationConfig Load()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Sonar.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);

        var path = Path.Combine(dir!.FullName, "config", "simulation.working.json");
        Assert.True(File.Exists(path), $"No se encontró {path}");
        return SimulationConfigLoader.LoadFromJson(File.ReadAllText(path));
    }
}
