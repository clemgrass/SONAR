using System.Linq;
using Sonar.Core;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Controller.Tests;

public class GameControllerTests
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
    };

    private static GameController New() => new(Config(), seed: 1, course: new Bearing(0), speed: 0.05);

    [Fact]
    public void Advance_RunsOneTickPerTickDuration()
    {
        var controller = New();
        controller.Advance(1.0 / 60.0);
        Assert.Equal(1, controller.Snapshot.Tick);
    }

    [Fact]
    public void Advance_AccumulatesPartialTime()
    {
        var controller = New();
        controller.Advance(1.0 / 120.0);
        Assert.Equal(0, controller.Snapshot.Tick);

        controller.Advance(1.0 / 120.0);
        Assert.Equal(1, controller.Snapshot.Tick);
    }

    [Fact]
    public void Advance_RunsMultipleTicksForALargeDelta()
    {
        var controller = New();
        controller.Advance(3.0 / 60.0);
        Assert.Equal(3, controller.Snapshot.Tick);
    }

    [Fact]
    public void Advance_CapsCatchUpAndDiscardsTheRest()
    {
        // Sin tope, un congelamiento de la ventana produce una avalancha de ticks que el
        // jugador experimenta como un salto. Es la espiral de la muerte clásica (TDD §6.3).
        var controller = New();
        controller.Advance(10.0);   // diez segundos de golpe

        Assert.Equal(5, controller.Snapshot.Tick);   // MaxCatchUpTicks
    }

    [Fact]
    public void Advance_DoesNotCarryDiscardedTimeIntoTheNextCall()
    {
        var controller = New();
        controller.Advance(10.0);
        controller.Advance(1.0 / 60.0);

        Assert.Equal(6, controller.Snapshot.Tick);
    }

    [Fact]
    public void SubmitInput_QueuesForTheNextTickBoundary()
    {
        var controller = New();
        controller.SubmitInput("STATUS");

        // Todavía no se ejecutó nada.
        Assert.Equal(0, controller.Snapshot.Tick);

        var events = controller.Advance(1.0 / 60.0);
        Assert.Contains(events, e => e is StatusReported);
    }

    [Fact]
    public void SubmitInput_SurvivesAnAdvanceTooShortToTick()
    {
        // Escribir entre dos fronteras de tick no puede perder el comando: el jugador
        // teclea a destiempo del reloj de simulación por definición.
        var controller = New();
        controller.SubmitInput("STATUS");

        Assert.Empty(controller.Advance(1.0 / 120.0));

        var events = controller.Advance(1.0 / 120.0);
        Assert.Contains(events, e => e is StatusReported);
    }

    [Fact]
    public void SubmitInput_UnknownText_ProducesNotRecognizedEvent()
    {
        var controller = New();
        controller.SubmitInput("what is this");
        var events = controller.Advance(1.0 / 60.0);

        var evt = (InputNotRecognized)Assert.Single(events, e => e is InputNotRecognized);
        Assert.Equal("what is this", evt.RawText);
    }

    [Fact]
    public void SubmitInput_EmptyText_QueuesNothing()
    {
        var controller = New();
        controller.SubmitInput("   ");
        var events = controller.Advance(1.0 / 60.0);

        Assert.Empty(events);
    }

    [Fact]
    public void MultipleInputs_AreAppliedInSubmissionOrder()
    {
        var controller = New();
        controller.SubmitInput("C047");
        controller.SubmitInput("C048");
        controller.Advance(1.0 / 60.0);

        var fired = controller.LastEvents.OfType<TorpedoFired>().ToArray();
        Assert.Equal(2, fired.Length);
        Assert.Equal(47, fired[0].BearingDegrees);
        Assert.Equal(48, fired[1].BearingDegrees);
    }

    [Fact]
    public void QueuedCommands_AreNotReappliedOnLaterTicks()
    {
        // Un solo STATUS debe producir un solo StatusReported, aunque el mismo Advance
        // ejecute varios ticks. Si la cola no se vacía, se repite en cada uno.
        var controller = New();
        controller.SubmitInput("STATUS");
        var events = controller.Advance(3.0 / 60.0);

        Assert.Single(events, e => e is StatusReported);
    }

    [Fact]
    public void Snapshot_IsRebuiltEachAdvance()
    {
        var controller = New();
        var first = controller.Snapshot;
        controller.Advance(1.0 / 60.0);
        Assert.NotSame(first, controller.Snapshot);
    }
}
