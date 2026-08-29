using Sonar.Autopilot.Tests.Support;
using Sonar.Controller;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Autopilot.Tests;

public class RunReportTests
{
    [Fact]
    public void TheReportKnowsWhichWavesHappened()
    {
        var config = TestConfig.Build();
        var controller = new GameController(config, 20260828, new Bearing(0), 0.012);
        var run = new AutopilotRun(config, controller, 1.5);

        for (var i = 0; i < 12000; i++) run.StepOneTick();
        var report = run.BuildReport();

        // Control positivo: sin oleadas la tabla estaría vacía y todo lo demás sería vacuo.
        Assert.NotEmpty(report.Waves);
        Assert.Equal(1, report.Waves[0].Wave);
        Assert.True(report.Waves[0].StartTick >= WaveDirector.OpeningQuietTicks,
            "la oleada 1 no puede empezar antes del silencio inicial");
    }

    [Fact]
    public void SaturationIsSampledOncePerSecond()
    {
        var config = TestConfig.Build();
        var controller = new GameController(config, 20260828, new Bearing(0), 0.012);
        var run = new AutopilotRun(config, controller, 1.5);

        for (var i = 0; i < 600; i++) run.StepOneTick();
        var report = run.BuildReport();

        // 600 ticks a 60 por segundo son diez muestras (los ticks 0, 60, … 540).
        Assert.Equal(10, report.SaturationPerSecond.Count);
    }

    [Fact]
    public void TheReportRendersWithoutBlowingUp()
    {
        var config = TestConfig.Build();
        var controller = new GameController(config, 20260828, new Bearing(0), 0.012);
        var run = new AutopilotRun(config, controller, 1.5);

        for (var i = 0; i < 12000; i++) run.StepOneTick();

        var text = run.BuildReport().Render();
        Assert.Contains("WAVE", text);

        // La semilla del reporte es la del INTENTO, no la base: GameController la deriva
        // como baseSeed + intento * 7919 para que los reintentos no repitan los spawns.
        Assert.Contains(controller.Seed.ToString(), text);
    }
}
