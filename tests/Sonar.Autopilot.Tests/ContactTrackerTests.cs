using Sonar.Autopilot.Tests.Support;
using Sonar.Controller.Presentation;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Autopilot.Tests;

public class ContactTrackerTests
{
    private static ContactView Contact(int id, Vec2 position, int ageTicks) =>
        new(id, position, Bearing.FromVector(position).Degrees, 'E', ageTicks);

    [Fact]
    public void OneReading_IsNotEnoughToEstimate()
    {
        var tracker = new ContactTracker();
        tracker.Observe(TestSnapshot.Build(0, new[] { Contact(100, new Vec2(0, -0.90), 0) }));

        Assert.False(tracker.TryEstimate(100, out _));

        // Control positivo: el §4 del contrato pide que toda afirmación de ausencia pruebe
        // que el escenario sí se produjo. Con una segunda lectura de verdad, sí estima.
        tracker.Observe(TestSnapshot.Build(420, new[] { Contact(100, new Vec2(0, -0.80), 0) }));
        Assert.True(tracker.TryEstimate(100, out _));
    }

    [Fact]
    public void TwoReadings_GiveTheVelocityBetweenThem()
    {
        var tracker = new ContactTracker();
        tracker.Observe(TestSnapshot.Build(0, new[] { Contact(100, new Vec2(0, -0.90), 0) }));
        tracker.Observe(TestSnapshot.Build(420, new[] { Contact(100, new Vec2(0, -0.80), 0) }));

        Assert.True(tracker.TryEstimate(100, out var estimate));
        Assert.Equal(new Vec2(0, -0.80), estimate.Position);
        Assert.Equal(420, estimate.ObservedAtTick);
        Assert.Equal(0.10 / 420.0, estimate.VelocityPerTick.Y, 12);
        Assert.Equal(0.0, estimate.VelocityPerTick.X, 12);
    }

    [Fact]
    public void TheSameReadingSeenTwice_DoesNotCountAsTwo()
    {
        // Observe corre cada tick, pero el barrido sólo refresca la lectura al cruzarla.
        // Si la misma lectura contara dos veces, la velocidad saldría cero y el bot
        // dispararía a donde el blanco estaba.
        var tracker = new ContactTracker();
        tracker.Observe(TestSnapshot.Build(0, new[] { Contact(100, new Vec2(0, -0.90), 0) }));
        tracker.Observe(TestSnapshot.Build(1, new[] { Contact(100, new Vec2(0, -0.90), 1) }));
        tracker.Observe(TestSnapshot.Build(2, new[] { Contact(100, new Vec2(0, -0.90), 2) }));

        Assert.False(tracker.TryEstimate(100, out _));

        // Control positivo: una lectura de verdad nueva sí produce estimación.
        tracker.Observe(TestSnapshot.Build(420, new[] { Contact(100, new Vec2(0, -0.80), 0) }));
        Assert.True(tracker.TryEstimate(100, out _));
    }

    [Fact]
    public void Extrapolate_ProjectsForwardFromTheLastReading()
    {
        var tracker = new ContactTracker();
        tracker.Observe(TestSnapshot.Build(0, new[] { Contact(100, new Vec2(0, -0.90), 0) }));
        tracker.Observe(TestSnapshot.Build(420, new[] { Contact(100, new Vec2(0, -0.80), 0) }));

        Assert.True(tracker.TryEstimate(100, out var estimate));
        var projected = ContactTracker.Extrapolate(estimate, 840);

        Assert.Equal(-0.70, projected.Y, 10);
    }

    [Fact]
    public void ARetryStartsTheTrackingOver()
    {
        // RETRY construye una Simulation nueva: el tick vuelve a cero Y los ids de contacto
        // vuelven a 100, porque el WaveDirector también se reconstruye. Sin olvidar, la
        // entrada vieja del contacto 100 recibe una lectura del intento nuevo con
        // observedAt 0 contra LatestTick 420, la toma por lectura nueva, y estima con un
        // span NEGATIVO: una velocidad de módulo cuatro veces la del torpedo propio,
        // apuntando a ninguna parte.
        var tracker = new ContactTracker();
        tracker.Observe(TestSnapshot.Build(0, new[] { Contact(100, new Vec2(0, -0.90), 0) }));
        tracker.Observe(TestSnapshot.Build(420, new[] { Contact(100, new Vec2(0, -0.80), 0) }));
        Assert.True(tracker.TryEstimate(100, out _));

        // Intento 2: mismo id, reloj en cero.
        tracker.Observe(TestSnapshot.Build(0, new[] { Contact(100, new Vec2(0, -0.95), 0) }));
        Assert.False(tracker.TryEstimate(100, out _));

        // Control positivo: la corrida nueva vuelve a estimar con SUS dos lecturas, no con
        // una mezcla de las dos corridas. El valor 0.20/420 es distinto del 0.10/420 del
        // intento 1 justamente para que una mezcla no pueda pasar por buena.
        tracker.Observe(TestSnapshot.Build(420, new[] { Contact(100, new Vec2(0, -0.75), 0) }));
        Assert.True(tracker.TryEstimate(100, out var estimate));
        Assert.Equal(0.20 / 420.0, estimate.VelocityPerTick.Y, 12);
    }

    [Fact]
    public void ContactsAreTrackedIndependently()
    {
        var tracker = new ContactTracker();
        tracker.Observe(TestSnapshot.Build(0, new[]
        {
            Contact(100, new Vec2(0, -0.90), 0),
            Contact(101, new Vec2(0.90, 0), 0),
        }));
        tracker.Observe(TestSnapshot.Build(420, new[]
        {
            Contact(100, new Vec2(0, -0.80), 0),
            Contact(101, new Vec2(0.85, 0), 0),
        }));

        Assert.True(tracker.TryEstimate(100, out var first));
        Assert.True(tracker.TryEstimate(101, out var second));
        Assert.Equal(0.0, first.VelocityPerTick.X, 12);
        Assert.Equal(-0.05 / 420.0, second.VelocityPerTick.X, 12);
    }
}
