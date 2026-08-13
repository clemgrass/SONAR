using Sonar.Core.Commands;
using Xunit;

namespace Sonar.Controller.Tests;

public class CommandParserTests
{
    private static readonly CommandParser Parser = new(bandCount: 5);

    [Theory]
    [InlineData("C047", 'C', 47)]
    [InlineData("c47", 'C', 47)]
    [InlineData("FIRE C047", 'C', 47)]
    [InlineData("fire c047", 'C', 47)]
    [InlineData("F C047", 'C', 47)]
    [InlineData("  f   c 47  ", 'C', 47)]
    [InlineData("A000", 'A', 0)]
    [InlineData("E359", 'E', 359)]
    public void ParsesFireCommands(string input, char band, int bearing)
    {
        var command = Assert.IsType<FireCommand>(Parser.Parse(input));
        Assert.Equal(band, command.Target.Band.Letter);
        Assert.Equal(bearing, command.Target.Bearing.Degrees);
    }

    [Theory]
    [InlineData("DEF C047", 'C', 47)]
    [InlineData("DEFEND C047", 'C', 47)]
    [InlineData("def c47", 'C', 47)]
    public void ParsesDefenseCommands(string input, char band, int bearing)
    {
        var command = Assert.IsType<DeployDefenseCommand>(Parser.Parse(input));
        Assert.Equal(band, command.Target.Band.Letter);
        Assert.Equal(bearing, command.Target.Bearing.Degrees);
    }

    [Theory]
    [InlineData("RELOAD")]
    [InlineData("reload")]
    [InlineData("  Reload  ")]
    public void ParsesReload(string input) => Assert.IsType<ReloadCommand>(Parser.Parse(input));

    [Fact]
    public void ParsesSonar() => Assert.IsType<ToggleSonarCommand>(Parser.Parse("SONAR"));

    [Fact]
    public void ParsesStatus() => Assert.IsType<StatusCommand>(Parser.Parse("STATUS"));

    [Theory]
    [InlineData("C400")]   // marcación fuera de rango: no se toma módulo
    [InlineData("C360")]
    [InlineData("Z047")]   // letra fuera del conteo de bandas
    [InlineData("F047")]   // con cinco bandas, F no existe
    [InlineData("hello")]
    [InlineData("C")]
    [InlineData("047")]
    [InlineData("C0470")]
    [InlineData("FIRE")]
    public void UnknownOrInvalidInput_BecomesUnrecognized(string input)
    {
        var command = Assert.IsType<UnrecognizedInput>(Parser.Parse(input));
        Assert.Equal(input, command.RawText);
    }

    [Fact]
    public void UnrecognizedInput_PreservesTheOriginalTextNotTheNormalized()
    {
        // El historial debe mostrar lo que el jugador escribió, no una versión limpia.
        var command = Assert.IsType<UnrecognizedInput>(Parser.Parse("  Hello There  "));
        Assert.Equal("  Hello There  ", command.RawText);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyInput_ReturnsNull(string input) => Assert.Null(Parser.Parse(input));

    [Theory]
    [InlineData("RETRY")]
    [InlineData("retry")]
    [InlineData("  Retry  ")]
    public void ParsesRetry(string input) => Assert.IsType<RetryCommand>(Parser.Parse(input));

    [Theory]
    [InlineData("START")]
    [InlineData("start")]
    [InlineData("  Start  ")]
    public void ParsesStart(string input) => Assert.IsType<StartCommand>(Parser.Parse(input));

    [Fact]
    public void BandCountIsRespected()
    {
        var threeBands = new CommandParser(bandCount: 3);
        Assert.IsType<FireCommand>(threeBands.Parse("C047"));
        Assert.IsType<UnrecognizedInput>(threeBands.Parse("D047"));
    }

    [Fact]
    public void OutOfRangeBearing_IsNotTakenModulo()
    {
        // Vale la pena por separado: C400 debe no hacer nada, y muy en particular NO
        // disparar a C040. Un módulo silencioso convertiría un error de tipeo en un
        // disparo a otra parte, y con munición limitada eso se paga caro.
        Assert.IsType<UnrecognizedInput>(Parser.Parse("C400"));
    }
}
