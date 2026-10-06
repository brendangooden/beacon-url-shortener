namespace Beacon.Api.Domain;

/// <summary>A shareable resource. Access flows down: Workspace -&gt; Folder -&gt; Link.</summary>
public enum ResourceType
{
    Workspace,
    Folder,
    Link,
}
