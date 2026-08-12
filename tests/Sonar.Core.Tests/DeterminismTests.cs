using System.Collections.Generic;
using Sonar.Core;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests;

public class DeterminismTests
{
    private static long RunAndHash(int seed, int ticks)
    {
        var runner = new ScenarioRunner(
            TestWorld.Config(), seed,
            ScenarioScripts.MixedActivity(),
            ScenarioScripts.ReferenceContacts());
        return StateHash.Compute(runner.Run(ticks));
    }

    [Fact]
    public void SameSeed_ProducesIdenticalStateHash()
    {
        Assert.Equal(RunAndHash(12345, 3600), RunAndHash(12345, 3600));
    }

    [Fact]
    public void SameSeed_ProducesIdenticalEventStream()
    {
        var a = new ScenarioRunner(TestWorld.Config(), 12345,
            ScenarioScripts.MixedActivity(), ScenarioScripts.ReferenceContacts());
        var b = new ScenarioRunner(TestWorld.Config(), 12345,
            ScenarioScripts.MixedActivity(), ScenarioScripts.ReferenceContacts());

        a.Run(3600);
        b.Run(3600);

        // Sin esto el test sería vacuo: dos flujos vacíos también son iguales. Verifica
        // además que el guion se aplique en ticks absolutos y no se reinicie por llamada.
        Assert.NotEmpty(a.Events);

        Assert.Equal(a.Events.Count, b.Events.Count);
        for (var i = 0; i < a.Events.Count; i++) Assert.Equal(a.Events[i], b.Events[i]);
    }

    [Fact]
    public void ReRunningFromScratch_ReproducesEveryIntermediateTick()
    {
        // No basta con que el estado final coincida: la divergencia puede aparecer y
        // cancelarse. Comparamos la huella tick a tick.
        var a = new ScenarioRunner(TestWorld.Config(), 999,
            ScenarioScripts.MixedActivity(), ScenarioScripts.ReferenceContacts());
        var b = new ScenarioRunner(TestWorld.Config(), 999,
            ScenarioScripts.MixedActivity(), ScenarioScripts.ReferenceContacts());

        for (var tick = 0; tick < 600; tick++)
        {
            a.Run(1);
            b.Run(1);
            Assert.Equal(StateHash.Compute(a.State), StateHash.Compute(b.State));
        }

        // Este test avanza de tick en tick, así que es el único que detecta si el guion
        // se reinicia en cada llamada a Run. Sin comandos aplicados compararía 600 ticks
        // de pura deriva y no probaría nada del camino de comandos.
        Assert.NotEmpty(a.Events);
    }

    // --- Determinismo con combate encendido ---

    private static IReadOnlyDictionary<int, IReadOnlyList<Command>> CombatScript() =>
        new Dictionary<int, IReadOnlyList<Command>>
        {
            [5] = new Command[] { new FireCommand(new TargetCoordinate(new Bearing(0), new Band(3))) },
            [30] = new Command[] { new DeployDefenseCommand(new TargetCoordinate(new Bearing(90), new Band(2))) },
            [60] = new Command[] { new FireCommand(new TargetCoordinate(new Bearing(180), new Band(2))) },
            [120] = new Command[] { new ReloadCommand() },
            [300] = new Command[] { new FireCommand(new TargetCoordinate(new Bearing(270), new Band(4))) },
        };

    private static IEnumerable<Contact> CombatContacts()
    {
        var spawner = new ContactSpawner(TestWorld.Config());
        return spawner.SpawnEnemies(
            spawnTick: 0, firstId: 200,
            positions: new[] { new Vec2(0.0, -0.75), new Vec2(0.70, 0.20), new Vec2(-0.60, 0.40) },
            velocities: new[] { new Vec2(0.02, 0.05), new Vec2(-0.06, 0.01), new Vec2(0.04, -0.03) });
    }

    [Fact]
    public void CombatScenario_IsReproducibleTickByTick()
    {
        var a = new ScenarioRunner(TestWorld.Config(), 4242, CombatScript(), CombatContacts());
        var b = new ScenarioRunner(TestWorld.Config(), 4242, CombatScript(), CombatContacts());

        for (var tick = 0; tick < 1200; tick++)
        {
            a.Run(1);
            b.Run(1);
            Assert.Equal(StateHash.Compute(a.State), StateHash.Compute(b.State));
        }
    }

    [Fact]
    public void CombatScenario_ProducesIdenticalEventStreams()
    {
        var a = new ScenarioRunner(TestWorld.Config(), 4242, CombatScript(), CombatContacts());
        var b = new ScenarioRunner(TestWorld.Config(), 4242, CombatScript(), CombatContacts());

        a.Run(1200);
        b.Run(1200);

        Assert.Equal(a.Events.Count, b.Events.Count);
        for (var i = 0; i < a.Events.Count; i++) Assert.Equal(a.Events[i], b.Events[i]);
    }

    [Fact]
    public void CombatScenario_ActuallyProducesCombat()
    {
        // Un test de determinismo que corre sobre un escenario vacío pasa trivialmente.
        // Este verifica que el escenario de referencia ejercita el sistema.
        var runner = new ScenarioRunner(TestWorld.Config(), 4242, CombatScript(), CombatContacts());
        runner.Run(1200);

        Assert.Contains(runner.Events, e => e is Events.TorpedoFired);
        Assert.Contains(runner.Events, e => e is Events.EnemyTorpedoFired);
        Assert.Contains(runner.Events, e => e is Events.TorpedoDetonated);
    }
}
