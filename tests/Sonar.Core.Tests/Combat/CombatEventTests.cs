using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Core.Tests.Combat;

public class CombatEventTests
{
    [Fact]
    public void ContactDestroyed_CarriesCauseAndPosition()
    {
        var evt = new ContactDestroyed(10, 3, new Vec2(0.2, -0.4), DestructionCause.DirectHit);
        Assert.Equal(DestructionCause.DirectHit, evt.Cause);
        Assert.Equal(3, evt.ContactId);
        Assert.Equal(10, evt.Tick);
    }

    [Fact]
    public void FireRejected_CarriesReason()
    {
        Assert.Equal(RejectionReason.NoAmmo, new FireRejected(5, RejectionReason.NoAmmo).Reason);
    }

    [Fact]
    public void NearMiss_CarriesDistance()
    {
        var evt = new NearMiss(12, 7, 0.13);
        Assert.Equal(0.13, evt.Distance, precision: 10);
    }

    [Fact]
    public void SubDestroyed_IsDistinctFromSubHit()
    {
        GameEvent hit = new SubHit(20, 1);
        GameEvent dead = new SubDestroyed(30, SubDeathCause.Torpedoes);
        Assert.IsType<SubHit>(hit);
        Assert.IsType<SubDestroyed>(dead);
    }
}
