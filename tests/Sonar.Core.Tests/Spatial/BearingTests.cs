using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Core.Tests.Spatial;

public class BearingTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(47, 47)]
    [InlineData(359, 359)]
    [InlineData(360, 0)]
    [InlineData(361, 1)]
    [InlineData(720, 0)]
    [InlineData(-1, 359)]
    [InlineData(-360, 0)]
    [InlineData(-361, 359)]
    public void Constructor_NormalizesToZeroThreeFiftyNine(int input, int expected)
    {
        Assert.Equal(expected, new Bearing(input).Degrees);
    }

    [Theory]
    [InlineData(0, 0, -1)]
    [InlineData(90, 1, 0)]
    [InlineData(180, 0, 1)]
    [InlineData(270, -1, 0)]
    public void ToUnitVector_MapsCardinalDirections(int degrees, double x, double y)
    {
        var v = new Bearing(degrees).ToUnitVector();
        Assert.Equal(x, v.X, precision: 10);
        Assert.Equal(y, v.Y, precision: 10);
    }

    [Fact]
    public void ToUnitVector_AlwaysHasUnitLength()
    {
        for (var d = 0; d < 360; d++)
        {
            Assert.Equal(1.0, new Bearing(d).ToUnitVector().Length, precision: 10);
        }
    }

    [Theory]
    [InlineData(0, -1, 0)]
    [InlineData(1, 0, 90)]
    [InlineData(0, 1, 180)]
    [InlineData(-1, 0, 270)]
    public void FromVector_MapsCardinalDirections(double x, double y, int expected)
    {
        Assert.Equal(expected, Bearing.FromVector(new Vec2(x, y)).Degrees);
    }

    [Fact]
    public void FromVector_IsInverseOfToUnitVector()
    {
        for (var d = 0; d < 360; d++)
        {
            var roundTripped = Bearing.FromVector(new Bearing(d).ToUnitVector());
            Assert.Equal(d, roundTripped.Degrees);
        }
    }

    [Fact]
    public void FromVector_OfZeroVector_ReturnsZero()
    {
        Assert.Equal(0, Bearing.FromVector(Vec2.Zero).Degrees);
    }
}
