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
        EnemyReloadTicks = 600,
    };

    /// <summary>
    /// Controller ya arrancado. Es lo que quieren casi todos los tests: sin START el mundo
    /// está detenido a propósito y ningún Advance corre un solo tick.
    /// </summary>
    private static GameController New()
    {
        var controller = NewBeforeStart();
        controller.SubmitInput("START");
        return controller;
    }

    /// <summary>Antes de START, para los tests que prueban justamente esa compuerta.</summary>
    private static GameController NewBeforeStart() =>
        new(Config(), baseSeed: 1, course: new Bearing(0), speed: 0.012);

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

    [Fact]
    public void Waves_ArriveThroughTheController()
    {
        var config = Config();
        var controller = New();

        // Antes del silencio inicial no hay nada.
        for (var i = 0; i < WaveDirector.OpeningQuietTicks - 1; i++)
            controller.Advance(config.TickDurationSeconds);
        Assert.Equal(0, controller.Wave);
        Assert.Equal(0, controller.LiveEnemyCount);

        for (var i = 0; i < 10; i++) controller.Advance(config.TickDurationSeconds);

        Assert.Equal(1, controller.Wave);
        Assert.Equal(1, controller.LiveEnemyCount);
    }

    [Fact]
    public void NothingRunsBeforeStart()
    {
        // La demo tiene que poder explicarse sola. Sin un momento con el reloj detenido, el
        // jugador nuevo lee las instrucciones mientras lo matan.
        var config = Config();
        var controller = NewBeforeStart();

        for (var i = 0; i < 3000; i++)
        {
            Assert.Empty(controller.Advance(config.TickDurationSeconds));
        }

        Assert.False(controller.Started);
        Assert.Equal(0, controller.Snapshot.Tick);
        Assert.Equal(0, controller.Wave);
    }

    [Fact]
    public void Start_SetsTheWorldRunning()
    {
        var config = Config();
        var controller = NewBeforeStart();

        Assert.True(controller.SubmitInput("START"));
        Assert.True(controller.Started);

        for (var i = 0; i < 60; i++) controller.Advance(config.TickDurationSeconds);
        Assert.Equal(60, controller.Snapshot.Tick);
    }

    [Fact]
    public void Retry_DoesNotSendYouBackToTheBriefing()
    {
        // Volver a leer el instructivo en cada muerte es tedio, no enseñanza.
        var config = Config();
        var controller = New();
        for (var i = 0; i < 60; i++) controller.Advance(config.TickDurationSeconds);

        controller.ForceDestroyForTests();
        controller.SubmitInput("RETRY");

        Assert.True(controller.Started);
        Assert.Equal(0, controller.Snapshot.Tick);

        controller.Advance(config.TickDurationSeconds);
        Assert.Equal(1, controller.Snapshot.Tick);
    }

    [Fact]
    public void SubmitInput_ReportsWhetherItUnderstood()
    {
        // La telemetría necesita saberlo y la escena no puede deducirlo: el parser vive
        // dentro del controller.
        var controller = New();

        Assert.True(controller.SubmitInput("C047"));
        Assert.False(controller.SubmitInput("hello"));
        Assert.False(controller.SubmitInput("   "));
    }

    [Fact]
    public void Retry_RestartsFromZeroWithADifferentSeed()
    {
        var config = Config();
        var controller = New();

        for (var i = 0; i < 600; i++) controller.Advance(config.TickDurationSeconds);
        var firstSeed = controller.Seed;
        Assert.True(controller.Snapshot.Tick > 0);

        controller.ForceDestroyForTests();
        controller.SubmitInput("RETRY");

        Assert.Equal(0, controller.Snapshot.Tick);
        Assert.False(controller.Snapshot.Destroyed);
        Assert.Equal(0, controller.Wave);
        Assert.Equal(2, controller.Attempt);
        Assert.NotEqual(firstSeed, controller.Seed);
    }

    [Fact]
    public void Retry_IsIgnoredWhileAlive()
    {
        var config = Config();
        var controller = New();
        for (var i = 0; i < 600; i++) controller.Advance(config.TickDurationSeconds);

        var tick = controller.Snapshot.Tick;
        controller.SubmitInput("RETRY");

        Assert.Equal(tick, controller.Snapshot.Tick);
        Assert.Equal(1, controller.Attempt);
    }

    [Fact]
    public void SameBaseSeed_ReproducesTheWholeSession()
    {
        // Semilla base más número de intento: la sesión completa es reproducible aunque los
        // spawns cambien entre intentos.
        static int[] SpawnBearings(GameController c, SimulationConfig cfg)
        {
            for (var i = 0; i < WaveDirector.OpeningQuietTicks + 10; i++)
                c.Advance(cfg.TickDurationSeconds);
            return c.SpawnedBearingsForTests();
        }

        var config = Config();
        var first = SpawnBearings(New(), config);

        // Sin esto el test es vacuo: dos listas vacías también son iguales.
        Assert.NotEmpty(first);
        Assert.Equal(first, SpawnBearings(New(), config));
    }
}
