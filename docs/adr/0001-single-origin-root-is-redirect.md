# Single origin, root path is the redirect

The whole product runs on one origin (for example `beacon.example.com`). The root path `/{code}` is the public 302 redirect — the shortest possible short URL, which is the point of the tool. The management API sits under `/api/*` (Entra-authenticated) and the React admin SPA under `/admin`.

## Consequences

- **Reserved prefixes.** `api`, `admin`, `health`, `assets` (and any other real route) must be excluded from ShortCode generation and rejected as vanity codes, or a code could shadow a real route. This exclusion list is load-bearing — treat it as append-only.
- A subdomain split for the SPA (`admin-beacon...`) was rejected: it adds a second route + CORS for no POC benefit while the origin is shared anyway.
- Putting the SPA at `/` with redirects under `/r/{code}` was rejected — it throws away the short URL, defeating the tool.
