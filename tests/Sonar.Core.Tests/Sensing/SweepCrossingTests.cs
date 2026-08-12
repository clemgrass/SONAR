using Sonar.Core.Sensing;
using Xunit;

namespace Sonar.Core.Tests.Sensing;

public class SweepCrossingTests
{
    [Fact]
    public void Crossed_WhenTargetIsBetweenPreviousAndCurrent()
    {
        Assert.True(SweepCrossing.Crossed(previous: 40, current: 60, target: 50));
    }

    [Fact]
    public void NotCrossed_WhenTargetIsAhead()
    {
        Assert.False(SweepCrossing.Crossed(previous: 40, current: 60, target: 90));
    }

    [Fact]
    public void NotCrossed_WhenTargetIsBehind()
    {
        Assert.False(SweepCrossing.Crossed(previous: 40, current: 60, target: 10));
    }

    [Fact]
    public void Crossed_AcrossTheWrapPoint()
    {
        // El haz pasó de 350 a 5 grados. Un contacto en 358 fue barrido.
        Assert.True(SweepCrossing.Crossed(previous: 350, current: 5, target: 358));
    }

    [Fact]
    public void Crossed_AcrossTheWrapPointOnTheLowSide()
    {
        Assert.True(SweepCrossing.Crossed(previous: 350, current: 5, target: 2));
    }

    [Fact]
    public void NotCrossed_AcrossTheWrapPointWhenOutside()
    {
        Assert.False(SweepCrossing.Crossed(previous: 350, current: 5, target: 180));
    }

    [Fact]
    public void Crossed_ExactlyOnCurrent_CountsAsCrossed()
    {
        Assert.True(SweepCrossing.Crossed(previous: 40, current: 60, target: 60));
    }

    [Fact]
    public void NotCrossed_ExactlyOnPrevious()
    {
        // Ya se registró en el tick anterior; no debe registrarse dos veces.
        Assert.False(SweepCrossing.Crossed(previous: 40, current: 60, target: 40));
    }

    [Fact]
    public void EveryBearing_IsCrossedExactlyOncePerRevolution()
    {
        // La propiedad que de verdad importa: un contacto quieto se registra una vez por
        // vuelta, ni cero ni dos. Los casos de arriba prueban puntos sueltos; este barre
        // una vuelta entera contra las 360 marcaciones enteras y cuenta.
        const double step = 360.0 / 210.0;   // paso que no divide a 360: cae en fracciones

        for (var target = 0; target < 360; target++)
        {
            var crossings = 0;
            var previous = 0.0;

            for (var i = 1; i <= 210; i++)
            {
                var current = i * step % 360.0;
                if (SweepCrossing.Crossed(previous, current, target)) crossings++;
                previous = current;
            }

            Assert.Equal(1, crossings);
        }
    }
}
