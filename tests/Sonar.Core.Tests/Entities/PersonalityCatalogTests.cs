using System.Linq;
using Sonar.Core.Entities;
using Xunit;

namespace Sonar.Core.Tests.Entities;

public class PersonalityCatalogTests
{
    [Fact]
    public void TheRosterIsFiveAndTheyAreDistinguishable()
    {
        Assert.Equal(5, PersonalityCatalog.All.Count);
        Assert.Equal(5, PersonalityCatalog.All.Select(p => p.Name).Distinct().Count());
    }

    [Fact]
    public void OnlyTheStalkerAndTheHunterWithdrawToReload()
    {
        // El ciclo de acecho es que los dos radios difieran. Que PICKET y GOAD los tengan
        // iguales es lo que los deja clavados en su sitio.
        Assert.NotEqual(PersonalityCatalog.Stalker.RadiusLoaded, PersonalityCatalog.Stalker.RadiusEmpty);
        Assert.NotEqual(PersonalityCatalog.Hunter.RadiusLoaded, PersonalityCatalog.Hunter.RadiusEmpty);
        Assert.Equal(PersonalityCatalog.Picket.RadiusLoaded, PersonalityCatalog.Picket.RadiusEmpty);
        Assert.Equal(PersonalityCatalog.Goad.RadiusLoaded, PersonalityCatalog.Goad.RadiusEmpty);
    }

    [Fact]
    public void OnlyTheHunterCompensatesForDrift()
    {
        // Es la única diferencia que lo hace el enemigo del que hay que preocuparse: mientras
        // haya uno en el tablero, las estadísticas de fallo no protegen a nadie.
        Assert.Equal(1.0, PersonalityCatalog.Hunter.AimLead);
        foreach (var other in PersonalityCatalog.All.Where(p => p.Name != "HUNTER"))
            Assert.Equal(0.0, other.AimLead);
    }

    [Fact]
    public void OnlyTheRamRamsAndItIsTheOnlyOneThatNeverFires()
    {
        Assert.True(PersonalityCatalog.Ram.Rams);
        Assert.Equal(0.0, PersonalityCatalog.Ram.FiringRadius);

        foreach (var other in PersonalityCatalog.All.Where(p => p.Name != "RAM"))
        {
            Assert.False(other.Rams);
            Assert.True(other.FiringRadius > 0.0);
        }
    }

    [Fact]
    public void EveryHesitationWindowIsWellFormed()
    {
        foreach (var p in PersonalityCatalog.All.Where(p => p.FiringRadius > 0.0))
            Assert.True(p.HesitationMaxTicks >= p.HesitationMinTicks && p.HesitationMinTicks >= 0,
                $"{p.Name} tiene una ventana de duda inválida.");
    }
}
