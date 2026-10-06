import { useRouterState } from "@tanstack/react-router";
import { useSelectedWorkspace } from "@/hooks/useSelectedWorkspace";
import { useFolders } from "@/queries/hooks";
import { useWorkspaceStore } from "@/state/workspace";
import { workspaceLabel } from "@/lib/workspace";

const SECTION: Record<string, string> = {
  "/analytics": "Analytics",
  "/shared": "Shared with me",
  "/all-links": "All links",
  "/settings": "Settings",
};

/** Top-bar "where am I": Workspace / section (or folder on the Links page). */
export function Breadcrumb() {
  const path = useRouterState({ select: (s) => s.location.pathname });
  const selected = useSelectedWorkspace();
  const folderId = useWorkspaceStore((s) => s.selectedFolderId);
  const folders = useFolders(selected?.id ?? "");

  const wsName = selected ? workspaceLabel(selected) : "…";

  let section: string;
  if (path === "/") {
    section = folderId ? folders.data?.find((f) => f.id === folderId)?.name ?? "Links" : "All links";
  } else if (path.startsWith("/links/")) {
    section = "Link analytics";
  } else {
    section = SECTION[path] ?? "Links";
  }

  return (
    <div className="flex min-w-0 items-center gap-2 font-display text-[15px] font-semibold">
      <span className="truncate text-[var(--fg)]">{wsName}</span>
      <span className="text-[var(--muted)]">/</span>
      <span className="truncate font-medium text-[var(--muted)]">{section}</span>
    </div>
  );
}
