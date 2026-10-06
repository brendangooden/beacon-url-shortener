using Shouldly;
using Beacon.Api.Common;
using Beacon.Api.Domain;

namespace Beacon.Api.UnitTests.Domain;

public sealed class LinkTests
{
    private const string Owner = "owner-oid";
    private const string Destination = "https://example.com/landing";
    private static readonly Guid WorkspaceId = Guid.NewGuid();

    private static Link NewLink(string code = "launch1") => Link.Create(WorkspaceId, null, code, Destination, Owner);

    // ---- Create ----

    [Test]
    public void Create_sets_an_active_link_with_the_given_code_and_owner()
    {
        var folderId = Guid.NewGuid();

        var link = Link.Create(WorkspaceId, folderId, "launch1", Destination, Owner);

        link.Id.ShouldNotBe(Guid.Empty);
        link.WorkspaceId.ShouldBe(WorkspaceId);
        link.FolderId.ShouldBe(folderId);
        link.Code.ShouldBe("launch1");
        link.Destination.ShouldBe(Destination);
        link.CreatedByOid.ShouldBe(Owner);
        link.IsActive.ShouldBeTrue();
        link.IsDeleted.ShouldBeFalse();
        link.ClickCount.ShouldBe(0);
        link.Tags.ShouldBeEmpty();
    }

    [Test]
    [Arguments("api")]
    [Arguments("Health")]
    public void Create_rejects_a_reserved_code(string code)
    {
        Should.Throw<DomainException>(() => NewLink(code)).Message.ShouldBe("The short code is not valid.");
    }

    [Test]
    [Arguments("")]
    [Arguments("bad-code")]
    [Arguments("with space")]
    public void Create_rejects_a_malformed_code(string code)
    {
        Should.Throw<DomainException>(() => NewLink(code)).Message.ShouldBe("The short code is not valid.");
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("not a url")]
    [Arguments("/relative/path")]
    [Arguments("ftp://example.com/file")]
    [Arguments("javascript:alert(1)")]
    public void Create_rejects_a_destination_that_is_not_an_absolute_http_url(string destination)
    {
        Should.Throw<DomainException>(() => Link.Create(WorkspaceId, null, "launch1", destination, Owner));
    }

    [Test]
    public void Create_trims_and_normalises_the_destination()
    {
        var link = Link.Create(WorkspaceId, null, "launch1", "  HTTPS://Example.COM  ", Owner);

        link.Destination.ShouldBe("https://example.com/");
    }

    // ---- Rename ----

    [Test]
    public void Rename_changes_the_current_code_and_stamps_the_update()
    {
        var link = NewLink("oldCode");

        link.Rename("newCode");

        link.Code.ShouldBe("newCode");
        link.UpdatedOnUtc.ShouldNotBeNull();
    }

    [Test]
    [Arguments("admin")]
    [Arguments("bad-code")]
    [Arguments("")]
    public void Rename_rejects_a_reserved_or_malformed_code_and_keeps_the_old_one(string newCode)
    {
        var link = NewLink("oldCode");

        Should.Throw<DomainException>(() => link.Rename(newCode));

        link.Code.ShouldBe("oldCode");
    }

    // ---- UpdateDestination / metadata ----

    [Test]
    public void UpdateDestination_normalises_the_new_destination()
    {
        var link = NewLink();

        link.UpdateDestination(" https://Example.org/Path?q=1 ");

        link.Destination.ShouldBe("https://example.org/Path?q=1");
        link.UpdatedOnUtc.ShouldNotBeNull();
    }

    [Test]
    public void UpdateDestination_rejects_a_non_http_url_and_keeps_the_old_one()
    {
        var link = NewLink();

        Should.Throw<DomainException>(() => link.UpdateDestination("mailto:someone@example.com"));

        link.Destination.ShouldBe(Destination);
    }

    [Test]
    public void UpdateMetadata_trims_blanks_to_null_and_dedupes_tags()
    {
        var link = NewLink();

        link.UpdateMetadata("  Spring launch  ", "   ", [" promo ", "promo", "", "  ", "q2"]);

        link.Title.ShouldBe("Spring launch");
        link.Notes.ShouldBeNull();
        link.Tags.ShouldBe(["promo", "q2"]);
    }

    // ---- Expiry ----

    [Test]
    public void SetExpiry_rejects_a_past_date()
    {
        var link = NewLink();

        Should.Throw<DomainException>(() => link.SetExpiry(DateTime.UtcNow.AddMinutes(-1)))
            .Message.ShouldBe("The expiry must be in the future.");
        link.ExpiresOnUtc.ShouldBeNull();
    }

    [Test]
    public void SetExpiry_accepts_a_future_date_and_null_clears_it()
    {
        var link = NewLink();
        var future = DateTime.UtcNow.AddDays(7);

        link.SetExpiry(future);
        link.ExpiresOnUtc.ShouldBe(future);

        link.SetExpiry(null);
        link.ExpiresOnUtc.ShouldBeNull();
    }

    // ---- IsResolvable ----

    [Test]
    public void IsResolvable_is_true_for_an_active_link_with_no_expiry()
    {
        NewLink().IsResolvable(DateTime.UtcNow).ShouldBeTrue();
    }

    [Test]
    public void IsResolvable_is_false_when_disabled()
    {
        var link = NewLink();

        link.SetActive(false);

        link.IsResolvable(DateTime.UtcNow).ShouldBeFalse();
    }

    [Test]
    public void IsResolvable_is_false_once_the_expiry_has_passed()
    {
        var link = NewLink();
        var expiry = DateTime.UtcNow.AddHours(1);
        link.SetExpiry(expiry);

        link.IsResolvable(expiry.AddMinutes(-1)).ShouldBeTrue();
        link.IsResolvable(expiry).ShouldBeFalse();
        link.IsResolvable(expiry.AddMinutes(1)).ShouldBeFalse();
    }

    // ---- Soft delete / restore ----

    [Test]
    public void SoftDelete_moves_the_link_to_the_trash_with_who_and_when()
    {
        var link = NewLink();

        link.SoftDelete("deleter-oid");

        link.IsDeleted.ShouldBeTrue();
        link.DeletedByOid.ShouldBe("deleter-oid");
        link.DeletedOnUtc.ShouldNotBeNull();
    }

    [Test]
    public void Restore_brings_a_trashed_link_back()
    {
        var link = NewLink();
        link.SoftDelete("deleter-oid");

        link.Restore();

        link.IsDeleted.ShouldBeFalse();
        link.DeletedByOid.ShouldBeNull();
        link.DeletedOnUtc.ShouldBeNull();
        link.Code.ShouldBe("launch1");
    }

    [Test]
    public void RecordClick_increments_the_click_count()
    {
        var link = NewLink();

        link.RecordClick();
        link.RecordClick();

        link.ClickCount.ShouldBe(2);
    }
}
