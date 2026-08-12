using Sonar.Core;
using Sonar.Core.Entities;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests;

public class StateHashTests
{
    private static WorldState Base() =>
        WorldState.CreateInitial(TestWorld.Config(), new Bearing(0), 0.05);

    [Fact]
    public void IdenticalStates_HashEqual()
    {
        Assert.Equal(StateHash.Compute(Base()), StateHash.Compute(Base()));
    }

    [Fact]
    public void DifferentTick_HashesDiffer()
    {
        var other = Base() with { Tick = 1 };
        Assert.NotEqual(StateHash.Compute(Base()), StateHash.Compute(other));
    }

    [Fact]
    public void DifferentContactPosition_HashesDiffer()
    {
        var a = Base().WithContacts(new[] { Contact.CreateInert(new ContactId(1), new Vec2(0.5, 0.0)) });
        var b = Base().WithContacts(new[] { Contact.CreateInert(new ContactId(1), new Vec2(0.6, 0.0)) });
        Assert.NotEqual(StateHash.Compute(a), StateHash.Compute(b));
    }

    [Fact]
    public void DifferenceBelowQuantum_HashesEqual()
    {
        // El hash debe detectar divergencia real, no ruido de último bit. Sin cuantizar,
        // el test de determinismo se vuelve inestable y se termina ignorando.
        var a = Base().WithContacts(new[] { Contact.CreateInert(new ContactId(1), new Vec2(0.5, 0.0)) });
        var b = Base().WithContacts(new[] { Contact.CreateInert(new ContactId(1), new Vec2(0.5 + 1e-12, 0.0)) });
        Assert.Equal(StateHash.Compute(a), StateHash.Compute(b));
    }

    [Fact]
    public void DifferentAmmo_HashesDiffer()
    {
        var other = Base();
        other = other with { Sub = other.Sub with { AmmoLoaded = 1 } };
        Assert.NotEqual(StateHash.Compute(Base()), StateHash.Compute(other));
    }

    [Fact]
    public void DifferentDefensePosition_HashesDiffer()
    {
        var a = Base().WithDefenses(new[] { Defense.Create(new DefenseId(1), new Vec2(0.2, 0.2)) });
        var b = Base().WithDefenses(new[] { Defense.Create(new DefenseId(1), new Vec2(0.2, 0.3)) });
        Assert.NotEqual(StateHash.Compute(a), StateHash.Compute(b));
    }
}
