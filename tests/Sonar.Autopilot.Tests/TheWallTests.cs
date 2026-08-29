using System.Linq;
using Sonar.Autopilot.Tests.Support;
using Sonar.Controller;
using Sonar.Core;
using Sonar.Core.Spatial;
using Xunit;
using Xunit.Abstractions;

namespace Sonar.Autopilot.Tests;

/// <summary>
/// La corrida completa, con la configuración real, jugada por el piloto hasta morir — o
/// hasta que se acabe el tiempo, que resultó ser lo que pasa la mayoría de las veces.
///
/// Ningún número exacto se afirma: ni el tick de muerte, ni la oleada, ni el valor de S. M1
/// es un milestone de tuning y un test que afirme "muere en el tick 8943" se pone rojo cada
/// vez que se hace el trabajo del milestone. Lo que se afirma es la FORMA de la corrida.
/// Los números van al reporte, que se imprime.
/// </summary>
public class TheWallTests
{
    private const double SubSpeed = 0.012;
    private const double Pace = 1.5;

    /// <summary>Mil segundos de juego. Es un tope de cordura, no una expectativa.</summary>
    private const int MaxTicks = 60000;

    /// <summary>
    /// La semilla del juego, y la del modo demo. Se eligió mirando el reporte de las cinco:
    /// es la que muere antes, en la oleada más alta y con más enemigos vivos encima.
    /// </summary>
    private const int DemoSeed = 20260811;

    private readonly ITestOutputHelper _output;

    public TheWallTests(ITestOutputHelper output) => _output = output;

    private RunReport Play(int seed, out GameController controller, out SimulationConfig config)
    {
        config = WorkingConfig.Load();
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
        // EL CONTROL POSITIVO. Sin él, un piloto que no teclea nada pasaría trivialmente
        // cualquier afirmación sobre la asfixia: se ahoga en la oleada 1 y el test lo
        // celebraría. Es exactamente la forma de test vacuo que ya se coló dos veces acá.
        var report = Play(seed, out _, out _);

        Assert.NotEmpty(report.Waves);
        Assert.True(report.DeathWave >= 2,
            $"se quedó en la oleada {report.DeathWave}: no pasó de la primera");
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
        // exactamente "una respuesta por amenaza", o sea seguirle el ritmo justo.
        //
        // Es también el control del medidor por un lado: uno que devolviera siempre un
        // número alto rompe acá. TheDemoSeedDiesUnderPressure lo controla por el otro.
        var report = Play(seed, out _, out _);

        Assert.NotEmpty(report.Waves);
        Assert.Equal(1, report.Waves[0].Wave);
        Assert.True(report.Waves[0].PeakSaturation <= 1.0,
            $"S llegó a {report.Waves[0].PeakSaturation:F2} durante la oleada 1");
    }

    [Theory]
    [InlineData(20260811)]
    [InlineData(11111111)]
    [InlineData(22222222)]
    [InlineData(33333333)]
    [InlineData(44444444)]
    public void EveryRunEndsWithAnEmptyLocker(int seed)
    {
        // EL HALLAZGO DEL PLAN 9, como afirmación. Ninguna corrida termina porque el jugador
        // no diera abasto: terminan porque se quedó sin torpedos, y las cinco gastan el pañol
        // COMPLETO. Con él vacío el piloto sigue teniendo soluciones de tiro válidas —errores
        // de 0,005 a 0,066 contra un radio de explosión de 0,100— y nada con qué usarlas.
        //
        // Es una afirmación más fuerte que "murió", y a diferencia de aquélla es cierta en las
        // cinco semillas. Si algún día se pone roja, la respuesta a la pregunta 7 de M1 cambió,
        // y eso es justo lo que uno querría que le avisaran.
        var report = Play(seed, out var controller, out var config);

        Assert.Equal(config.MagazineSize + config.AmmoReserve, report.Waves.Sum(w => w.Shots));
        Assert.Equal(0, controller.Snapshot.AmmoReserve);
        Assert.Equal(0, controller.Snapshot.AmmoLoaded);
    }

    [Fact]
    public void TheDemoSeedDiesUnderPressure()
    {
        // La semilla del modo demo, fijada acá porque es la corrida que se mira. Tres de las
        // cinco semillas NO mueren —quedan a la deriva sin munición, casi inmortales gracias
        // a la defensa— así que afirmar la muerte en las cinco sería afirmar algo falso. Está
        // documentado en docs/development/m1-findings.md.
        //
        // Es también el control del medidor por el lado contrario a TheFirstWaveIsNotAWall:
        // uno que devolviera siempre cero rompe acá.
        var report = Play(DemoSeed, out var controller, out _);

        Assert.True(controller.Snapshot.Destroyed,
            $"la semilla de la demo sobrevivió {MaxTicks} ticks");
        Assert.True(report.DeathWave >= 4,
            $"murió en la oleada {report.DeathWave}, antes de que la presión se acumulara");

        // Murió con la escena encima, no en un descuido: enemigos vivos y el índice por
        // arriba de 1, y más alto que un minuto antes.
        Assert.True(report.Waves[^1].EnemiesLeft >= 2,
            $"sólo quedaban {report.Waves[^1].EnemiesLeft} enemigos vivos al morir");
        Assert.True(report.DeathSaturation > 1.0,
            $"S en la muerte fue {report.DeathSaturation:F2}: no murió bajo presión");
        Assert.True(report.DeathSaturation > report.SaturationAtSecond(report.DeathTick / 60 - 60),
            "S no venía subiendo");
    }
}
