using System;
using System.Linq;
using Sonar.Controller.Presentation;
using Sonar.Core;
using Sonar.Core.Entities;
using Sonar.Core.Sensing;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Controller.Tests;

public class SnapshotBuilderTests
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

    private static WorldState BaseState() =>
        WorldState.CreateInitial(Config(), new Bearing(0), 0.05);

    [Fact]
    public void ContactView_UsesTheReadingPositionNotTheRealOne()
    {
        // Esta es la línea de defensa más fuerte del diseño: la capa de presentación
        // no puede dibujar la verdad porque no la recibe.
        var config = Config();
        var state = BaseState()
            .WithContacts(new[]
            {
                new Contact
                {
                    Id = new ContactId(1), Kind = ContactKind.Enemy,
                    Position = new Vec2(0.90, 0.0),      // dónde está ahora
                    Velocity = Vec2.Zero, Alive = true, NextFireTick = int.MaxValue,
                },
            });

        state = state with
        {
            Tick = 100,
            Sonar = state.Sonar with
            {
                Readings = new[]
                {
                    new Reading
                    {
                        ContactId = new ContactId(1),
                        Position = new Vec2(0.50, 0.0),  // dónde el barrido lo vio
                        ObservedAtTick = 60,
                    },
                },
            },
        };

        var snapshot = SnapshotBuilder.Build(state, config.BuildBandLayout());

        var view = Assert.Single(snapshot.Contacts);
        Assert.Equal(0.50, view.Position.X, precision: 10);
        Assert.Equal(40, view.AgeTicks);
    }

    [Fact]
    public void Snapshot_ExposesNoWayToReachTheRealContactPosition()
    {
        // Refuerza el test de arriba a nivel de tipo: no basta con que Build elija bien,
        // el snapshot no debe cargar ninguna puerta trasera hacia WorldState o Contact.
        // Si esto falla es porque alguien agregó un campo que filtra la verdad.
        var reachable = typeof(WorldSnapshot).GetProperties()
            .Select(p => p.PropertyType)
            .Concat(typeof(ContactView).GetProperties().Select(p => p.PropertyType))
            .Select(t => t.IsGenericType ? t.GetGenericArguments()[0] : t)
            .ToArray();

        Assert.DoesNotContain(typeof(WorldState), reachable);
        Assert.DoesNotContain(typeof(Contact), reachable);
        Assert.DoesNotContain(typeof(Sonar.Core.Sensing.SonarState), reachable);
    }

    [Fact]
    public void ContactWithoutReading_IsNotInTheSnapshot()
    {
        var config = Config();
        var state = BaseState().WithContacts(new[]
        {
            Contact.CreateInert(new ContactId(1), new Vec2(0.5, 0.0)),
        });

        var snapshot = SnapshotBuilder.Build(state, config.BuildBandLayout());

        Assert.Empty(snapshot.Contacts);
    }

    [Fact]
    public void ContactView_CarriesBearingAndBandOfTheReading()
    {
        var config = Config();
        var state = BaseState().WithContacts(new[]
        {
            Contact.CreateInert(new ContactId(1), new Vec2(0.0, 0.0)),
        });
        state = state with
        {
            Sonar = state.Sonar with
            {
                Readings = new[]
                {
                    new Reading
                    {
                        ContactId = new ContactId(1),
                        Position = new Vec2(0.55, 0.0),   // este, banda C
                        ObservedAtTick = 0,
                    },
                },
            },
        };

        var view = Assert.Single(SnapshotBuilder.Build(state, config.BuildBandLayout()).Contacts);

        Assert.Equal(90, view.BearingDegrees);
        Assert.Equal('C', view.BandLetter);
    }

    [Fact]
    public void ReadingInsideTheDeadZone_GetsThePlaceholderBand()
    {
        // Fuera de toda banda no hay letra que mostrar. Se marca con '-' en vez de
        // inventar una banda, porque la UI necesita distinguir ese caso.
        var config = Config();
        var state = BaseState();
        state = state with
        {
            Sonar = state.Sonar with
            {
                Readings = new[]
                {
                    new Reading
                    {
                        ContactId = new ContactId(1),
                        Position = new Vec2(0.04, 0.0),   // dentro de la zona muerta
                        ObservedAtTick = 0,
                    },
                },
            },
        };

        var view = Assert.Single(SnapshotBuilder.Build(state, config.BuildBandLayout()).Contacts);

        Assert.Equal('-', view.BandLetter);
    }

    [Fact]
    public void OwnTorpedoes_AppearWithTheirRealPosition()
    {
        var config = Config();
        var layout = config.BuildBandLayout();
        var state = BaseState().WithTorpedoes(new[]
        {
            Torpedo.CreateOwn(new TorpedoId(1),
                new TargetCoordinate(new Bearing(90), new Band(2)), layout, 0.7),
        });

        var snapshot = SnapshotBuilder.Build(state, layout);

        var view = Assert.Single(snapshot.Torpedoes);
        Assert.Equal(0.10, view.Position.X, precision: 10);
        Assert.Empty(snapshot.EnemyTorpedoes);
    }

    [Fact]
    public void EnemyTorpedoes_AppearInTheirOwnList()
    {
        var config = Config();
        var state = BaseState().WithTorpedoes(new[]
        {
            Torpedo.CreateEnemy(new TorpedoId(2), new Vec2(0.0, -0.8), 0.45),
        });

        var snapshot = SnapshotBuilder.Build(state, config.BuildBandLayout());

        Assert.Single(snapshot.EnemyTorpedoes);
        Assert.Empty(snapshot.Torpedoes);
    }

    [Fact]
    public void ResourcesAreCarriedThrough()
    {
        var config = Config();
        var snapshot = SnapshotBuilder.Build(BaseState(), config.BuildBandLayout());

        Assert.Equal(4, snapshot.AmmoLoaded);
        Assert.Equal(12, snapshot.AmmoReserve);
        Assert.Equal(3, snapshot.DefensesAvailable);
        Assert.Equal(0, snapshot.HitsTaken);
        Assert.False(snapshot.Destroyed);
    }
}
