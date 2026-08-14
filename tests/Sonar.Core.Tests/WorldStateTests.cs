using System.Linq;
using Sonar.Core;
using Sonar.Core.Entities;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Core.Tests;

public class WorldStateTests
{
    private static SimulationConfig Config() => new()
    {
        TickDurationSeconds = 1.0 / 60.0,
        MaxCatchUpTicks = 5,
        DeadZoneRadius = 0.10,
        BandOuterRadii = new[] { 0.28, 0.46, 0.64, 0.82, 1.00 },
        SweepPeriodSeconds = 3.5,
        TorpedoSpeed = 0.70,
        HitRadius = 0.040,
        BlastRadius = 0.100,
        NearMissRadius = 0.160,
        EnemyTorpedoSpeed = 0.45,
        EnemyFireIntervalTicks = 420,
        EnemyFireStaggerTicks = 70,
        SubHitRadius = 0.045,
        DefenseRadius = 0.070,
        DefenseCapacity = 3,
        MagazineSize = 4,
        AmmoReserve = 12,
        ReloadTicks = 300,
        EnemyReloadTicks = 600,
    };

    [Fact]
    public void CreateInitial_StartsAtTickZeroWithFullMagazine()
    {
        var state = WorldState.CreateInitial(Config(), new Bearing(0), speed: 0.05);

        Assert.Equal(0, state.Tick);
        Assert.Equal(4, state.Sub.AmmoLoaded);
        Assert.Equal(12, state.Sub.AmmoReserve);
        Assert.False(state.Sub.Destroyed);
        Assert.Empty(state.Contacts);
        Assert.Empty(state.Torpedoes);
        Assert.Empty(state.Defenses);
    }

    [Fact]
    public void NextEntityId_StartsAtOne()
    {
        Assert.Equal(1, WorldState.CreateInitial(Config(), new Bearing(0), 0.05).NextEntityId);
    }

    [Fact]
    public void TakeEntityId_ReturnsIncrementingIdsAndAdvancesState()
    {
        var state = WorldState.CreateInitial(Config(), new Bearing(0), 0.05);

        var (first, afterFirst) = state.TakeEntityId();
        var (second, afterSecond) = afterFirst.TakeEntityId();

        Assert.Equal(1, first);
        Assert.Equal(2, second);
        Assert.Equal(3, afterSecond.NextEntityId);
        // El estado original no cambió.
        Assert.Equal(1, state.NextEntityId);
    }

    [Fact]
    public void WithContacts_KeepsThemSortedById()
    {
        var state = WorldState.CreateInitial(Config(), new Bearing(0), 0.05);
        var contacts = new[]
        {
            Contact.CreateInert(new ContactId(5), new Vec2(0.5, 0)),
            Contact.CreateInert(new ContactId(2), new Vec2(0.4, 0)),
            Contact.CreateInert(new ContactId(8), new Vec2(0.3, 0)),
        };

        var updated = state.WithContacts(contacts);
        var ids = updated.Contacts.Select(c => c.Id.Value).ToArray();

        Assert.Equal(new[] { 2, 5, 8 }, ids);
    }
}
