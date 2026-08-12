using System;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Core.Tests.Spatial;

public class BandLayoutTests
{
    private static BandLayout FiveBands() =>
        new(deadZoneRadius: 0.10, outerRadii: new[] { 0.28, 0.46, 0.64, 0.82, 1.00 });

    [Fact]
    public void Count_IsNumberOfOuterRadii()
    {
        Assert.Equal(5, FiveBands().Count);
    }

    [Fact]
    public void FirstBand_StartsAtDeadZone()
    {
        Assert.Equal(0.10, FiveBands().InnerRadius(new Band(0)), precision: 10);
    }

    [Fact]
    public void BandInner_IsPreviousBandOuter()
    {
        var layout = FiveBands();
        Assert.Equal(layout.OuterRadius(new Band(1)), layout.InnerRadius(new Band(2)), precision: 10);
    }

    [Fact]
    public void CenterRadius_IsMidpointOfBand()
    {
        // Banda C: [0.46, 0.64) -> centro 0.55
        Assert.Equal(0.55, FiveBands().CenterRadius(new Band(2)), precision: 10);
    }

    [Theory]
    [InlineData(0.20, 0)]
    [InlineData(0.35, 1)]
    [InlineData(0.55, 2)]
    [InlineData(0.70, 3)]
    [InlineData(0.95, 4)]
    public void BandAt_ReturnsBandContainingRadius(double radius, int expectedIndex)
    {
        Assert.True(FiveBands().TryBandAt(radius, out var band));
        Assert.Equal(expectedIndex, band.Index);
    }

    [Fact]
    public void BandAt_OnBoundary_BelongsToOuterBand()
    {
        // 0.28 es el radio exterior de A y el interior de B. Pertenece a B.
        Assert.True(FiveBands().TryBandAt(0.28, out var band));
        Assert.Equal(1, band.Index);
    }

    [Theory]
    [InlineData(0.05)]   // dentro de la zona muerta
    [InlineData(1.50)]   // fuera del scope
    public void BandAt_OutsideRange_ReturnsFalse(double radius)
    {
        Assert.False(FiveBands().TryBandAt(radius, out _));
    }

    [Fact]
    public void Constructor_RejectsNonIncreasingRadii()
    {
        Assert.Throws<ArgumentException>(() =>
            new BandLayout(0.10, new[] { 0.28, 0.20, 0.64 }));
    }

    [Fact]
    public void Constructor_RejectsFirstRadiusInsideDeadZone()
    {
        Assert.Throws<ArgumentException>(() =>
            new BandLayout(0.30, new[] { 0.28, 0.46 }));
    }

    [Fact]
    public void Constructor_RejectsEmptyRadii()
    {
        Assert.Throws<ArgumentException>(() => new BandLayout(0.10, Array.Empty<double>()));
    }
}
