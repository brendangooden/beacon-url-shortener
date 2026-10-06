using Shouldly;
using Beacon.Api.Domain;

namespace Beacon.Api.UnitTests.Domain;

public sealed class RetiredCodeTests
{
    [Test]
    public void Create_records_the_former_code_against_its_link()
    {
        var linkId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        var retired = RetiredCode.Create(linkId, "oldCode");

        retired.Id.ShouldNotBe(Guid.Empty);
        retired.LinkId.ShouldBe(linkId);
        retired.Code.ShouldBe("oldCode");
        retired.CreatedOnUtc.ShouldBeGreaterThanOrEqualTo(before);
    }

    [Test]
    public void Each_retired_code_gets_its_own_identity()
    {
        var linkId = Guid.NewGuid();

        var first = RetiredCode.Create(linkId, "codeA");
        var second = RetiredCode.Create(linkId, "codeB");

        first.Id.ShouldNotBe(second.Id);
    }
}
