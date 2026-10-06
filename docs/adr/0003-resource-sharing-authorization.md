# Resource-level sharing, not a workspace permission boundary

Authorization is **per-resource sharing with downward inheritance**, and there are only **two system
roles**: Global Admin and User. This supersedes the original model where a Workspace was "the
permission boundary" holding Members with Owner/Editor/Viewer roles.

- **System roles**: `GlobalAdmin` (full access to everything + system settings) and `User`. Global
  Admin is recognised by the same config allowlist mechanism as before (oid / email / group / claim),
  now under `Auth:GlobalAdmins`.
- **Ownership**: a Workspace, Folder, or Link is owned by its creator (`CreatedByOid`); the owner has
  `Manager` access without a grant.
- **Shares**: a single `ResourceGrant` (ResourceType + ResourceId + grantee + `AccessLevel`) shares
  any resource with a user. Access levels: `Viewer` &lt; `Editor` &lt; `Manager`.
- **Inheritance**: access flows **down** Workspace → Folder → Link. A user's effective level on a
  resource is the highest of: Global-Admin, ownership of it or an ancestor, a direct grant, or a
  grant on an ancestor. `ResourceAccess` computes this.

## Consequences

- A Workspace is no longer a security boundary — it's just a shareable top-level container. A user
  with only a Link shared to them sees just that Link (via "Shared with me"), not its Workspace.
- Listing is workspace-scoped and needs at least Viewer on the workspace; directly-shared resources
  surface through `/shares/with-me`, which carries the `workspaceId` so a shared Link stays openable.
- Grants keyed by email before the grantee's first login are reconciled to their real `oid` on login
  (same pattern the old workspace membership used).
- Deleting a resource must also delete its grants (no FK — grants are polymorphic); services do this.
- The migration drops `workspace_members`; existing workspace owners keep Manager access via
  ownership, so no access is lost for the common (owner-only) case.

## Considered and rejected

- **Keep the workspace-as-boundary model** (Owner/Editor/Viewer members): simpler, but can't express
  "share one folder / one link with someone", which is the whole point of the change.
- **Collapse Workspaces into Folders**: rejected — keeps the team-space grouping and is a smaller
  migration (see the hierarchy decision).
