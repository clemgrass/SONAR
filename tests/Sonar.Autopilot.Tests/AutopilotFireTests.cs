using System.Collections.Generic;
using Sonar.Autopilot.Tests.Support;
using Sonar.Controller.Presentation;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Autopilot.Tests;

public class AutopilotFireTests
{
    private static ContactView Contact(int id, Vec2 position) =>
        new(id, position, Bearing.FromVector(position).Degrees, 'E', 0);

    /// <summary>
    /// Piloto que ya vio dos lecturas de cada contacto, que es el mínimo para tener
    /// velocidad. La segunda observación va en el tick 420 — una vuelta del haz a 7 s.
    /// </summary>
    private static Pilot Tracking(IReadOnlyList<ContactView> first, IReadOnlyList<ContactView> second)
    {
        var pilot = new Pilot(TestConfig.Build());
        pilot.Observe(TestSnapshot.Build(0, first));
        pilot.Observe(TestSnapshot.Build(420, second));
        return pilot;
    }

    [Fact]
    public void WithOneReadingOnly_ItHoldsFire()
    {
        // Sin velocidad estimada no hay solución posible: disparar sería tirarle a donde
        // el blanco estaba. Un jugador está en la misma situación tras un solo barrido.
        var pilot = new Pilot(TestConfig.Build());
        var snapshot = TestSnapshot.Build(0, new[] { Contact(100, new Vec2(0, -0.90)) });
        pilot.Observe(snapshot);

        Assert.Null(pilot.Decide(snapshot));
    }

    [Fact]
    public void WithASolution_ItTypesTheCoordinate()
    {
        // El blanco cierra de 0.90 a 0.795 en una vuelta del haz. En los 10,5 s que tarda
        // el torpedo en llegar a la banda C el blanco ya estará ahí: el disparo es C000
        // aunque en este instante el contacto esté en la banda D.
        var first = new[] { Contact(100, new Vec2(0, -0.90)) };
        var second = new[] { Contact(100, new Vec2(0, -0.795)) };
        var pilot = Tracking(first, second);

        Assert.Equal("C000", pilot.Decide(TestSnapshot.Build(420, second)));

        // Control positivo de que la banda elegida no es donde está el contacto ahora.
        Assert.True(TestConfig.Build().BuildBandLayout().TryBandAt(0.795, out var current));
        Assert.Equal('D', current.Letter);
    }

    [Fact]
    public void TheNearestContactGoesFirst()
    {
        // Dos blancos con solución. Gana el de menor radio: el que ya está encima.
        var first = new[] { Contact(100, new Vec2(0, -0.90)), Contact(101, new Vec2(0.55, 0)) };
        var second = new[] { Contact(100, new Vec2(0, -0.795)), Contact(101, new Vec2(0.55, 0)) };
        var pilot = Tracking(first, second);

        Assert.Equal("C090", pilot.Decide(TestSnapshot.Build(420, second)));
    }

    [Fact]
    public void EmptyTubesAndEmptyReserve_MeanNoCommandAtAll()
    {
        var first = new[] { Contact(100, new Vec2(0, -0.90)) };
        var second = new[] { Contact(100, new Vec2(0, -0.795)) };
        var pilot = Tracking(first, second);

        Assert.Null(pilot.Decide(TestSnapshot.Build(420, second, ammoLoaded: 0, ammoReserve: 0)));
    }

    [Fact]
    public void WhileReloading_ItDoesNotFire()
    {
        // Un FIRE con la recarga en curso lo rechaza la simulación. Un comando rechazado no
        // es una respuesta: emitirlo inflaría el denominador del índice de saturación.
        var first = new[] { Contact(100, new Vec2(0, -0.90)) };
        var second = new[] { Contact(100, new Vec2(0, -0.795)) };
        var pilot = Tracking(first, second);

        Assert.Null(pilot.Decide(TestSnapshot.Build(420, second, reloadInProgress: true)));
    }

    [Fact]
    public void AStaleReadingIsNotWorthShootingAt()
    {
        // Un contacto sale de `snapshot.Contacts` sólo al morir, nunca por irse del alcance,
        // y el barrido no refresca nada más allá del radio 1.0. O sea que una lectura puede
        // envejecer sin cota, y extrapolar linealmente desde ella produce una solución que
        // el solucionador acepta con toda confianza: el error que mide es contra la posición
        // PREDICHA, y una predicción rancia es internamente consistente y falsa.
        var first = new[] { Contact(100, new Vec2(0, -0.90)) };
        var second = new[] { Contact(100, new Vec2(0, -0.795)) };
        var pilot = Tracking(first, second);

        // Tres barridos de antigüedad: 1580 ticks contra un tope de 1260.
        var stale = new[] { new ContactView(100, new Vec2(0, -0.795), 0, 'D', 1580) };
        Assert.Null(pilot.Decide(TestSnapshot.Build(2000, stale)));

        // Control positivo: la misma lectura, aún vigente, sí produce un disparo. En el tick
        // 2000 el blanco ya cerró hasta 0.40, así que la banda correcta es la B y no la C.
        var fresh = new[] { new ContactView(100, new Vec2(0, -0.795), 0, 'D', 100) };
        Assert.Equal("B000", pilot.Decide(TestSnapshot.Build(2000, fresh)));
    }

    [Fact]
    public void ItDoesNotFireTwiceAtTheSameSolution()
    {
        // Con tres tubos y un vuelo de diez segundos, sin esta regla el piloto vacía el
        // cargador entero contra un solo contacto y se pasa nueve segundos recargando.
        var first = new[] { Contact(100, new Vec2(0, -0.90)) };
        var second = new[] { Contact(100, new Vec2(0, -0.795)) };
        var pilot = Tracking(first, second);

        var inFlight = new[] { new TorpedoView(1, new Vec2(0, -0.30), 0, 0.55) };
        Assert.Null(pilot.Decide(TestSnapshot.Build(420, second, torpedoes: inFlight)));

        // Control positivo: el mismo torpedo por otra marcación no bloquea nada.
        var elsewhere = new[] { new TorpedoView(1, new Vec2(0, 0.30), 180, 0.55) };
        Assert.Equal("C000", pilot.Decide(TestSnapshot.Build(420, second, torpedoes: elsewhere)));
    }
}
