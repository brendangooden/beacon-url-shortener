import { Layers, Search, Star } from "lucide-react";
import { useState } from "react";
import { Button, Dialog, ErrorBanner, Input, Spinner } from "@/components/ui";
import { useAdminWorkspaces } from "@/queries/hooks";

/** Global-Admin only: search + open any workspace in the system, with its owner shown. */
export function BrowseWorkspacesDialog({ onOpen, onClose }: { onOpen: (id: string) => void; onClose: () => void }) {
  const [search, setSearch] = useState("");
  const workspaces = useAdminWorkspaces(search, true);

  return (
    <Dialog
      open
      onClose={onClose}
      title="All workspaces"
      footer={
        <Button variant="secondary" onClick={onClose}>
          Close
        </Button>
      }
    >
      <div className="space-y-3">
        <div className="relative">
          <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--muted)]" />
          <Input
            autoFocus
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search workspaces…"
            className="pl-8"
          />
        </div>

        {workspaces.isLoading ? (
          <div className="flex items-center gap-2 py-6 text-[var(--muted)]">
            <Spinner /> Loading…
          </div>
        ) : workspaces.isError ? (
          <ErrorBanner message="Could not load workspaces." />
        ) : (workspaces.data?.length ?? 0) === 0 ? (
          <p className="py-6 text-sm text-[var(--muted)]">No workspaces match.</p>
        ) : (
          <ul className="max-h-[50vh] divide-y divide-[var(--border)] overflow-y-auto rounded-lg border border-[var(--border)]">
            {workspaces.data!.map((w) => (
              <li key={w.id} className="flex items-center gap-3 px-3 py-2">
                <span className="flex-none text-[var(--muted)]">
                  {w.isPersonal ? <Star className="h-4 w-4 text-[var(--accent)]" /> : <Layers className="h-4 w-4" />}
                </span>
                <div className="min-w-0 flex-1">
                  <div className="truncate text-sm text-[var(--fg)]">
                    {w.isPersonal && w.ownerName ? w.ownerName : w.name}
                    {w.isPersonal && <span className="ml-1.5 text-xs text-[var(--muted)]">personal</span>}
                  </div>
                  <div className="truncate text-xs text-[var(--muted)]">
                    {w.ownerName ? `${w.ownerName}` : "unknown owner"}
                    {w.ownerEmail ? ` · ${w.ownerEmail}` : ""} · {w.linkCount} links · {w.folderCount} folders
                  </div>
                </div>
                <Button variant="secondary" size="sm" onClick={() => onOpen(w.id)}>
                  Open
                </Button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </Dialog>
  );
}
