using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Core.Tests.Spatial;

public class TargetCoordinateTests
{
    private static BandLayout FiveBands() =>
        new(deadZoneRadius: 0.10, outerRadii: new[] { 0.28, 0.46, 0.64, 0.82, 1.00 });

    [Fact]
    public void ToString_FormatsAsLetterThenThreeDigits()
    {
        var target = new TargetCoordinate(new Bearing(47), new Band(2));
        Assert.Equal("C047", target.ToString());
    }

    [Fact]
    public void ToString_PadsSingleDigitBearing()
    {
        var target = new TargetCoordinate(new Bearing(7), new Band(0));
        Assert.Equal("A007", target.ToString());
    }

    [Fact]
    public void ToPosition_IsAtBandCenterRadius()
    {
        var layout = FiveBands();
        var target = new TargetCoordinate(new Bearing(0), new Band(2));
        var position = target.ToPosition(layout);

        // Marcación 0 es norte: (0, -centro). Centro de C = 0.55.
        Assert.Equal(0.0, position.X, precision: 10);
        Assert.Equal(-0.55, position.Y, precision: 10);
    }

    [Fact]
    public void ToPosition_LengthEqualsBandCenterRadius()
    {
        var layout = FiveBands();
        var target = new TargetCoordinate(new Bearing(137), new Band(3));
        Assert.Equal(layout.CenterRadius(new Band(3)), target.ToPosition(layout).Length, precision: 10);
    }
}
