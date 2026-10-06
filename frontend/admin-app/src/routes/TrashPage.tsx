import { RotateCcw, Trash2 } from "lucide-react";
import { CodePill } from "@/components/CodePill";
import { useDialogs } from "@/components/dialogs";
import { PageHeader } from "@/components/PageHeader";
import { useDeletedLinks, useMe, usePurgeLink, useRestoreLink } from "@/queries/hooks";
import { Button, Card, ErrorBanner, Spinner } from "@/components/ui";
import { ApiError } from "@/lib/api";

function timeAgo(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleDateString(undefined, { month: "short", day: "numeric" });
}

/** Global-Admin only: soft-deleted links, restorable or purgeable. */
export function TrashPage() {
  const me = useMe();
  const isGlobalAdmin = me.data?.isGlobalAdmin ?? false;
  const dialogs = useDialogs();
  const links = useDeletedLinks(isGlobalAdmin);
  const restore = useRestoreLink();
  const purge = usePurgeLink();

  if (me.isSuccess && !isGlobalAdmin) {
    return (
      <div>
        <PageHeader title="Trash" subtitle="Deleted links" />
        <ErrorBanner message="This view is for Global Admins only." />
      </div>
    );
  }

  const onRestore = (id: string) =>
    restore.mutate(id, {
      onSuccess: () => dialogs.toast({ message: "Link restored.", tone: "success" }),
      onError: (e) => dialogs.toast({ message: e instanceof ApiError ? e.message : "Restore failed", tone: "error" }),
    });

  const onPurge = async (code: string, id: string) => {
    const ok = await dialogs.confirm({
      title: "Delete forever",
      message: `Permanently delete /${code}? Its clicks and history go too. This can't be undone.`,
      tone: "danger",
      confirmText: "Delete forever",
    });
    if (!ok) return;
    purge.mutate(id, {
      onError: (e) => dialogs.toast({ message: e instanceof ApiError ? e.message : "Delete failed", tone: "error" }),
    });
  };

  return (
    <div>
      <PageHeader title="Trash" subtitle="Deleted links across all workspaces — restore or delete for good" />

      {links.isLoading ? (
        <div className="flex items-center gap-2 text-[var(--muted)]">
          <Spinner /> Loading…
        </div>
      ) : links.isError ? (
        <ErrorBanner message="Could not load the trash." />
      ) : (links.data?.length ?? 0) === 0 ? (
        <Card className="p-10 text-center">
          <Trash2 className="mx-auto mb-3 h-8 w-8 text-[var(--muted)]/50" />
          <p className="text-[var(--muted)]">Trash is empty.</p>
        </Card>
      ) : (
        <Card className="overflow-hidden">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-[var(--border)] text-left text-xs font-medium text-[var(--muted)]">
                <th className="px-4 py-2.5">Short link</th>
                <th className="px-4 py-2.5">Workspace</th>
                <th className="px-4 py-2.5">Deleted</th>
                <th className="px-4 py-2.5 text-right">Clicks</th>
                <th className="px-4 py-2.5 text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {links.data!.map((l) => (
                <tr key={l.id} className="border-b border-[var(--border)] last:border-0 hover:bg-[var(--surface-2)]">
                  <td className="px-4 py-2.5">
                    <CodePill code={l.code} shortUrl={l.shortUrl} />
                    {l.title && <div className="mt-0.5 text-xs text-[var(--muted)]">{l.title}</div>}
                    <div className="mt-0.5 max-w-xs truncate text-xs text-[var(--muted)]/70" title={l.destination}>{l.destination}</div>
                  </td>
                  <td className="px-4 py-2.5">
                    <div className="text-[var(--fg)]">{l.workspaceName}</div>
                    {l.ownerName && <div className="text-xs text-[var(--muted)]">{l.ownerName}</div>}
                  </td>
                  <td className="px-4 py-2.5 text-[var(--muted)]">
                    {timeAgo(l.deletedOnUtc)}
                    {l.deletedByName && <div className="text-xs">by {l.deletedByName}</div>}
                  </td>
                  <td className="px-4 py-2.5 text-right font-mono text-[13px] text-[var(--fg)]">{l.clickCount}</td>
                  <td className="px-4 py-2.5">
                    <div className="flex justify-end gap-2">
                      <Button variant="secondary" size="sm" onClick={() => onRestore(l.id)} disabled={restore.isPending}>
                        <RotateCcw className="h-3.5 w-3.5" /> Restore
                      </Button>
                      <Button variant="ghost" size="icon" aria-label="Delete forever" onClick={() => void onPurge(l.code, l.id)}>
                        <Trash2 className="h-4 w-4 text-[var(--danger)]" />
                      </Button>
                    </div>
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
