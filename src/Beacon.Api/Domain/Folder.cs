namespace Beacon.Api.Domain;

/// <summary>
/// An organisation container for Links inside one Workspace. The creator is its owner (Manager);
/// access inherits from the Workspace and down to the Folder's Links, and can be shared directly.
/// </summary>
public sealed class Folder
{
    private Folder() { } // EF

    private Folder(Guid workspaceId, string name, string createdByOid)
    {
        Id = Guid.NewGuid();
        WorkspaceId = workspaceId;
        Name = name;
        CreatedByOid = createdByOid;
        CreatedOnUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string Name { get; private set; } = null!;
    public string CreatedByOid { get; private set; } = null!;
    public DateTime CreatedOnUtc { get; private set; }

    public static Folder Create(Guid workspaceId, string name, string createdByOid)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new Common.DomainException("A folder name is required.");
        }

        return new Folder(workspaceId, name.Trim(), createdByOid);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new Common.DomainException("A folder name is required.");
        }

        Name = name.Trim();
    }
}
