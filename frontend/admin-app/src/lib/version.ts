import { useEffect, useState } from "react";

/** The build stamp this bundle was compiled with (git SHA in deployed builds). */
export const APP_VERSION = __APP_VERSION__;

const POLL_MS = 60_000;
const VERSION_URL = `${import.meta.env.BASE_URL}version.json`;

/**
 * Detect that a newer build has been deployed while this tab stayed open.
 *
 * Our SPA sits behind nginx/Traefik with no document-caching CDN, so `index.html` is served
 * `no-cache` and a fresh load always gets the latest bundle — the only gap is a long-lived tab that
 * never reloads. This polls version.json (served `no-store`) and reports the deployed version when
 * it differs from ours. It always reports the *currently* deployed version and clears on a match
 * (e.g. a rollback) — it never latches onto the first change it sees.
 *
 * Returns the newer version string, or null when we are up to date (or in dev, where there is no
 * version.json). A plain `location.reload()` is enough to adopt it here; no cache-key trickery is
 * needed because nothing caches the document.
 */
export function useNewRelease(): string | null {
  const [latest, setLatest] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    const check = async () => {
      try {
        const res = await fetch(VERSION_URL, { cache: "no-store" });
        if (!res.ok || cancelled) return;
        const data = (await res.json()) as { version?: string };
        if (cancelled) return;
        setLatest(data.version && data.version !== APP_VERSION ? data.version : null);
      } catch {
        // Offline / transient / no version.json in dev — leave the current state untouched.
      }
    };

    void check();
    const timer = setInterval(check, POLL_MS);
    const onFocus = () => void check();
    window.addEventListener("focus", onFocus);
    document.addEventListener("visibilitychange", onFocus);

    return () => {
      cancelled = true;
      clearInterval(timer);
      window.removeEventListener("focus", onFocus);
      document.removeEventListener("visibilitychange", onFocus);
    };
  }, []);

  return latest;
}
