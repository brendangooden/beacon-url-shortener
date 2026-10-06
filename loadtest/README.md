# Load tests

k6 load tests for the redirect hot path, plus a local prod-like stack (Traefik → API → Postgres +
Redis).

## Layout

| Path | What |
|---|---|
| `local/compose.yml` | Local prod-like stack: **Traefik → API → Postgres + Redis** (HTTP-only). |
| `local/traefik-dynamic.yml` | Traefik file-provider route (Docker socket negotiation fails with Engine 29 on Windows). |
| `k6/redirect-load.js` | Ramp 100→4000 rps against a local stack, with mid-test update/disable/delete. |
| `k6/prod-probe.js` | Parametric constant-rate probe (`RATE`, `DURATION`, `BASE`) for a deployed instance. |

## Run locally (find the real server ceiling)

```bash
# 1. build the API image the compose references
docker build -f docker/Dockerfile.api -t beacon-api:load .
# 2. bring up the stack
docker compose -f loadtest/local/compose.yml -p lnkload up -d
# 3. ramping load through Traefik (Host header mimics prod)
k6 run loadtest/k6/redirect-load.js
# 4. tear down
docker compose -f loadtest/local/compose.yml -p lnkload down -v
```

`redirect-load.js` creates 10 links in `setup()`, ramps redirect RPS, and mid-test updates two
codes, disables one, and deletes one — so you can watch cache invalidation under load.

## Run a metered probe against a deployed instance

Start small and step the rate up while watching the target host's CPU.

```bash
# constant-rate probe (creates probe0-9, deletes them in teardown)
BASE=https://links.example.com RATE=50 DURATION=40s k6 run loadtest/k6/prod-probe.js
```

Step the rate up (50 → 300 → 800 …) only while the host stays comfortable.

## Example findings

Indicative numbers from one run, to show the shape (your hardware will differ):

- **Local (in-datacenter path):** ~4,000 rps sustained, p50 ~2 ms, p99 ~43 ms, 0 errors — CPU-bound
  (API ~2.2 cores, reverse proxy ~2.0 cores). Cache invalidation under load was clean.
- **Over the internet:** 50 / 300 / 750 rps all 100% 302, 0 errors; median held ~23 ms (the network
  floor). At ~750 rps the app used ~1.3 of 4 cores — the server was not the bottleneck; the tail was
  client/network-bound.
- **Takeaway:** the redirect path (FusionCache L1 hit) is very cheap and memory is a non-issue; the
  true server ceiling needs a load generator in the same region to remove the network floor.
