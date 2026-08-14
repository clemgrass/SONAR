using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sonar.Core;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Controller.Tests;

/// <summary>
/// El determinismo es lo que hace que RETRY sea un reset puro y que las sesiones de los
/// testers se puedan re-ejecutar y mirar. Con el gauntlet consumiendo azar, la promesa sigue
/// siendo la misma: misma semilla, mismo resultado.
/// </summary>
public class GauntletDeterminismTests
{
    private static SimulationConfig WorkingConfig()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Sonar.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return SimulationConfigLoader.LoadFromJson(
            File.ReadAllText(Path.Combine(dir!.FullName, "config", "simulation.working.json")));
    }

    /// Corre un guion de comandos por tick absoluto y devuelve el flujo de eventos.
    private static List<GameEvent> RunScripted(int baseSeed, SimulationConfig config, int ticks)
    {
        var controller = new GameController(config, baseSeed, new Bearing(0), 0.012);
        var script = new Dictionary<int, string>
        {
            [0] = "START",
            [700] = "C047",
            [900] = "STATUS",
            [1100] = "DEF C090",
            [1400] = "RELOAD",
            [2000] = "E200",
        };

        var events = new List<GameEvent>();
        for (var tick = 0; tick < ticks; tick++)
        {
            if (script.TryGetValue(tick, out var text)) controller.SubmitInput(text);
            events.AddRange(controller.Advance(config.TickDurationSeconds));
        }
        return events;
    }

    [Fact]
    public void SameSeed_ProducesIdenticalEventStreams()
    {
        var config = WorkingConfig();
        var a = RunScripted(4242, config, 3000);
        var b = RunScripted(4242, config, 3000);

        // Sin esto el test sería vacuo: dos flujos vacíos también son iguales.
        Assert.NotEmpty(a);
        Assert.Contains(a, e => e is TorpedoFired);

        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++) Assert.Equal(a[i], b[i]);
    }

    [Fact]
    public void DifferentSeed_ProducesADifferentRun()
    {
        // El primer test del proyecto que puede afirmar esto. Hasta el WaveDirector nada
        // consumía IRandomSource, y forzarlo antes habría sido inventar aleatoriedad para
        // satisfacer un test — ver la nota sobre el azar del plan 3.
        var config = WorkingConfig();
        var a = RunScripted(4242, config, 3000);
        var b = RunScripted(9001, config, 3000);

        Assert.NotEqual(a, b);
    }
}
