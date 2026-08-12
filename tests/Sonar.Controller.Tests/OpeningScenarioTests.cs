using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sonar.Core;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Controller.Tests;

/// <summary>
/// Recorre el escenario de apertura exactamente como lo hace Main.cs: la misma
/// configuración de disco, los mismos contactos, los mismos comandos escritos como texto.
/// Es lo más cerca de "el juego funciona" que se puede correr sin abrir Godot.
///
/// Si estos tests fallan, el prototipo no arranca — sin importar qué diga el editor.
/// </summary>
public class OpeningScenarioTests
{
    private const int Seed = 20260811;

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Sonar.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static SimulationConfig WorkingConfig()
    {
        var path = Path.Combine(RepoRoot(), "config", "simulation.working.json");
        Assert.True(File.Exists(path), $"No se encontró {path}");
        return SimulationConfigLoader.LoadFromJson(File.ReadAllText(path));
    }

    /// El escenario real, el mismo que arma Main.cs. No una copia.
    private static GameController Opening(out SimulationConfig config)
    {
        config = WorkingConfig();
        var controller = new GameController(
            config, Seed, OpeningScenario.Course, OpeningScenario.SubSpeed);
        controller.SetContacts(OpeningScenario.Contacts(config));
        return controller;
    }

    /// Avanza `seconds` en pasos de un tick, que es lo que hace el juego frame a frame.
    private static List<GameEvent> Run(GameController controller, SimulationConfig config, double seconds)
    {
        var events = new List<GameEvent>();
        var ticks = (int)(seconds / config.TickDurationSeconds);
        for (var i = 0; i < ticks; i++)
            events.AddRange(controller.Advance(config.TickDurationSeconds));
        return events;
    }

    [Fact]
    public void AfterOneSweep_EveryContactHasBeenRead()
    {
        var controller = Opening(out var config);
        Run(controller, config, config.SweepPeriodSeconds + 0.5);

        // Cuatro enemigos más el pecio de referencia.
        Assert.Equal(5, controller.Snapshot.Contacts.Count);
        Assert.All(controller.Snapshot.Contacts, c => Assert.NotEqual('-', c.BandLetter));
    }

    [Fact]
    public void ReadingsAge_BetweenSweeps()
    {
        // La propiedad central: entre pasadas del haz la etiqueta envejece y no se mueve.
        var controller = Opening(out var config);
        Run(controller, config, config.SweepPeriodSeconds + 0.5);

        var before = controller.Snapshot.Contacts.OrderBy(c => c.Id).ToArray();
        Run(controller, config, config.SweepPeriodSeconds / 4.0);
        var after = controller.Snapshot.Contacts.OrderBy(c => c.Id).ToArray();

        var stale = before.Zip(after).Where(p => p.First.Position == p.Second.Position).ToArray();
        Assert.NotEmpty(stale);
        Assert.All(stale, p => Assert.True(p.Second.AgeTicks > p.First.AgeTicks,
            $"El contacto {p.First.Id} no envejeció: {p.First.AgeTicks} -> {p.Second.AgeTicks}"));
    }

    [Fact]
    public void TypedCoordinate_FiresATorpedo()
    {
        var controller = Opening(out var config);
        controller.SubmitInput("C047");
        var events = Run(controller, config, 0.1);

        var fired = (TorpedoFired)Assert.Single(events, e => e is TorpedoFired);
        Assert.Equal(47, fired.BearingDegrees);
        Assert.Equal('C', fired.BandLetter);
        Assert.Equal(3, controller.Snapshot.AmmoLoaded);
        Assert.Single(controller.Snapshot.Torpedoes);
    }

    [Fact]
    public void Typo_IsLoggedAndDoesNothing()
    {
        var controller = Opening(out var config);
        controller.SubmitInput("C400");
        var events = Run(controller, config, 0.1);

        Assert.Contains(events, e => e is InputNotRecognized);
        Assert.DoesNotContain(events, e => e is TorpedoFired);
        Assert.Equal(config.MagazineSize, controller.Snapshot.AmmoLoaded);
    }

    [Fact]
    public void StoppingTheSweep_FreezesTheLabels()
    {
        var controller = Opening(out var config);
        Run(controller, config, config.SweepPeriodSeconds + 0.5);

        controller.SubmitInput("SONAR");
        Run(controller, config, 0.1);
        Assert.False(controller.Snapshot.SweepRunning);

        var frozen = controller.Snapshot.Contacts.Select(c => c.Position).ToArray();
        Assert.NotEmpty(frozen);

        Run(controller, config, 10.0);

        // Diez segundos con el barrido detenido: las etiquetas no se movieron ni una.
        Assert.Equal(frozen, controller.Snapshot.Contacts.Select(c => c.Position).ToArray());

        // Control positivo. Sin esto el test pasaría por vacuidad: comparar dos veces la
        // misma lista congelada es trivialmente cierto si el barrido nunca lee nada. Con
        // el haz andando, esos mismos diez segundos SÍ mueven las etiquetas.
        var running = Opening(out var runningConfig);
        Run(running, runningConfig, runningConfig.SweepPeriodSeconds + 0.5);

        var beforeRunning = running.Snapshot.Contacts.Select(c => c.Position).ToArray();
        Run(running, runningConfig, 10.0);

        Assert.NotEqual(beforeRunning, running.Snapshot.Contacts.Select(c => c.Position).ToArray());
    }

    /// <summary>
    /// Ventana de observación tranquila que el escenario promete antes del primer disparo
    /// enemigo. Varios barridos completos, con margen.
    /// </summary>
    private const double QuietSeconds = 18.0;

    [Fact]
    public void ThePlayerGetsTimeToReadBeforeTheFirstShotLands()
    {
        // Sin margen de gracia el jugador muere a los diez segundos sin haber tocado nada,
        // y M1 no puede medir si extrapolar es satisfactorio porque nunca se extrapola.
        // Pasó de verdad: el submarino se destruía en el tick 582.
        var controller = Opening(out var config);
        var events = Run(controller, config, QuietSeconds);

        Assert.DoesNotContain(events, e => e is SubHit);
        Assert.False(controller.Snapshot.Destroyed);

        // La simulación tiene que haber avanzado de verdad. Sin esto el test es vacuo:
        // al morir, Tick deja de avanzar y TODO queda congelado — incluidas las
        // afirmaciones de más abajo, que pasarían para siempre.
        var expected = (int)(QuietSeconds / config.TickDurationSeconds);
        Assert.True(controller.Snapshot.Tick >= expected - 1,
            $"La simulación se congeló en el tick {controller.Snapshot.Tick} de {expected}.");

        // Y en esos segundos hubo varias vueltas del haz: hay material para extrapolar.
        Assert.Equal(5, controller.Snapshot.Contacts.Count);
    }

    [Fact]
    public void ContactsDoNotPileOntoTheSub()
    {
        // La deriva del mundo se suma a la velocidad de todos los contactos. Con
        // velocidades propias mal elegidas, todo lo que empieza al norte cae encima del
        // submarino en segundos. Pasó con los valores del plan: un enemigo terminaba
        // dentro de la zona muerta, mostrando la banda '-'.
        var controller = Opening(out var config);
        Run(controller, config, QuietSeconds);

        var enemies = controller.Snapshot.Contacts.Where(c => c.Id < 200).ToArray();
        Assert.Equal(4, enemies.Length);

        Assert.All(enemies, c => Assert.True(
            c.Position.Length > config.BandOuterRadii[0],
            $"El contacto {c.Id} se pegó al submarino: radio {c.Position.Length:F3}."));
    }

    [Fact]
    public void TheWreckVisiblyDrifts()
    {
        // Sin una referencia que derive de verdad, la anomalía del §25 no se puede leer:
        // el jugador no tiene contra qué comparar la defensa que se queda con él.
        var controller = Opening(out var config);
        Run(controller, config, config.SweepPeriodSeconds + 0.5);

        var first = controller.Snapshot.Contacts.Single(c => c.Id == 200).Position;
        Run(controller, config, 14.0);
        var later = controller.Snapshot.Contacts.Single(c => c.Id == 200).Position;

        // Deriva pura hacia el sur: el pecio no tiene velocidad propia.
        Assert.True(later.Y - first.Y > 0.25,
            $"El pecio apenas se movió: {first.Y:F3} -> {later.Y:F3}.");
    }

    [Fact]
    public void TheEnemyEventuallyShoots()
    {
        // La otra mitad del margen de gracia: tiene que ser una demora, no una amnistía.
        // Junto con ThePlayerGetsTimeToReadBeforeTheFirstShotLands deja la ventana
        // tranquila acotada por ambos lados.
        var controller = Opening(out var config);
        var events = Run(controller, config, 26.0);

        var fired = events.OfType<EnemyTorpedoFired>().ToArray();
        Assert.NotEmpty(fired);

        var quietTicks = (int)(QuietSeconds / config.TickDurationSeconds);
        Assert.True(fired[0].Tick > quietTicks,
            $"El primer disparo enemigo llegó en el tick {fired[0].Tick}, dentro de la ventana tranquila.");
    }

    [Fact]
    public void ReloadDoesNotStopTheSweep()
    {
        var controller = Opening(out var config);
        controller.SubmitInput("RELOAD");
        Run(controller, config, 0.1);

        var before = controller.Snapshot.SweepDegrees;
        Run(controller, config, 0.5);

        Assert.True(controller.Snapshot.ReloadInProgress);
        Assert.NotEqual(before, controller.Snapshot.SweepDegrees);
    }
}
