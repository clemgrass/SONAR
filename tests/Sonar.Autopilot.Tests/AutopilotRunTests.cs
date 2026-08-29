using System.Collections.Generic;
using Sonar.Autopilot.Tests.Support;
using Sonar.Controller;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Autopilot.Tests;

public class AutopilotRunTests
{
    private const int Seed = 20260828;
    private const double SubSpeed = 0.012;

    private static (GameController Controller, AutopilotRun Run) Build(double pace = 1.5)
    {
        var config = TestConfig.Build();
        var controller = new GameController(config, Seed, new Bearing(0), SubSpeed);
        return (controller, new AutopilotRun(config, controller, pace));
    }

    private static List<string> Commands(AutopilotRun run, List<int>? ticks = null)
    {
        var log = new List<string>();
        run.CommandEmitted += (tick, text) => { log.Add(text); ticks?.Add(tick); };
        return log;
    }

    [Fact]
    public void TheRunTypesStartByItself()
    {
        // Arrancar una sesión no es táctica: lo hace el arnés, no el piloto.
        var (controller, _) = Build();
        Assert.True(controller.Started);
    }

    [Fact]
    public void OneStep_IsExactlyOneTick()
    {
        var (controller, run) = Build();
        run.StepOneTick();
        Assert.Equal(1, controller.Snapshot.Tick);

        run.StepOneTick();
        Assert.Equal(2, controller.Snapshot.Tick);
    }

    [Fact]
    public void ADeadRunDoesNotStep()
    {
        // Con el submarino destruido el tick queda congelado; un bucle que espere a que
        // avance nunca termina. Esta guarda es lo que impide que la suite se cuelgue.
        var (controller, run) = Build();
        run.StepOneTick();
        var frozen = controller.Snapshot.Tick;

        controller.ForceDestroyForTests();
        run.StepOneTick();

        Assert.True(run.Finished);
        Assert.Equal(frozen, controller.Snapshot.Tick);
    }

    [Fact]
    public void TheBudgetPacesTheCommands()
    {
        var (_, run) = Build(pace: 1.5);
        var ticks = new List<int>();
        var log = Commands(run, ticks);

        for (var i = 0; i < 12000; i++) run.StepOneTick();

        // Control positivo: sin esto, un piloto que no teclea nada pasaría el test.
        Assert.True(log.Count >= 3, $"sólo {log.Count} comandos en 12000 ticks");

        for (var i = 1; i < ticks.Count; i++)
            Assert.True(ticks[i] - ticks[i - 1] >= 90,
                $"comandos en {ticks[i - 1]} y {ticks[i]}: menos de 1,5 s de separación");
    }

    [Fact]
    public void SameSeedAndPace_ProduceTheSameRun()
    {
        // Lo que hace que mirar la demo y correr el test sean la misma corrida.
        var (_, first) = Build();
        var firstLog = Commands(first);
        for (var i = 0; i < 6000; i++) first.StepOneTick();

        var (_, second) = Build();
        var secondLog = Commands(second);
        for (var i = 0; i < 6000; i++) second.StepOneTick();

        Assert.NotEmpty(firstLog);          // dos listas vacías también son iguales
        Assert.Equal(firstLog, secondLog);
    }
}
