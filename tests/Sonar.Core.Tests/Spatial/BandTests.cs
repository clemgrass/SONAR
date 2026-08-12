using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Core.Tests.Spatial;

public class BandTests
{
    [Theory]
    [InlineData(0, 'A')]
    [InlineData(2, 'C')]
    [InlineData(4, 'E')]
    [InlineData(7, 'H')]
    public void Letter_IsDerivedFromIndex(int index, char expected)
    {
        Assert.Equal(expected, new Band(index).Letter);
    }

    [Theory]
    [InlineData('A', 5, 0)]
    [InlineData('C', 5, 2)]
    [InlineData('E', 5, 4)]
    [InlineData('a', 5, 0)]
    [InlineData('c', 5, 2)]
    public void TryParseLetter_AcceptsLettersWithinBandCount(char letter, int bandCount, int expectedIndex)
    {
        Assert.True(Band.TryParseLetter(letter, bandCount, out var band));
        Assert.Equal(expectedIndex, band.Index);
    }

    [Theory]
    [InlineData('F', 5)]
    [InlineData('Z', 5)]
    [InlineData('D', 3)]
    [InlineData('1', 5)]
    [InlineData('-', 5)]
    public void TryParseLetter_RejectsLettersOutsideBandCount(char letter, int bandCount)
    {
        Assert.False(Band.TryParseLetter(letter, bandCount, out _));
    }
}
