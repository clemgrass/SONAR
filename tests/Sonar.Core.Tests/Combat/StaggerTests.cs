using System.Linq;
using Sonar.Core;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Combat;

public class StaggerTests
{
    [Fact]
    public void SpawnedEnemies_GetStaggeredFireTicks()
    {
        // Sin escalonar, todos disparan en el mismo tick y la presión llega en oleadas
        // sincronizadas en vez de sostenida (TDD §4.6).
        var config = TestWorld.Config();
        var spawner = new ContactSpawner(config);

        var contacts = spawner.SpawnEnemies(
            spawnTick: 0,
            firstId: 1,
            positions: new[]
            {
                new Vec2(0.0, -0.8), new Vec2(0.8, 0.0), new Vec2(0.0, 0.8), new Vec2(-0.8, 0.0),
            },
            velocities: new[] { Vec2.Zero, Vec2.Zero, Vec2.Zero, Vec2.Zero }).ToList();

        var fireTicks = contacts.Select(c => c.NextFireTick).ToList();
        Assert.Equal(fireTicks.Distinct().Count(), fireTicks.Count);

        for (var i = 1; i < fireTicks.Count; i++)
            Assert.Equal(config.EnemyFireStaggerTicks, fireTicks[i] - fireTicks[i - 1]);
    }

    [Fact]
    public void SpawnedEnemies_GetSequentialIds()
    {
        var spawner = new ContactSpawner(TestWorld.Config());
        var contacts = spawner.SpawnEnemies(0, firstId: 10,
            positions: new[] { new Vec2(0.0, -0.8), new Vec2(0.8, 0.0) },
            velocities: new[] { Vec2.Zero, Vec2.Zero }).ToList();

        Assert.Equal(new[] { 10, 11 }, contacts.Select(c => c.Id.Value).ToArray());
    }

    [Fact]
    public void Spawner_IsDeterministic()
    {
        var spawner = new ContactSpawner(TestWorld.Config());
        var positions = new[] { new Vec2(0.0, -0.8), new Vec2(0.8, 0.0) };
        var velocities = new[] { new Vec2(0.0, 0.1), new Vec2(-0.1, 0.0) };

        var first = spawner.SpawnEnemies(5, 1, positions, velocities).ToList();
        var second = spawner.SpawnEnemies(5, 1, positions, velocities).ToList();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Spawner_RejectsMismatchedLengths()
    {
        var spawner = new ContactSpawner(TestWorld.Config());

        // ToList() fuerza la enumeración: el método es un iterador y sin esto la
        // validación nunca correría.
        Assert.Throws<System.ArgumentException>(() => spawner.SpawnEnemies(0, 1,
            positions: new[] { Vec2.Zero, Vec2.Zero },
            velocities: new[] { Vec2.Zero }).ToList());
    }
}
