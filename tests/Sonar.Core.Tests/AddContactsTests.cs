using System.Linq;
using Sonar.Core.Entities;
using Sonar.Core.Spatial;
using Sonar.Core.Tests.Support;
using Xunit;

namespace Sonar.Core.Tests;

public class AddContactsTests
{
    [Fact]
    public void AddContacts_KeepsTheOnesAlreadyThere()
    {
        // SetContacts reemplaza; el director de oleadas necesita sumar sin borrar.
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { Contact.CreateInert(new ContactId(1), new Vec2(0.5, 0.0)) });

        sim.AddContacts(new[] { Contact.CreateInert(new ContactId(2), new Vec2(0.0, 0.5)) });

        Assert.Equal(new[] { 1, 2 }, sim.State.Contacts.Select(c => c.Id.Value).ToArray());
    }

    [Fact]
    public void AddContacts_KeepsTheListSortedById()
    {
        var sim = TestWorld.Still();
        sim.SetContacts(new[] { Contact.CreateInert(new ContactId(9), new Vec2(0.5, 0.0)) });

        sim.AddContacts(new[] { Contact.CreateInert(new ContactId(3), new Vec2(0.0, 0.5)) });

        Assert.Equal(new[] { 3, 9 }, sim.State.Contacts.Select(c => c.Id.Value).ToArray());
    }
}
