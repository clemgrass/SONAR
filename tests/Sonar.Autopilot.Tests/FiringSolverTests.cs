using Sonar.Autopilot.Tests.Support;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Autopilot.Tests;

public class FiringSolverTests
{
    private static readonly Sonar.Core.SimulationConfig Config = TestConfig.Build();

    [Fact]
    public void AStationaryTarget_IsShotWhereItSits()
    {
        // En el centro de la banda C, marcación 000, quieto. El error debe ser cero.
        var estimate = new ReadingEstimate(new Vec2(0, -0.55), Vec2.Zero, 0);

        Assert.True(FiringSolver.TrySolve(Config, Config.BuildBandLayout(), estimate, 0, out var solution));
        Assert.Equal('C', solution.Band.Letter);
        Assert.Equal(0, solution.Bearing.Degrees);
        Assert.True(solution.ErrorRadius < 1e-9, $"error {solution.ErrorRadius}");
    }

    [Fact]
    public void AClosingTarget_IsShotWhereItWillBe()
    {
        // El corazón del juego. El blanco está a 0.90 — banda E — y cierra al techo de
        // 0.015 por segundo. El disparo correcto es a la banda D, porque en los 14,7 s que
        // tarda el torpedo en llegar el blanco ya no está donde se lo leyó.
        var estimate = new ReadingEstimate(new Vec2(0, -0.90), new Vec2(0, 0.015 / 60.0), 0);

        Assert.True(FiringSolver.TrySolve(Config, Config.BuildBandLayout(), estimate, 0, out var solution));
        Assert.Equal('D', solution.Band.Letter);
        Assert.Equal(0, solution.Bearing.Degrees);

        // Control positivo de que esto NO es la banda donde está el blanco ahora.
        Assert.True(Config.BuildBandLayout().TryBandAt(0.90, out var current));
        Assert.Equal('E', current.Letter);
    }

    [Fact]
    public void NoSolution_WhenEveryBandMissesByMoreThanTheBlast()
    {
        // Dentro de la zona muerta no hay banda que sirva: el centro de la banda A está a
        // 0.19 y el blanco a 0.02, así que el error mínimo supera el radio de explosión.
        var estimate = new ReadingEstimate(new Vec2(0, -0.02), Vec2.Zero, 0);

        Assert.False(FiringSolver.TrySolve(Config, Config.BuildBandLayout(), estimate, 0, out _));
    }

    [Fact]
    public void TheBearingFollowsTheTargetAround()
    {
        // Mismo caso que el primero pero al este: la marcación tiene que ser 090, no 000.
        var estimate = new ReadingEstimate(new Vec2(0.55, 0), Vec2.Zero, 0);

        Assert.True(FiringSolver.TrySolve(Config, Config.BuildBandLayout(), estimate, 0, out var solution));
        Assert.Equal(90, solution.Bearing.Degrees);
        Assert.Equal('C', solution.Band.Letter);
    }

    [Fact]
    public void TheAgeOfTheReadingIsPaidFor()
    {
        // La misma lectura, resuelta 600 ticks después de haberse tomado, tiene que apuntar
        // más adentro: el blanco siguió cerrando mientras la lectura envejecía.
        var estimate = new ReadingEstimate(new Vec2(0, -0.90), new Vec2(0, 0.015 / 60.0), 0);

        Assert.True(FiringSolver.TrySolve(Config, Config.BuildBandLayout(), estimate, 0, out var fresh));
        Assert.True(FiringSolver.TrySolve(Config, Config.BuildBandLayout(), estimate, 600, out var stale));

        Assert.True(stale.Band.Index < fresh.Band.Index,
            $"fresca {fresh.Band.Letter}, vieja {stale.Band.Letter}");
    }
}
