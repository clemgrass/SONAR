using System;
using System.Collections.Generic;
using System.Linq;
using Sonar.Core;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Sensing;

public class GatingTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Contact EnemyAt(int id, Vec2 position, Vec2 velocity) => new()
    {
        Id = new ContactId(id),
        Kind = ContactKind.Enemy,
        Position = position,
        Velocity = velocity,
        Alive = true,
        NextFireTick = int.MaxValue,
    };

    private static int TicksPerSweep(SimulationConfig config) =>
        (int)Math.Round(config.SweepPeriodSeconds / config.TickDurationSeconds);

    /// <summary>
    /// Corre hasta que aparezca la primera lectura. Existe porque el haz arranca en
    /// marcación 0 y gira en sentido horario: un contacto en marcación 0 es el ÚLTIMO en
    /// ser barrido, no el primero. Contar ticks a mano para adivinar cuándo pasa el haz
    /// es frágil, sobre todo si el contacto se mueve en marcación mientras el haz gira.
    /// </summary>
    private static Sonar.Core.Sensing.Reading RunUntilFirstReading(Simulation sim, int maxTicks)
    {
        for (var i = 0; i < maxTicks; i++)
        {
            sim.Tick(NoCommands);
            if (sim.State.Sonar.Readings.Count > 0) return sim.State.Sonar.Readings[0];
        }

        throw new InvalidOperationException($"No apareció ninguna lectura en {maxTicks} ticks.");
    }

    [Fact]
    public void BearingZero_IsTheLastBearingSwept_NotTheFirst()
    {
        // Pincha la orientación de arranque del haz. Es contraintuitivo y ya indujo a
        // error una vez: el haz nace en 0, así que un contacto justo ahí espera una
        // vuelta entera. Uno unos grados más adelante se lee casi de inmediato.
        var config = TestWorld.Config();

        var atZero = TestWorld.Still(config);
        atZero.SetContacts(new[] { EnemyAt(1, new Vec2(0.0, -0.60), Vec2.Zero) });
        for (var i = 0; i < 10; i++) atZero.Tick(NoCommands);
        Assert.Empty(atZero.State.Sonar.Readings);

        var justAhead = TestWorld.Still(config);
        // Marcación ~10: unos pocos grados en el sentido de giro.
        justAhead.SetContacts(new[] { EnemyAt(1, new Vec2(0.104, -0.591), Vec2.Zero) });
        for (var i = 0; i < 10; i++) justAhead.Tick(NoCommands);
        Assert.Single(justAhead.State.Sonar.Readings);
    }

    [Fact]
    public void NoReading_BeforeTheBeamPasses()
    {
        var sim = TestWorld.Still();
        // Contacto al oeste (marcación 270). El haz arranca en 0 y gira en sentido horario.
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(-0.60, 0.0), Vec2.Zero) });

        for (var i = 0; i < 10; i++) sim.Tick(NoCommands);

        Assert.Empty(sim.State.Sonar.Readings);
    }

    [Fact]
    public void ReadingAppears_AfterAFullSweep()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(-0.60, 0.0), Vec2.Zero) });

        for (var i = 0; i < TicksPerSweep(config) + 2; i++) sim.Tick(NoCommands);

        var reading = Assert.Single(sim.State.Sonar.Readings);
        Assert.Equal(1, reading.ContactId.Value);
    }

    [Fact]
    public void ReadingDoesNotFollowTheContact_BetweenSweeps()
    {
        // Esta es la propiedad central del juego: la etiqueta que ves es del último
        // barrido, no de dónde está el contacto ahora.
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        // Marcación 90 (este), derivando al sur: su marcación crece mucho más lento que
        // el haz, así que una vez barrido el haz no lo vuelve a alcanzar en esta ventana.
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.60, 0.0), new Vec2(0.0, 0.15)) });

        var justRead = RunUntilFirstReading(sim, TicksPerSweep(config)).Position;

        // Avanza un cuarto de barrido sin que el haz vuelva a pasar.
        for (var i = 0; i < TicksPerSweep(config) / 4; i++) sim.Tick(NoCommands);

        var stillTheSame = sim.State.Sonar.Readings.Single().Position;
        Assert.Equal(justRead, stillTheSame);

        // Pero el contacto sí se movió.
        Assert.NotEqual(justRead.Y, sim.State.Contacts.Single().Position.Y, precision: 3);
    }

    [Fact]
    public void ReadingRefreshes_OnTheNextPass()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.60, 0.0), new Vec2(0.0, 0.05)) });

        var firstTick = RunUntilFirstReading(sim, TicksPerSweep(config)).ObservedAtTick;

        // Dos vueltas, no una: el contacto también avanza en marcación mientras el haz
        // gira, así que el haz necesita algo más de una vuelta para volver a alcanzarlo.
        for (var i = 0; i < TicksPerSweep(config) * 2; i++) sim.Tick(NoCommands);
        var secondTick = sim.State.Sonar.Readings.Single().ObservedAtTick;

        Assert.True(secondTick > firstTick,
            $"La lectura no se refrescó: sigue siendo la del tick {firstTick}.");
    }

    [Fact]
    public void StoppedSweep_RecordsNothingNew()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.Tick(new Command[] { new ToggleSonarCommand() });
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(-0.60, 0.0), Vec2.Zero) });

        for (var i = 0; i < TicksPerSweep(config) * 2; i++) sim.Tick(NoCommands);

        Assert.Empty(sim.State.Sonar.Readings);
    }

    [Fact]
    public void DestroyedContact_LosesItsReadingImmediately()
    {
        // La confirmación de destrucción es ley del mundo y no depende del barrido
        // (spec §4.6). Dejar la lectura sería mentirle al jugador.
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[] { EnemyAt(1, new Vec2(0.55, 0.0), Vec2.Zero) });

        RunUntilFirstReading(sim, TicksPerSweep(config));
        Assert.Single(sim.State.Sonar.Readings);

        sim.Tick(new Command[] { new FireCommand(new TargetCoordinate(new Bearing(90), new Band(2))) });
        for (var i = 0; i < 120; i++) sim.Tick(NoCommands);

        Assert.Empty(sim.State.Contacts);
        Assert.Empty(sim.State.Sonar.Readings);
    }

    [Fact]
    public void Readings_StaySortedByContactId()
    {
        var config = TestWorld.Config();
        var sim = TestWorld.Still(config);
        sim.SetContacts(new[]
        {
            EnemyAt(9, new Vec2(0.0, -0.5), Vec2.Zero),
            EnemyAt(2, new Vec2(0.5, 0.0), Vec2.Zero),
            EnemyAt(5, new Vec2(0.0, 0.5), Vec2.Zero),
        });

        for (var i = 0; i < TicksPerSweep(config) + 5; i++) sim.Tick(NoCommands);

        var ids = sim.State.Sonar.Readings.Select(r => r.ContactId.Value).ToArray();
        Assert.Equal(3, ids.Length);
        Assert.Equal(ids.OrderBy(v => v).ToArray(), ids);
    }
}
