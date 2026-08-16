using System;
using System.Collections.Generic;
using System.Linq;
using Sonar.Core.Commands;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests.Combat;

public class HesitationTests
{
    private static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    private static Contact Enemy(int id, Personality personality) => new()
    {
        Id = new ContactId(id),
        Kind = ContactKind.Enemy,
        Position = new Vec2(0.0, -0.90),
        Velocity = Vec2.Zero,
        Alive = true,
        NextFireTick = 0,
        LastFixTick = 0,
        Personality = personality,
    };

    private static List<int> FireTicks(Personality personality, int seed, int ticks = 4000)
    {
        var config = TestWorld.Config();
        var sim = new Simulation(config, new SeededRandomSource(seed),
            WorldState.CreateInitial(config, new Bearing(0), 0.0));
        sim.SetContacts(new[] { Enemy(1, personality) });

        var fired = new List<int>();
        for (var i = 0; i < ticks; i++)
        {
            foreach (var e in sim.Tick(NoCommands))
                if (e is EnemyTorpedoFired f) fired.Add(f.Tick);
            // El torpedo en el agua bloquearía el segundo disparo; se limpia para medir
            // sólo el reloj de recarga más la duda.
            sim.SetTorpedoes(Array.Empty<Torpedo>());
        }
        return fired;
    }

    [Fact]
    public void TheGapBetweenShotsIsNotAlwaysTheSame()
    {
        var ticks = FireTicks(PersonalityCatalog.Picket, seed: 7);
        Assert.True(ticks.Count >= 4, "hacen falta varios disparos para medir la variación");

        var gaps = ticks.Zip(ticks.Skip(1), (a, b) => b - a).ToList();
        Assert.True(gaps.Distinct().Count() > 1,
            $"todos los intervalos son iguales ({gaps[0]}): no hay duda ninguna");
    }

    [Fact]
    public void EveryGapStaysInsideReloadPlusTheWindow()
    {
        var config = TestWorld.Config();
        var picket = PersonalityCatalog.Picket;
        var ticks = FireTicks(picket, seed: 11);
        var gaps = ticks.Zip(ticks.Skip(1), (a, b) => b - a);

        foreach (var gap in gaps)
            Assert.InRange(gap,
                config.EnemyReloadTicks + picket.HesitationMinTicks,
                config.EnemyReloadTicks + picket.HesitationMaxTicks);
    }

    [Fact]
    public void TheGoadHesitatesLessThanThePicket()
    {
        // El ancho de la ventana es carácter: el impulsivo tira apenas puede.
        var goad = FireTicks(PersonalityCatalog.Goad, seed: 3);
        var picket = FireTicks(PersonalityCatalog.Picket, seed: 3);

        Assert.True(goad.Count > picket.Count,
            $"el GOAD disparó {goad.Count} veces y el PICKET {picket.Count}");
    }

    [Fact]
    public void TheSameSeedProducesTheSameHesitations()
    {
        Assert.Equal(FireTicks(PersonalityCatalog.Stalker, seed: 42),
                     FireTicks(PersonalityCatalog.Stalker, seed: 42));
    }

    [Fact]
    public void DifferentSeedsProduceDifferentHesitations()
    {
        Assert.NotEqual(FireTicks(PersonalityCatalog.Stalker, seed: 1),
                        FireTicks(PersonalityCatalog.Stalker, seed: 2));
    }
}
