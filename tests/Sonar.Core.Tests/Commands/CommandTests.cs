using Sonar.Core.Commands;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Core.Tests.Commands;

public class CommandTests
{
    [Fact]
    public void FireCommand_CarriesItsTarget()
    {
        var target = new TargetCoordinate(new Bearing(47), new Band(2));
        var command = new FireCommand(target);
        Assert.Equal(target, command.Target);
    }

    [Fact]
    public void UnrecognizedInput_IsACommandNotAnError()
    {
        // El §10 del GDD exige que todo input quede registrado y que lo desconocido
        // simplemente no haga nada. Modelarlo como comando mantiene el historial uniforme.
        Command command = new UnrecognizedInput("hello");
        Assert.IsType<UnrecognizedInput>(command);
        Assert.Equal("hello", ((UnrecognizedInput)command).RawText);
    }

    [Fact]
    public void Events_CarryTheTickTheyOccurredOn()
    {
        GameEvent evt = new SonarToggled(Tick: 42, Running: false);
        Assert.Equal(42, evt.Tick);
    }

    [Fact]
    public void InputNotRecognized_PreservesRawText()
    {
        var evt = new InputNotRecognized(7, "what is this");
        Assert.Equal("what is this", evt.RawText);
    }
}
