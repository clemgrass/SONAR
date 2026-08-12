using System.Collections.Generic;
using Sonar.Core;
using Xunit;

namespace Sonar.Core.Tests;

public class SeededRandomSourceTests
{
    private static List<double> Draw(int seed, int count)
    {
        var source = new SeededRandomSource(seed);
        var values = new List<double>();
        for (var i = 0; i < count; i++) values.Add(source.NextDouble());
        return values;
    }

    [Fact]
    public void SameSeed_ProducesIdenticalSequence()
    {
        Assert.Equal(Draw(12345, 200), Draw(12345, 200));
    }

    [Fact]
    public void DifferentSeed_ProducesDifferentSequence()
    {
        Assert.NotEqual(Draw(11111, 200), Draw(22222, 200));
    }

    [Fact]
    public void NextDouble_StaysInZeroToOne()
    {
        var source = new SeededRandomSource(7);
        for (var i = 0; i < 1000; i++)
        {
            var v = source.NextDouble();
            Assert.InRange(v, 0.0, 1.0);
        }
    }

    [Fact]
    public void NextInt_StaysWithinRequestedRange()
    {
        var source = new SeededRandomSource(7);
        for (var i = 0; i < 1000; i++)
        {
            var v = source.NextInt(5, 10);
            Assert.InRange(v, 5, 9);
        }
    }

    [Fact]
    public void NextInt_WithSingleValueRange_AlwaysReturnsThatValue()
    {
        var source = new SeededRandomSource(7);
        for (var i = 0; i < 50; i++) Assert.Equal(3, source.NextInt(3, 4));
    }
}
