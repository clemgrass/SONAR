using Sonar.Core.Entities;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Core.Tests.Entities;

public class TorpedoTests
{
    private static BandLayout FiveBands() =>
        new(deadZoneRadius: 0.10, outerRadii: new[] { 0.28, 0.46, 0.64, 0.82, 1.00 });

    [Fact]
    public void CreateOwn_StartsAtDeadZoneEdgeOnTheGivenBearing()
    {
        var layout = FiveBands();
        var target = new TargetCoordinate(new Bearing(90), new Band(2));
        var torpedo = Torpedo.CreateOwn(new TorpedoId(1), target, layout, speed: 0.7);

        // Marcación 90 es este: (1, 0). Nace al borde de la zona muerta.
        Assert.Equal(0.10, torpedo.Position.X, precision: 10);
        Assert.Equal(0.00, torpedo.Position.Y, precision: 10);
        Assert.Equal(TorpedoSide.Own, torpedo.Side);
    }

    [Fact]
    public void CreateOwn_RunsToBandCenterRadius()
    {
        var layout = FiveBands();
        var target = new TargetCoordinate(new Bearing(0), new Band(2));
        var torpedo = Torpedo.CreateOwn(new TorpedoId(1), target, layout, speed: 0.7);

        Assert.Equal(0.55, torpedo.RunToRadius, precision: 10);
        Assert.Equal(0.10, torpedo.ArmedFromRadius, precision: 10);
    }

    [Fact]
    public void CreateOwn_DirectionMatchesBearing()
    {
        var layout = FiveBands();
        var target = new TargetCoordinate(new Bearing(180), new Band(1));
        var torpedo = Torpedo.CreateOwn(new TorpedoId(1), target, layout, speed: 0.7);

        // Marcación 180 es sur: (0, 1).
        Assert.Equal(0.0, torpedo.Direction.X, precision: 10);
        Assert.Equal(1.0, torpedo.Direction.Y, precision: 10);
    }

    [Fact]
    public void CreateEnemy_TravelsTowardTheOrigin()
    {
        var origin = new Vec2(0.6, 0.8);   // longitud 1.0
        var torpedo = Torpedo.CreateEnemy(new TorpedoId(2), origin, speed: 0.45);

        Assert.Equal(TorpedoSide.Enemy, torpedo.Side);
        Assert.Equal(origin, torpedo.Position);
        // Dirección unitaria apuntando al origen: (-0.6, -0.8)
        Assert.Equal(-0.6, torpedo.Direction.X, precision: 10);
        Assert.Equal(-0.8, torpedo.Direction.Y, precision: 10);
        Assert.Equal(1.0, torpedo.Direction.Length, precision: 10);
    }
}
