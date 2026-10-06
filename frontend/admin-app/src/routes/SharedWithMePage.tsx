import { useNavigate } from "@tanstack/react-router";
import { ArrowUpRight, FolderClosed, Layers, Link2 } from "lucide-react";
import { PageHeader } from "@/components/PageHeader";
import { Badge, Button, Card, Spinner } from "@/components/ui";
import { useSharedWithMe } from "@/queries/hooks";
import { useWorkspaceStore } from "@/state/workspace";
import type { ResourceType, SharedWithMe } from "@/lib/types";

const ICONS: Record<ResourceType, React.ReactNode> = {
  Workspace: <Layers className="h-4 w-4 text-[var(--muted)]" />,
  Folder: <FolderClosed className="h-4 w-4 text-[var(--muted)]" />,
  Link: <Link2 className="h-4 w-4 text-[var(--muted)]" />,
};

export function SharedWithMePage() {
  const shared = useSharedWithMe();
  const navigate = useNavigate();
  const setWorkspace = useWorkspaceStore((s) => s.setWorkspace);
  const setFolder = useWorkspaceStore((s) => s.setFolder);

  const open = (item: SharedWithMe) => {
    if (item.resourceType === "Link") {
      // The link may live in a workspace we don't otherwise have — pass it via ?ws.
      void navigate({
        to: "/links/$linkId",
        params: { linkId: item.resourceId },
        search: item.workspaceId ? { ws: item.workspaceId } : {},
      });
      return;
    }

    if (item.resourceType === "Folder") {
      if (item.workspaceId) setWorkspace(item.workspaceId);
      setFolder(item.resourceId);
      void navigate({ to: "/" });
      return;
    }

    // Workspace
    setWorkspace(item.resourceId);
    setFolder(null);
    void navigate({ to: "/" });
  };

  return (
    <div>
      <PageHeader title="Shared with me" subtitle="Resources other people have shared with you" />

      {shared.isLoading ? (
        <div className="flex items-center gap-2 text-[var(--muted)]">
          <Spinner /> Loading…
        </div>
      ) : (shared.data?.length ?? 0) === 0 ? (
        <Card className="p-10 text-center text-[var(--muted)]">Nothing has been shared with you yet.</Card>
      ) : (
        <Card className="overflow-hidden">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-[var(--border)] text-left text-xs font-medium text-[var(--muted)]">
                <th className="px-4 py-2.5">Name</th>
                <th className="px-4 py-2.5">Type</th>
                <th className="px-4 py-2.5">Access</th>
                <th className="px-4 py-2.5" />
              </tr>
            </thead>
            <tbody>
              {shared.data!.map((item) => (
                <tr
                  key={`${item.resourceType}:${item.resourceId}`}
                  className="border-b border-[var(--border)] last:border-0 hover:bg-[var(--surface-2)]"
                >
                  <td className="px-4 py-2.5">
                    <div className="flex items-center gap-2">
                      {ICONS[item.resourceType]}
                      <div className="min-w-0">
                        <div
                          className={
                            item.resourceType === "Link"
                              ? "font-mono text-[13px] text-[var(--teal)]"
                              : "text-[var(--fg)]"
                          }
                        >
                          {item.resourceType === "Link" ? `/${item.name}` : item.name}
                        </div>
                        {item.detail && (
                          <div className="max-w-md truncate text-xs text-[var(--muted)]" title={item.detail}>
                            {item.detail}
                          </div>
                        )}
                      </div>
                    </div>
                  </td>
                  <td className="px-4 py-2.5 text-[var(--muted)]">{item.resourceType}</td>
                  <td className="px-4 py-2.5">
                    <Badge tone="amber">{item.level}</Badge>
                  </td>
                  <td className="px-4 py-2.5 text-right">
                    <Button variant="ghost" size="sm" onClick={() => open(item)}>
                      Open <ArrowUpRight className="h-3.5 w-3.5" />
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}
    </div>
  );
}
