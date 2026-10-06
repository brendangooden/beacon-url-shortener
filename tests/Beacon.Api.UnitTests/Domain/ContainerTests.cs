using Shouldly;
using Beacon.Api.Common;
using Beacon.Api.Domain;

namespace Beacon.Api.UnitTests.Domain;

/// <summary>Workspace and Folder: the two Link containers share the same naming invariants.</summary>
public sealed class ContainerTests
{
    [Test]
    public void Workspace_create_trims_the_name_and_sets_the_owner()
    {
        var ws = Workspace.Create("  Marketing  ", "owner-oid");

        ws.Name.ShouldBe("Marketing");
        ws.CreatedByOid.ShouldBe("owner-oid");
        ws.IsPersonal.ShouldBeFalse();
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public void Workspace_requires_a_name_on_create_and_rename(string name)
    {
        Should.Throw<DomainException>(() => Workspace.Create(name, "owner-oid"));

        var ws = Workspace.Create("Marketing", "owner-oid");
        Should.Throw<DomainException>(() => ws.Rename(name));
        ws.Name.ShouldBe("Marketing");
    }

    [Test]
    public void Folder_create_trims_the_name_and_keeps_its_workspace()
    {
        var workspaceId = Guid.NewGuid();

        var folder = Folder.Create(workspaceId, "  Campaigns ", "owner-oid");

        folder.Name.ShouldBe("Campaigns");
        folder.WorkspaceId.ShouldBe(workspaceId);
        folder.CreatedByOid.ShouldBe("owner-oid");
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public void Folder_requires_a_name_on_create_and_rename(string name)
    {
        Should.Throw<DomainException>(() => Folder.Create(Guid.NewGuid(), name, "owner-oid"));

        var folder = Folder.Create(Guid.NewGuid(), "Campaigns", "owner-oid");
        Should.Throw<DomainException>(() => folder.Rename(name));
        folder.Name.ShouldBe("Campaigns");
    }

    [Test]
    public void ResourceGrant_requires_a_grantee_identity()
    {
        Should.Throw<DomainException>(() =>
            ResourceGrant.Create(ResourceType.Folder, Guid.NewGuid(), " ", "a@b.test", "A", AccessLevel.Viewer, "granter"));
    }

    [Test]
    public void ResourceGrant_reconcile_swaps_the_provisional_email_key_for_the_real_oid()
    {
        var grant = ResourceGrant.Create(ResourceType.Link, Guid.NewGuid(), "sam@b.test", "sam@b.test", "sam@b.test", AccessLevel.Editor, "granter");

        grant.Reconcile("real-oid", "sam@b.test", "Sam Lee");

        grant.GranteeOid.ShouldBe("real-oid");
        grant.GranteeName.ShouldBe("Sam Lee");
        grant.Level.ShouldBe(AccessLevel.Editor);
    }

    [Test]
    public void Access_levels_are_ordered_viewer_editor_manager()
    {
        (AccessLevel.Viewer < AccessLevel.Editor).ShouldBeTrue();
        (AccessLevel.Editor < AccessLevel.Manager).ShouldBeTrue();
    }
}
