using System.Linq;
using Sonar.Autopilot.Tests.Support;
using Sonar.Controller;
using Sonar.Core.Spatial;
using Xunit;
using Xunit.Abstractions;

namespace Sonar.Autopilot.Tests;

/// <summary>
/// La corrida completa, con la configuración real, jugada por el piloto hasta morir.
///
/// Ningún número exacto se afirma: ni el tick de muerte, ni la oleada, ni el valor de S. M1
/// es un milestone de tuning y un test que afirme "muere en el tick 8943" se pone rojo cada
/// vez que se hace el trabajo del milestone — y un test que se pone rojo por trabajo legítimo
/// se termina ajustando hasta que pase. Lo que se afirma es la FORMA de la muerte. Los
/// números van al reporte, que se imprime.
/// </summary>
public class TheWallTests
{
    private const double SubSpeed = 0.012;
    private const double Pace = 1.5;

    /// <summary>Mil segundos de juego. Si el piloto sobrevive a eso, el test falla con un mensaje.</summary>
    private const int MaxTicks = 60000;

    private readonly ITestOutputHelper _output;

    public TheWallTests(ITestOutputHelper output) => _output = output;

    private RunReport Play(int seed, out GameController controller)
    {
        var config = WorkingConfig.Load();
        controller = new GameController(config, seed, new Bearing(0), SubSpeed);
        var run = new AutopilotRun(config, controller, Pace);
        run.RunToDeath(MaxTicks);

        var report = run.BuildReport();
        _output.WriteLine(report.Render());
        return report;
    }

    [Theory]
    [InlineData(20260811)]
    [InlineData(11111111)]
    [InlineData(22222222)]
    [InlineData(33333333)]
    [InlineData(44444444)]
    public void ThePilotActuallyPlays(int seed)
    {
        // EL CONTROL POSITIVO. Sin él, un piloto que no teclea nada pasaría trivialmente la
        // afirmación de que murió ahogado: muere en la oleada 1 con S enorme y el test lo
        // celebraría. Es exactamente la forma de test vacuo que ya se coló dos veces acá.
        var report = Play(seed, out var controller);

        Assert.True(controller.Snapshot.Destroyed,
            $"el piloto sobrevivió {MaxTicks} ticks sin morir");
        Assert.NotEmpty(report.Waves);
        Assert.True(report.DeathWave >= 2,
            $"murió en la oleada {report.DeathWave}: no pasó de la primera");
        Assert.True(report.Waves.Sum(w => w.Kills) >= 1,
            "no destruyó ni un contacto en toda la corrida");
    }

    [Theory]
    [InlineData(20260811)]
    [InlineData(11111111)]
    [InlineData(22222222)]
    [InlineData(33333333)]
    [InlineData(44444444)]
    public void TheFirstWaveIsNotAWall(int seed)
    {
        // Iba sobrado al principio. El límite es no estricto a propósito: S = 1 es
        // exactamente "una respuesta por amenaza", o sea seguirle el ritmo justo. Quedarse
        // atrás empieza estrictamente por encima de 1.
        //
        // Este es también el control del medidor: uno que devolviera siempre un número alto
        // rompe acá.
        var report = Play(seed, out _);

        Assert.NotEmpty(report.Waves);
        Assert.Equal(1, report.Waves[0].Wave);
        Assert.True(report.Waves[0].PeakSaturation <= 1.0,
            $"S llegó a {report.Waves[0].PeakSaturation:F2} durante la oleada 1");
    }
}
