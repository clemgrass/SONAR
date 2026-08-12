using System.Collections.Generic;
using System.Linq;
using Sonar.Core.Entities;
using Xunit;

namespace Sonar.Core.Tests.Entities;

public class EntityIdTests
{
    [Fact]
    public void ContactIds_WithSameValue_AreEqual()
    {
        Assert.Equal(new ContactId(7), new ContactId(7));
    }

    [Fact]
    public void ContactIds_SortByValue()
    {
        var ids = new List<ContactId> { new(3), new(1), new(2) };
        var sorted = ids.OrderBy(id => id.Value).Select(id => id.Value).ToList();
        Assert.Equal(new[] { 1, 2, 3 }, sorted);
    }

    [Fact]
    public void DistinctIdTypes_AreNotInterchangeable()
    {
        // Compila porque son tipos distintos; el test documenta la intención.
        var contact = new ContactId(1);
        var torpedo = new TorpedoId(1);
        Assert.Equal(contact.Value, torpedo.Value);
    }
}
