using Sonar.Autopilot.Tests.Support;
using Sonar.Controller.Presentation;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Autopilot.Tests;

public class SaturationMeterTests
{
    private static SaturationMeter Meter() => new(TestConfig.Build());

    private static EnemyTorpedoView[] Torpedoes(params int[] ids)
    {
        var views = new EnemyTorpedoView[ids.Length];
        for (var i = 0; i < ids.Length; i++) views[i] = new EnemyTorpedoView(ids[i], new Vec2(0.5, 0));
        return views;
    }

    [Fact]
    public void WithNoThreats_ThereIsNoPressure()
    {
        var meter = Meter();
        meter.NoteCommand(10);
        meter.Observe(TestSnapshot.Build(100));

        Assert.Equal(0.0, meter.IndexAt(100));
    }

    [Fact]
    public void MoreThreatsThanCommands_PushTheIndexAboveOne()
    {
        var meter = Meter();
        meter.Observe(TestSnapshot.Build(100, enemyTorpedoes: Torpedoes(1)));
        meter.Observe(TestSnapshot.Build(200, enemyTorpedoes: Torpedoes(1, 2)));
        meter.Observe(TestSnapshot.Build(300, enemyTorpedoes: Torpedoes(1, 2, 3)));
        meter.NoteCommand(150);
        meter.NoteCommand(250);

        Assert.Equal(1.5, meter.IndexAt(300), 10);
    }

    [Fact]
    public void MoreCommandsThanThreats_KeepItBelowOne()
    {
        var meter = Meter();
        meter.Observe(TestSnapshot.Build(100, enemyTorpedoes: Torpedoes(1)));
        meter.NoteCommand(110);
        meter.NoteCommand(120);
        meter.NoteCommand(130);
        meter.NoteCommand(140);

        Assert.Equal(0.25, meter.IndexAt(200), 10);
    }

    [Fact]
    public void TheSameTorpedoSeenEveryTick_IsOneThreat()
    {
        // El torpedo sigue en el agua durante cientos de ticks. Si cada tick contara como
        // una amenaza nueva, el índice mediría cuánto dura un torpedo y no cuántos llegan.
        var meter = Meter();
        for (var tick = 100; tick < 400; tick++)
            meter.Observe(TestSnapshot.Build(tick, enemyTorpedoes: Torpedoes(1)));
        meter.NoteCommand(150);

        Assert.Equal(1.0, meter.IndexAt(400), 10);
    }

    [Fact]
    public void ThreatsWithNoAnswer_DoNotDivideByZero()
    {
        // Tres amenazas y ni un comando es el caso "me están matando y no alcanzo a teclear".
        // Se reporta como un número grande y finito, no como infinito: un infinito rompe
        // las comparaciones del test de la pared y no dice nada más que un tres.
        var meter = Meter();
        meter.Observe(TestSnapshot.Build(100, enemyTorpedoes: Torpedoes(1, 2, 3)));

        Assert.Equal(3.0, meter.IndexAt(100), 10);
    }

    [Fact]
    public void WhatHappenedBeforeTheWindow_DoesNotCount()
    {
        // La ventana son 1800 ticks. Una amenaza del tick 0 mirada desde el 2000 ya salió.
        var stale = Meter();
        stale.Observe(TestSnapshot.Build(0, enemyTorpedoes: Torpedoes(1)));
        stale.Observe(TestSnapshot.Build(2000));

        Assert.Equal(0.0, stale.IndexAt(2000));

        // Control positivo: la misma amenaza dentro de la ventana sí cuenta.
        var fresh = Meter();
        fresh.Observe(TestSnapshot.Build(500, enemyTorpedoes: Torpedoes(1)));
        fresh.Observe(TestSnapshot.Build(2000));

        Assert.Equal(1.0, fresh.IndexAt(2000), 10);
    }
}
