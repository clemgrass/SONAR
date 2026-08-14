using System.IO;
using Sonar.Core;
using Xunit;

namespace Sonar.Core.Tests;

public class SimulationConfigLoaderTests
{
    [Fact]
    public void Load_ParsesJsonAndValidates()
    {
        var json = """
        {
          "tickDurationSeconds": 0.016666666666666666,
          "maxCatchUpTicks": 5,
          "deadZoneRadius": 0.10,
          "bandOuterRadii": [0.28, 0.46, 0.64, 0.82, 1.00],
          "sweepPeriodSeconds": 3.5,
          "torpedoSpeed": 0.70,
          "hitRadius": 0.040,
          "blastRadius": 0.100,
          "nearMissRadius": 0.160,
          "enemyTorpedoSpeed": 0.45,
          "enemyFireIntervalTicks": 420,
          "enemyFireStaggerTicks": 70,
          "subHitRadius": 0.045,
          "defenseRadius": 0.070,
          "defenseCapacity": 3,
          "magazineSize": 4,
          "ammoReserve": 12,
          "reloadTicks": 300,
          "enemyReloadTicks": 600
        }
        """;

        var config = SimulationConfigLoader.LoadFromJson(json);

        Assert.Equal(5, config.BandOuterRadii.Count);
        Assert.Equal(0.70, config.TorpedoSpeed, precision: 10);
        Assert.Equal(420, config.EnemyFireIntervalTicks);
    }

    [Fact]
    public void Load_ThrowsOnInvariantViolation()
    {
        var json = """
        {
          "tickDurationSeconds": 0.016666666666666666,
          "maxCatchUpTicks": 5,
          "deadZoneRadius": 0.10,
          "bandOuterRadii": [0.28, 0.46, 0.64, 0.82, 1.00],
          "sweepPeriodSeconds": 3.5,
          "torpedoSpeed": 0.70,
          "hitRadius": 0.001,
          "blastRadius": 0.100,
          "nearMissRadius": 0.160,
          "enemyTorpedoSpeed": 0.45,
          "enemyFireIntervalTicks": 420,
          "enemyFireStaggerTicks": 70,
          "subHitRadius": 0.045,
          "defenseRadius": 0.070,
          "defenseCapacity": 3,
          "magazineSize": 4,
          "ammoReserve": 12,
          "reloadTicks": 300,
          "enemyReloadTicks": 600
        }
        """;

        Assert.Throws<System.ArgumentException>(() => SimulationConfigLoader.LoadFromJson(json));
    }

    [Fact]
    public void WorkingConfigOnDisk_IsValid()
    {
        var path = Path.Combine(RepoRoot(), "config", "simulation.working.json");
        Assert.True(File.Exists(path), $"No se encontró {path}");
        SimulationConfigLoader.LoadFromJson(File.ReadAllText(path));   // no debe lanzar
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Sonar.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
