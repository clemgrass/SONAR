using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sonar.Core;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Controller.Tests;

/// <summary>
/// Recorre el gauntlet con la configuración real de disco, igual que lo hace el juego. Es lo
/// más cerca de "el juego funciona" que se puede correr sin abrir Godot.
/// </summary>
public class GauntletTests
{
    private const int BaseSeed = 20260813;
    private const double SubSpeed = 0.012;

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

    /// <summary>Controller con la configuración real y ya arrancado con START.</summary>
    private static GameController Start(out SimulationConfig config)
    {
        config = WorkingConfig();
        var controller = new GameController(config, BaseSeed, new Bearing(0), SubSpeed);
        controller.SubmitInput("START");
        return controller;
    }

    private static List<GameEvent> Run(GameController controller, SimulationConfig config, double seconds)
    {
        var events = new List<GameEvent>();
        var ticks = (int)(seconds / config.TickDurationSeconds);
        for (var i = 0; i < ticks; i++) events.AddRange(controller.Advance(config.TickDurationSeconds));
        return events;
    }

    private static double QuietSeconds(SimulationConfig config) =>
        WaveDirector.OpeningQuietTicks * config.TickDurationSeconds;

    [Fact]
    public void TheOpeningIsSilent()
    {
        var controller = Start(out var config);
        Run(controller, config, QuietSeconds(config) - 1.0);

        Assert.Equal(0, controller.Wave);
        Assert.Equal(0, controller.LiveEnemyCount);
        Assert.False(controller.Snapshot.Destroyed);

        // Control positivo: el mundo sí está corriendo, sólo que no ha entrado nada. Sin
        // esto el test pasaría igual si START no hubiera hecho nada.
        Assert.True(controller.Snapshot.Tick > 0);
    }

    [Fact]
    public void TheFirstWaveIsASingleContact()
    {
        var controller = Start(out var config);
        Run(controller, config, QuietSeconds(config) + 1.0);

        Assert.Equal(1, controller.Wave);
        Assert.Equal(1, controller.LiveEnemyCount);
    }

    [Fact]
    public void TheFirstContactGetsReadWithinASweep()
    {
        var controller = Start(out var config);
        Run(controller, config, QuietSeconds(config) + config.SweepPeriodSeconds + 1.0);

        Assert.Single(controller.Snapshot.Contacts);
    }

    [Fact]
    public void ContactsDoNotPileOntoTheSub()
    {
        // La deriva del mundo se suma a la velocidad de todos los contactos. Con velocidades
        // mal elegidas, todo lo que entra cae encima del submarino en segundos. Con colisión
        // mortal eso ya no es un escenario aburrido: es morir sin haber jugado.
        var controller = Start(out var config);
        Run(controller, config, QuietSeconds(config) + 30.0);

        Assert.False(controller.Snapshot.Destroyed,
            "El submarino murió a los treinta segundos de la primera oleada.");
        Assert.Equal(1, controller.LiveEnemyCount);
    }

    [Fact]
    public void TypedCoordinate_FiresATorpedo()
    {
        var controller = Start(out var config);
        Run(controller, config, QuietSeconds(config) + 1.0);

        controller.SubmitInput("C047");
        var events = Run(controller, config, 0.1);

        Assert.Contains(events, e => e is TorpedoFired);
        Assert.Equal(config.MagazineSize - 1, controller.Snapshot.AmmoLoaded);
    }

    [Fact]
    public void Typo_IsLoggedAndDoesNothing()
    {
        var controller = Start(out var config);
        controller.SubmitInput("C400");
        var events = Run(controller, config, 0.1);

        Assert.Contains(events, e => e is InputNotRecognized);
        Assert.DoesNotContain(events, e => e is TorpedoFired);
        Assert.Equal(config.MagazineSize, controller.Snapshot.AmmoLoaded);
    }

    [Fact]
    public void StoppingTheSweep_FreezesTheLabels()
    {
        var controller = Start(out var config);
        Run(controller, config, QuietSeconds(config) + config.SweepPeriodSeconds + 1.0);

        controller.SubmitInput("SONAR");
        Run(controller, config, 0.1);
        Assert.False(controller.Snapshot.SweepRunning);

        var frozen = controller.Snapshot.Contacts.Select(c => c.Position).ToArray();
        Assert.NotEmpty(frozen);

        Run(controller, config, 10.0);
        Assert.Equal(frozen, controller.Snapshot.Contacts.Select(c => c.Position).ToArray());

        // Control positivo: con el haz andando esos mismos diez segundos SÍ mueven las
        // etiquetas. Sin esto el test pasaría por vacuidad.
        var running = Start(out var runningConfig);
        Run(running, runningConfig, QuietSeconds(runningConfig) + runningConfig.SweepPeriodSeconds + 1.0);
        var before = running.Snapshot.Contacts.Select(c => c.Position).ToArray();
        Run(running, runningConfig, 10.0);

        Assert.NotEqual(before, running.Snapshot.Contacts.Select(c => c.Position).ToArray());
    }

    [Fact]
    public void ReloadDoesNotStopTheSweep()
    {
        var controller = Start(out var config);
        Run(controller, config, QuietSeconds(config) + 1.0);

        // Dispara primero: recargar con el cargador lleno no prueba gran cosa.
        controller.SubmitInput("C047");
        Run(controller, config, 0.1);

        controller.SubmitInput("RELOAD");
        Run(controller, config, 0.1);

        var before = controller.Snapshot.SweepDegrees;
        Run(controller, config, 0.5);

        Assert.True(controller.Snapshot.ReloadInProgress);
        Assert.NotEqual(before, controller.Snapshot.SweepDegrees);
    }

    [Fact]
    public void NothingRunsUntilStart()
    {
        // La demo tiene que poder abrirse sin nadie al lado que la explique: el instructivo
        // se lee con el reloj detenido.
        var config = WorkingConfig();
        var controller = new GameController(config, BaseSeed, new Bearing(0), SubSpeed);

        Run(controller, config, QuietSeconds(config) + 30.0);

        Assert.Equal(0, controller.Snapshot.Tick);
        Assert.Equal(0, controller.Wave);
        Assert.False(controller.Started);
    }
}
