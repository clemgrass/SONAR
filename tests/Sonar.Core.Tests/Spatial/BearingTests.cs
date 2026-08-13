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

    [Fact]
    public void DegreesOf_KeepsFractionsThatFromVectorRoundsAway()
    {
        // Un contacto a 0.5 grados al este del norte. FromVector devolvería 000 o 001;
        // para medir error de puntería hace falta el valor sin redondear.
        var v = new Vec2(System.Math.Sin(0.5 * System.Math.PI / 180.0),
                        -System.Math.Cos(0.5 * System.Math.PI / 180.0));

        Assert.Equal(0.5, Bearing.DegreesOf(v), precision: 6);
    }

    [Fact]
    public void DegreesOf_IsAlwaysInRange()
    {
        Assert.Equal(90.0, Bearing.DegreesOf(new Vec2(1, 0)), precision: 6);
        Assert.Equal(180.0, Bearing.DegreesOf(new Vec2(0, 1)), precision: 6);
        Assert.Equal(270.0, Bearing.DegreesOf(new Vec2(-1, 0)), precision: 6);
        Assert.Equal(0.0, Bearing.DegreesOf(new Vec2(0, -1)), precision: 6);
        Assert.Equal(0.0, Bearing.DegreesOf(Vec2.Zero), precision: 6);
    }

    [Fact]
    public void Separation_TakesTheShortWayAround()
    {
        Assert.Equal(10.0, Bearing.Separation(5.0, 355.0), precision: 6);
        Assert.Equal(10.0, Bearing.Separation(355.0, 5.0), precision: 6);
        Assert.Equal(180.0, Bearing.Separation(0.0, 180.0), precision: 6);
        Assert.Equal(0.0, Bearing.Separation(47.0, 47.0), precision: 6);
    }
}
