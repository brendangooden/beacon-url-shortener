namespace Beacon.Api.Domain;

/// <summary>
/// What a share (or ownership/admin) lets someone do with a resource. Ordered so "&gt;= Editor"
/// comparisons work. Inheritance and ownership resolve to the highest level that applies.
/// - Viewer: open the link + see its analytics.
/// - Editor: also create/update/move/disable links and folders.
/// - Manager: also re-share (grant/revoke) and delete the resource.
/// </summary>
public enum AccessLevel
{
    Viewer = 0,
    Editor = 1,
    Manager = 2,
}
