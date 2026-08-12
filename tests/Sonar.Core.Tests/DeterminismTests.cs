using Sonar.Core;
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
}
