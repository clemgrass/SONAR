using Sonar.Core.Entities;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Core.Tests.Entities;

public class ContactTests
{
    [Fact]
    public void Enemy_CarriesItsOwnVelocity()
    {
        var contact = new Contact
        {
            Id = new ContactId(1),
            Kind = ContactKind.Enemy,
            Position = new Vec2(0.5, 0.0),
            Velocity = new Vec2(-0.1, 0.0),
            Alive = true,
            NextFireTick = 100,
        };

        Assert.Equal(ContactKind.Enemy, contact.Kind);
        Assert.Equal(-0.1, contact.Velocity.X, precision: 10);
    }

    [Fact]
    public void With_ProducesModifiedCopyLeavingOriginalIntact()
    {
        var original = new Contact
        {
            Id = new ContactId(1),
            Kind = ContactKind.Inert,
            Position = new Vec2(0.5, 0.0),
            Velocity = Vec2.Zero,
            Alive = true,
            NextFireTick = 0,
        };

        var moved = original with { Position = new Vec2(0.6, 0.0) };

        Assert.Equal(0.5, original.Position.X, precision: 10);
        Assert.Equal(0.6, moved.Position.X, precision: 10);
        Assert.Equal(original.Id, moved.Id);
    }

    [Fact]
    public void Inert_HasZeroOwnVelocity()
    {
        var wreck = Contact.CreateInert(new ContactId(9), new Vec2(0.3, 0.4));
        Assert.Equal(Vec2.Zero, wreck.Velocity);
        Assert.Equal(ContactKind.Inert, wreck.Kind);
        Assert.True(wreck.Alive);
    }

    [Fact]
    public void AContactWithoutPersonalityKeepsTheOldShape()
    {
        // Los campos nuevos son opcionales a propósito: un contacto sin personalidad se
        // comporta exactamente como antes, y por eso los tests que ya existían siguen
        // valiendo sin tocarlos.
        var contact = new Contact
        {
            Id = new ContactId(1),
            Kind = ContactKind.Enemy,
            Position = new Vec2(0.0, -0.5),
            Velocity = Vec2.Zero,
            Alive = true,
            NextFireTick = 100,
        };

        Assert.Null(contact.Personality);
        Assert.Equal(0, contact.LastFixTick);
    }
}
