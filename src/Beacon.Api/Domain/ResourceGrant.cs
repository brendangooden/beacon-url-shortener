namespace Beacon.Api.Domain;

/// <summary>
/// A share: grants a User an <see cref="AccessLevel"/> on one resource (Workspace, Folder, or Link).
/// Access inherits down the hierarchy, so a grant on a Folder also covers its Links. The resource
/// owner (creator) and a Global-Admin always have Manager access without a grant.
/// </summary>
public sealed class ResourceGrant
{
    private ResourceGrant() { } // EF

    private ResourceGrant(ResourceType resourceType, Guid resourceId, string granteeOid, string granteeEmail, string granteeName, AccessLevel level, string grantedByOid)
    {
        Id = Guid.NewGuid();
        ResourceType = resourceType;
        ResourceId = resourceId;
        GranteeOid = granteeOid;
        GranteeEmail = granteeEmail;
        GranteeName = granteeName;
        Level = level;
        GrantedByOid = grantedByOid;
        CreatedOnUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public ResourceType ResourceType { get; private set; }
    public Guid ResourceId { get; private set; }
    public string GranteeOid { get; private set; } = null!;
    public string GranteeEmail { get; private set; } = null!;
    public string GranteeName { get; private set; } = null!;
    public AccessLevel Level { get; private set; }
    public string GrantedByOid { get; private set; } = null!;
    public DateTime CreatedOnUtc { get; private set; }

    public static ResourceGrant Create(ResourceType resourceType, Guid resourceId, string granteeOid, string granteeEmail, string granteeName, AccessLevel level, string grantedByOid)
    {
        if (string.IsNullOrWhiteSpace(granteeOid))
        {
            throw new Common.DomainException("A grantee identity is required.");
        }

        return new ResourceGrant(resourceType, resourceId, granteeOid, granteeEmail ?? string.Empty, granteeName ?? string.Empty, level, grantedByOid);
    }

    public void ChangeLevel(AccessLevel level) => Level = level;

    /// <summary>Promote a provisional (email-keyed) grant to the real identity on the grantee's first login.</summary>
    public void Reconcile(string oid, string email, string name)
    {
        if (string.IsNullOrWhiteSpace(oid))
        {
            throw new Common.DomainException("A grantee must have an identity (oid).");
        }

        GranteeOid = oid;
        GranteeEmail = email ?? GranteeEmail;
        GranteeName = string.IsNullOrWhiteSpace(name) ? GranteeName : name;
    }
}
