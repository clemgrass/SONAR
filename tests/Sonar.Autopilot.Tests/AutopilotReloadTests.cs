using Sonar.Autopilot.Tests.Support;
using Sonar.Controller.Presentation;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Autopilot.Tests;

public class AutopilotReloadTests
{
    private static Pilot Idle() => new(TestConfig.Build());

    [Fact]
    public void EmptyTubes_MeanReload()
    {
        var pilot = Idle();
        Assert.Equal("RELOAD", pilot.Decide(TestSnapshot.Build(0, ammoLoaded: 0)));
    }

    [Fact]
    public void AHalfFullMagazine_IsToppedUpWhenThereIsNothingToShoot()
    {
        var pilot = Idle();
        Assert.Equal("RELOAD", pilot.Decide(TestSnapshot.Build(0, ammoLoaded: 2)));
    }

    [Fact]
    public void FullTubes_NeedNothing()
    {
        var pilot = Idle();
        Assert.Null(pilot.Decide(TestSnapshot.Build(0, ammoLoaded: 3)));
    }

    [Fact]
    public void ASecondReloadIsNotTypedWhileOneIsRunning()
    {
        // La simulación lo rechazaría en silencio: `if (ReloadInProgress) return state`.
        var pilot = Idle();
        Assert.Null(pilot.Decide(TestSnapshot.Build(0, ammoLoaded: 0, reloadInProgress: true)));
    }

    [Fact]
    public void WithNoReserve_ThereIsNothingToLoad()
    {
        var pilot = Idle();
        Assert.Null(pilot.Decide(TestSnapshot.Build(0, ammoLoaded: 0, ammoReserve: 0)));
    }

    [Fact]
    public void ShootingComesBeforeToppingUp()
    {
        // La recarga es oportunista, no codiciosa: con un blanco resuelto y tubos a medias,
        // el turno es para el disparo.
        var pilot = new Pilot(TestConfig.Build());
        var first = new[] { new ContactView(100, new Vec2(0, -0.90), 0, 'E', 0) };
        var second = new[] { new ContactView(100, new Vec2(0, -0.795), 0, 'D', 0) };
        pilot.Observe(TestSnapshot.Build(0, first));
        pilot.Observe(TestSnapshot.Build(420, second));

        Assert.Equal("C000", pilot.Decide(TestSnapshot.Build(420, second, ammoLoaded: 2)));
    }
}
