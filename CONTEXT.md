# Beacon

A self-hostable, multi-tenant link shortener. Staff sign in, create and manage short links, group them into workspaces/folders, and see click analytics. Redirects run at `/{code}` on the deployment origin (for example `beacon.example.com`). The app is provider-agnostic for identity (Entra first; see [[auth]]).

## Language

**Link**:
A single short URL: a **ShortCode** plus its destination and metadata. Belongs to exactly one **Folder**.
_Avoid_: URL, shortlink, slug (the *code* is the slug, not the whole Link).

**ShortCode**:
The unique key in the short URL path (`beacon.../{code}`). Resolves to one **Link**'s destination.
A **Link** has exactly one current ShortCode at a time.
_Avoid_: alias, token, key.

**Retired code**:
A former **ShortCode** of a **Link**, kept after a rename so it still forwards to the Link. A Link
can have zero or more Retired codes. Not reusable by any other Link.
_Avoid_: alias (reserved as an avoid-term for ShortCode itself — don't reuse it here either).

**Destination**:
The long target URL a **ShortCode** redirects to.
_Avoid_: long URL, original URL, target (use "Destination").

**Resource**:
A **Workspace**, **Folder**, or **Link** — the three shareable things. Each has an **Owner** and can be shared. Access flows down: Workspace → Folder → Link.
_Avoid_: object, entity, item.

**Folder**:
An organisation container for **Links** inside one **Workspace**. A shareable **Resource** with an **Owner**.
_Avoid_: directory, group, tag.

**Workspace**:
A top-level container for **Folders** and **Links**. A shareable **Resource** with an **Owner**. NOT a permission boundary — access is per-Resource via **Shares**.
_Avoid_: team, project, account, tenant, boundary.

**Owner**:
The creator of a **Resource**. An Owner always has **Manager** access to it (and, by inheritance, everything inside it) without a **Share**.
_Avoid_: admin (that's the system role).

**Share**:
Grants a **User** an **Access level** on one **Resource**. Inherits down the hierarchy (a Share on a Folder also covers its Links).
_Avoid_: membership, permission, ACL, grant (the code type is `ResourceGrant`, but the term is "Share").

**Access level**:
What a **Share** (or ownership / Global-Admin) allows on a **Resource**: **Viewer**, **Editor**, or **Manager**.
- Viewer: open the Link + see its analytics.
- Editor: also create / update / move / disable Links and Folders.
- Manager: also re-share (Share/unshare) and delete the Resource.
_Avoid_: role (that word is reserved for the two system roles).

**Global Admin**:
One of the two system roles; full access to every **Resource** and system settings (e.g. **Branding**). The other system role is a plain **User**.
_Avoid_: super-admin, root, super-user.

**User**:
A staff person (the ordinary system role), authenticated via a configured identity provider (Entra ID first). Owns the **Resources** they create and sees ones **Shared** with them.

**Rename**:
Changing a **Link**'s current **ShortCode** to a new one. The old ShortCode becomes a **Retired code**:
it keeps forwarding to the Link and is never available to any Link (including this one) again.

**Click**:
One resolution of a **ShortCode** — the analytics unit. Recorded at redirect time.
_Avoid_: hit, visit, view.

**Branding**:
The global look of the public surface: app name, tagline, logo, primary colour, and home URL.
One record; a **Global Admin** edits it. Feeds the **Landing page** and the admin shell.
_Avoid_: theme, settings, config.

**Landing page**:
The branded HTML page served for an unknown, disabled, or expired **ShortCode** (and any other
unmatched non-API path), in place of a raw 404 response. Rendered from **Branding**.
_Avoid_: error page (it IS a 404, but "Landing page" is the term).

## Example dialogue

> **Dev:** If I share a Folder with someone as Editor, what can they touch?
> **Expert:** The Folder and every Link in it — access inherits down. They can't touch other Folders in the Workspace, and they won't even see the Workspace unless it's shared too. It shows up in their "Shared with me".
> **Dev:** Can they delete a Link in that Folder?
> **Expert:** No — delete needs Manager. Editor can create/edit/disable. The Link's Owner, a Folder/Workspace Manager, or a Global Admin can delete it.
> **Dev:** When someone hits a ShortCode, that's a Click?
> **Expert:** One Click per resolution, recorded even for a cache hit.
