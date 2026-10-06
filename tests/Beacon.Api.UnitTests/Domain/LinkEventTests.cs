using Shouldly;
using Beacon.Api.Domain;

namespace Beacon.Api.UnitTests.Domain;

public sealed class LinkEventTests
{
    [Test]
    public void Record_keeps_who_what_and_the_old_and_new_values()
    {
        var linkId = Guid.NewGuid();

        var evt = LinkEvent.Record(linkId, LinkEventType.Renamed, "actor-oid", "Alex Rivera", "oldCode", "newCode");

        evt.LinkId.ShouldBe(linkId);
        evt.Type.ShouldBe(LinkEventType.Renamed);
        evt.ActorOid.ShouldBe("actor-oid");
        evt.ActorName.ShouldBe("Alex Rivera");
        evt.OldValue.ShouldBe("oldCode");
        evt.NewValue.ShouldBe("newCode");
    }

    [Test]
    public void Record_falls_back_to_the_oid_when_the_actor_has_no_name()
    {
        LinkEvent.Record(Guid.NewGuid(), LinkEventType.Created, "actor-oid", "  ").ActorName.ShouldBe("actor-oid");
    }

    [Test]
    public void Record_blanks_empty_values_and_caps_long_ones_at_300_chars()
    {
        var evt = LinkEvent.Record(Guid.NewGuid(), LinkEventType.DestinationChanged, "a", "A", "   ", new string('x', 500));

        evt.OldValue.ShouldBeNull();
        evt.NewValue.ShouldNotBeNull().Length.ShouldBe(300);
    }
}
