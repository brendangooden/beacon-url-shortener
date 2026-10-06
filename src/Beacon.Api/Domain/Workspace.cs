namespace Beacon.Api.Domain;

/// <summary>
/// A top-level container for Folders and Links. The creator is its owner (Manager). Access is
/// granted to other users via <see cref="ResourceGrant"/> and inherits down to folders and links.
/// A personal Workspace is auto-created on a User's first login.
/// </summary>
public sealed class Workspace
{
    private Workspace() { } // EF

    private Workspace(string name, string createdByOid, bool isPersonal)
    {
        Id = Guid.NewGuid();
        Name = name;
        IsPersonal = isPersonal;
        CreatedByOid = createdByOid;
        CreatedOnUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public bool IsPersonal { get; private set; }

    /// <summary>The owner (creator). Owner always has Manager access, no grant needed.</summary>
    public string CreatedByOid { get; private set; } = null!;
    public DateTime CreatedOnUtc { get; private set; }

    public static Workspace Create(string name, string ownerOid, bool isPersonal = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new Common.DomainException("A workspace name is required.");
        }

        return new Workspace(name.Trim(), ownerOid, isPersonal);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new Common.DomainException("A workspace name is required.");
        }

        Name = name.Trim();
    }
}
