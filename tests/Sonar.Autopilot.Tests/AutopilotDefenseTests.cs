using Sonar.Autopilot.Tests.Support;
using Sonar.Controller.Presentation;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Autopilot.Tests;

public class AutopilotDefenseTests
{
    // A 0.40 de radio y 0.03 por segundo, el impacto llega en 13,3 s: dentro de la ventana.
    private static readonly EnemyTorpedoView[] Inbound =
        { new(1, new Vec2(0.40, 0)) };

    // A 0.90 el impacto está a 30 s: todavía no se gasta la única defensa.
    private static readonly EnemyTorpedoView[] FarInbound =
        { new(1, new Vec2(0.90, 0)) };

    [Fact]
    public void AnInboundInsideTheWindow_GetsTheDefense()
    {
        // Banda B: es la más externa cuyo centro (0.37) el torpedo todavía no cruzó.
        var pilot = new Pilot(TestConfig.Build());
        Assert.Equal("DEF B090", pilot.Decide(TestSnapshot.Build(0, enemyTorpedoes: Inbound)));
    }

    [Fact]
    public void AFarInbound_IsLeftAlone()
    {
        var pilot = new Pilot(TestConfig.Build());
        Assert.Null(pilot.Decide(TestSnapshot.Build(0, enemyTorpedoes: FarInbound)));

        // Control positivo: el mismo torpedo, ya cerca, sí produce una defensa.
        Assert.Equal("DEF B090", pilot.Decide(TestSnapshot.Build(0, enemyTorpedoes: Inbound)));
    }

    [Fact]
    public void WithOneAlreadyInTheWater_ThereIsNothingToDeploy()
    {
        var pilot = new Pilot(TestConfig.Build());
        Assert.Null(pilot.Decide(
            TestSnapshot.Build(0, enemyTorpedoes: Inbound, defensesAvailable: 0)));
    }

    [Fact]
    public void ItDoesNotPutTheDefenseInItsOwnLane()
    {
        // Una defensa mata los torpedos propios. Cambiar un tiro propio por uno enemigo es
        // mal negocio cuando recargar cuesta un comando y tres segundos.
        var pilot = new Pilot(TestConfig.Build());
        var ownLane = new[] { new TorpedoView(2, new Vec2(0.30, 0), 90, 0.55) };

        Assert.Null(pilot.Decide(
            TestSnapshot.Build(0, torpedoes: ownLane, enemyTorpedoes: Inbound)));

        // Control positivo: el mismo torpedo propio por la marcación opuesta no estorba.
        var otherLane = new[] { new TorpedoView(2, new Vec2(-0.30, 0), 270, 0.55) };
        Assert.Equal("DEF B090", pilot.Decide(
            TestSnapshot.Build(0, torpedoes: otherLane, enemyTorpedoes: Inbound)));
    }

    [Fact]
    public void DefendingComesBeforeShooting()
    {
        // Un blanco resuelto y un torpedo encima: el turno es para la defensa.
        var pilot = new Pilot(TestConfig.Build());
        var first = new[] { new ContactView(100, new Vec2(0, -0.90), 0, 'E', 0) };
        var second = new[] { new ContactView(100, new Vec2(0, -0.795), 0, 'D', 0) };
        pilot.Observe(TestSnapshot.Build(0, first));
        pilot.Observe(TestSnapshot.Build(420, second));

        Assert.Equal("DEF B090", pilot.Decide(
            TestSnapshot.Build(420, second, enemyTorpedoes: Inbound)));

        // Control positivo: sin el entrante, ese mismo turno era un disparo.
        Assert.Equal("C000", pilot.Decide(TestSnapshot.Build(420, second)));
    }
}
