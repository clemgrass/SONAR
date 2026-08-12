using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Core.Tests.Spatial;

public class Vec2Tests
{
    [Fact]
    public void Length_OfThreeFourVector_IsFive()
    {
        var v = new Vec2(3, 4);
        Assert.Equal(5.0, v.Length, precision: 10);
    }

    [Fact]
    public void Addition_AddsComponents()
    {
        var result = new Vec2(1, 2) + new Vec2(10, 20);
        Assert.Equal(new Vec2(11, 22), result);
    }

    [Fact]
    public void Subtraction_SubtractsComponents()
    {
        var result = new Vec2(10, 20) - new Vec2(1, 2);
        Assert.Equal(new Vec2(9, 18), result);
    }

    [Fact]
    public void ScalarMultiplication_ScalesBothComponents()
    {
        var result = new Vec2(3, -4) * 2;
        Assert.Equal(new Vec2(6, -8), result);
    }

    [Fact]
    public void Distance_BetweenTwoPoints_IsSymmetric()
    {
        var a = new Vec2(1, 1);
        var b = new Vec2(4, 5);
        Assert.Equal(5.0, Vec2.Distance(a, b), precision: 10);
        Assert.Equal(Vec2.Distance(a, b), Vec2.Distance(b, a), precision: 10);
    }

    [Fact]
    public void Normalized_OfNonZeroVector_HasUnitLength()
    {
        var v = new Vec2(3, 4).Normalized();
        Assert.Equal(1.0, v.Length, precision: 10);
    }

    [Fact]
    public void Normalized_OfZeroVector_ReturnsZero()
    {
        Assert.Equal(Vec2.Zero, Vec2.Zero.Normalized());
    }
}
