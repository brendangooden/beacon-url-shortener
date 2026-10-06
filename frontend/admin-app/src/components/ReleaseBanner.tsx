import { RefreshCw, X } from "lucide-react";
import { useState } from "react";
import { useNewRelease } from "@/lib/version";

/**
 * Non-blocking prompt shown when a newer build has been deployed while this tab stayed open.
 * Mounted above the router so it appears on every screen. Reload is the user's choice — we never
 * force it (nothing here needs the auto-reload machinery a CDN-fronted app would).
 */
export function ReleaseBanner() {
  const latest = useNewRelease();
  const [dismissed, setDismissed] = useState<string | null>(null);

  // Show again if a *different* newer version ships after a dismiss.
  if (!latest || latest === dismissed) return null;

  return (
    <div className="fixed inset-x-0 bottom-4 z-[60] flex justify-center px-4">
      <div className="flex items-center gap-3 rounded-xl border border-[var(--border)] bg-[var(--surface)] px-4 py-2.5 shadow-lg">
        <span className="text-sm text-[var(--fg)]">A new version is available.</span>
        <button
          onClick={() => window.location.reload()}
          className="inline-flex items-center gap-1.5 rounded-lg bg-[var(--accent)] px-3 py-1.5 text-sm font-medium text-[var(--accent-fg)] transition hover:brightness-95"
        >
          <RefreshCw className="h-3.5 w-3.5" /> Reload
        </button>
        <button
          onClick={() => setDismissed(latest)}
          aria-label="Dismiss"
          className="rounded p-1 text-[var(--muted)] transition hover:bg-[var(--surface-2)] hover:text-[var(--fg)]"
        >
          <X className="h-4 w-4" />
        </button>
      </div>
    </div>
  );
}
