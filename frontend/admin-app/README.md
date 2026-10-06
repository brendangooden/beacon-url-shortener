# Link Admin (SPA)

React + Vite + TypeScript admin UI for the Beacon link shortener. TanStack Router (code-based, `/admin`
basepath) + TanStack Query + Zustand, Tailwind v4, hand-rolled shadcn-style primitives, Recharts.

## Commands

```bash
npm install
npm run dev      # http://localhost:34100/admin/  (proxies /api -> http://localhost:34110)
npm run build    # type-checks (tsc -b) then builds to dist/
npm run preview  # serve the production build locally
npm run lint
```

## Environment

| Var            | Default | Notes                                                                 |
| -------------- | ------- | --------------------------------------------------------------------- |
| `VITE_API_URL` | `/api`  | Leave unset in dev (Vite proxy) and in the single-origin prod deploy. |

The app is served under **`/admin/`** in production (single origin behind Traefik: `/{code}`
redirects, `/api` the API, `/admin` this SPA). `vite build` bakes that base into asset paths.

## Auth — two modes, config-gated

The app runs in one of two modes decided **at build time** by whether `VITE_ENTRA_CLIENT_ID` is set:

**Dev mode (no `VITE_ENTRA_CLIENT_ID`)** — the default for `npm run dev` and the current POC
deploy. MSAL is never initialized, there is no login screen, and the backend's Dev auth provider
authenticates every request. Behaves exactly as before.

**Entra mode (`VITE_ENTRA_CLIENT_ID` set)** — MSAL bearer flow (`@azure/msal-browser` +
`@azure/msal-react`). On boot the app processes the redirect, and with no account signs in via
`loginRedirect` (clean "Signing in…" splash). The API client acquires an access token
(`acquireTokenSilent`, interactive fallback) and sends `Authorization: Bearer …` on every `/api/*`
call; a 401 triggers interactive re-auth. Public reads (`/branding`, `/branding/logo`, redirects)
need no token. A sign-out control appears in the rail. Token cache is `sessionStorage`
(POC-acceptable; in-memory is more secure).

### Build args (all optional; baked at build)

| Var                    | Example                                                    |
| ---------------------- | --------------------------------------------------------- |
| `VITE_ENTRA_CLIENT_ID` | `<app-registration-client-id>`                            |
| `VITE_ENTRA_TENANT_ID` | `<directory-tenant-id>`                                    |
| `VITE_ENTRA_SCOPE`     | `api://<clientId>/access_as_user`                         |

Authority is `https://login.microsoftonline.com/${VITE_ENTRA_TENANT_ID}`; redirect URI is
`${window.location.origin}/admin` (dynamic — register each host's `/admin` on the app reg).

## Design & branding

Design system: **"beacon at dusk"** — an ink-navy left rail, cool off-white content, and one
signature accent (amber) spent on the active-nav beacon indicator and the mono code pills;
everything else stays quiet. Fonts (Google Fonts): Space Grotesk (display), IBM Plex Sans (UI),
IBM Plex Mono (codes/counts). Tokens live on `:root` in `src/styles.css` with a
`prefers-color-scheme: dark` block.

Runtime branding: on load the app reads `GET /branding` and sets the document `--accent` to the
admin's primary color (fallback amber `#E8A317`), swaps in the logo/app-name wordmark, and sets
`document.title`. **Global Admins** edit it at **Settings → Branding** (name, tagline, home URL,
color, logo upload/remove) with a live 404-landing preview; saving invalidates the branding query
so the shell updates immediately.

## Access model

Two system roles: **Global Admin** (everything) and **User**. Each Workspace/Folder/Link has an
owner and can be **shared** at a level — `Viewer` (open + analytics), `Editor` (+ create/edit/move/
disable), `Manager` (+ re-share + delete); access inherits Workspace → Folder → Link. The UI gates
on the resource's effective `access` (`lib/access.ts`: `canEdit`, `canManage`). Sharing is managed
per-resource via `ShareDialog` (workspace Share button, folder rail hover-action, link row-action —
all shown at `Manager`). **Shared with me** lists resources others shared with you; opening a shared
link routes to its analytics with `?ws=<workspaceId>` so it resolves against its own workspace.

## Layout

- `src/lib/` — fetch client (`api.ts`), DTO types, `cn()`, `access.ts`
- `src/queries/hooks.ts` — all TanStack Query hooks + mutations (workspaces, folders, links, shares, branding)
- `src/state/` — selected workspace + folder store (Zustand)
- `src/components/` — UI primitives (`ui.tsx`), `dialogs.tsx` (promise-based confirm/prompt/toast), shell, rail folders, link + share dialogs, charts
- `src/routes/` — Links, Analytics, per-link Analytics, Shared with me, Settings
- `src/router.tsx` — route tree
