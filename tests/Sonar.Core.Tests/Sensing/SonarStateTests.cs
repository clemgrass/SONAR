using System.Linq;
using Sonar.Core.Entities;
using Sonar.Core.Sensing;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Core.Tests.Sensing;

public class SonarStateTests
{
    [Fact]
    public void Initial_StartsAtZeroAndRunning()
    {
        var state = SonarState.CreateInitial();
        Assert.Equal(0.0, state.SweepDegrees, precision: 10);
        Assert.True(state.SweepRunning);
        Assert.Empty(state.Readings);
    }

    [Fact]
    public void Reading_RecordsPositionAndTick()
    {
        var reading = new Reading
        {
            ContactId = new ContactId(3),
            Position = new Vec2(0.2, -0.4),
            ObservedAtTick = 120,
        };

        Assert.Equal(120, reading.ObservedAtTick);
        Assert.Equal(new ContactId(3), reading.ContactId);
    }

    [Fact]
    public void Readings_AreHeldInContactIdOrder()
    {
        var state = SonarState.CreateInitial() with
        {
            Readings = new[]
            {
                new Reading { ContactId = new ContactId(1), Position = Vec2.Zero, ObservedAtTick = 0 },
                new Reading { ContactId = new ContactId(4), Position = Vec2.Zero, ObservedAtTick = 0 },
                new Reading { ContactId = new ContactId(9), Position = Vec2.Zero, ObservedAtTick = 0 },
            },
        };

        var ids = state.Readings.Select(r => r.ContactId.Value).ToArray();
        Assert.Equal(ids.OrderBy(v => v).ToArray(), ids);
    }
}
