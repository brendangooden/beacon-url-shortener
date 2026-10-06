import { Link as RouterLink, useNavigate, useSearch } from "@tanstack/react-router";
import { BarChart3, FolderPlus, Link2, Pencil, Plus, QrCode, Search, Share2, Trash2 } from "lucide-react";
import { useEffect, useState } from "react";
import { CodePill } from "@/components/CodePill";
import { useDialogs } from "@/components/dialogs";
import { LinkFormDialog } from "@/components/LinkFormDialog";
import { PageHeader } from "@/components/PageHeader";
import { QrDialog } from "@/components/QrDialog";
import { ShareDialog } from "@/components/ShareDialog";
import { Badge, Button, Card, ErrorBanner, Input, Spinner, Switch } from "@/components/ui";
import { useSelectedWorkspace } from "@/hooks/useSelectedWorkspace";
import { useCreateFolder, useDeleteLink, useFolders, useLinks, useSetLinkActive } from "@/queries/hooks";
import { useWorkspaceStore } from "@/state/workspace";
import { canEdit, canManage } from "@/lib/access";
import { ApiError } from "@/lib/api";
import type { Link, ResourceType } from "@/lib/types";

interface ShareTarget {
  type: ResourceType;
  id: string;
  name: string;
}

export function LinksPage() {
  const workspace = useSelectedWorkspace();
  const workspaceId = workspace?.id ?? "";
  const editable = canEdit(workspace?.access);
  const manageable = canManage(workspace?.access);
  const setFolder = useWorkspaceStore((s) => s.setFolder);
  const navigate = useNavigate();
  const dialogs = useDialogs();
  // The URL is the source of truth for the selected folder (so a folder view is hotlinkable).
  const routeSearch = useSearch({ strict: false }) as { folder?: string };
  const folderId = routeSearch.folder ?? null;
  useEffect(() => {
    setFolder(folderId);
  }, [folderId, setFolder]);

  const [search, setSearch] = useState("");
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<Link | null>(null);
  const [share, setShare] = useState<ShareTarget | null>(null);
  const [qrLink, setQrLink] = useState<Link | null>(null);

  const folders = useFolders(workspaceId);
  const links = useLinks(workspaceId, folderId, search);
  const setActive = useSetLinkActive(workspaceId);
  const del = useDeleteLink(workspaceId);
  const createFolder = useCreateFolder(workspaceId);

  const folderName = folderId ? folders.data?.find((f) => f.id === folderId)?.name : null;

  const openNew = () => {
    setEditing(null);
    setDialogOpen(true);
  };
  const openEdit = (link: Link) => {
    setEditing(link);
    setDialogOpen(true);
  };
  const toggleActive = async (link: Link) => {
    // Disabling breaks the live short link (visitors hit the branded 404), so confirm it first.
    if (link.isActive) {
      const ok = await dialogs.confirm({
        title: "Disable link",
        message: `Disable /${link.code}? The short link stops working — visitors see the “not found” page until you re-enable it.`,
        tone: "danger",
        confirmText: "Disable",
      });
      if (!ok) return;
    }
    setActive.mutate(
      { id: link.id, isActive: !link.isActive },
      { onError: (e) => dialogs.toast({ message: e instanceof ApiError ? e.message : "Could not update the link", tone: "error" }) },
    );
  };
  const newFolder = async () => {
    const name = await dialogs.prompt({
      title: "New folder",
      label: "Folder name",
      placeholder: "e.g. Campaigns",
      required: true,
      confirmText: "Create",
    });
    if (!name) return;
    createFolder.mutate(name, {
      onSuccess: (folder) => void navigate({ to: "/", search: { folder: folder.id } }),
      onError: (e) => dialogs.toast({ message: e instanceof ApiError ? e.message : "Could not create folder", tone: "error" }),
    });
  };

  return (
    <div>
      <PageHeader
        title={folderName ?? "Links"}
        actions={
          <>
            {manageable && workspace && (
              <Button
                variant="secondary"
                onClick={() => setShare({ type: "Workspace", id: workspace.id, name: workspace.name })}
              >
                <Share2 className="h-4 w-4" /> Share
              </Button>
            )}
            {editable && (
              <Button variant="secondary" onClick={newFolder}>
                <FolderPlus className="h-4 w-4" /> New folder
              </Button>
            )}
            {editable && (
              <Button onClick={openNew}>
                <Plus className="h-4 w-4" /> Create link
              </Button>
            )}
          </>
        }
      />

      <div className="mb-4 flex items-center gap-2">
        <div className="relative max-w-xs flex-1">
          <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--muted)]" />
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search code, destination, title…"
            className="pl-8"
          />
        </div>
      </div>

      {links.isLoading ? (
        <div className="flex items-center gap-2 text-[var(--muted)]">
          <Spinner /> Loading links…
        </div>
      ) : links.isError ? (
        <ErrorBanner message="Could not load links." />
      ) : (links.data?.length ?? 0) === 0 ? (
        <Card className="p-10 text-center">
          <Link2 className="mx-auto mb-3 h-8 w-8 text-[var(--muted)]/50" />
          <p className="text-[var(--muted)]">No links here yet.</p>
          {editable && (
            <Button className="mt-4" onClick={openNew}>
              <Plus className="h-4 w-4" /> Create link
            </Button>
          )}
        </Card>
      ) : (
        <Card className="overflow-hidden">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-[var(--border)] text-left text-xs font-medium text-[var(--muted)]">
                <th className="px-4 py-2.5">Short link</th>
                <th className="px-4 py-2.5">Destination</th>
                <th className="px-4 py-2.5">Tags</th>
                <th className="px-4 py-2.5 text-right">Clicks</th>
                <th className="px-4 py-2.5 text-center">Active</th>
                <th className="px-4 py-2.5" />
              </tr>
            </thead>
            <tbody>
              {links.data!.map((link) => (
                <tr key={link.id} className="group border-b border-[var(--border)] last:border-0 hover:bg-[var(--surface-2)]">
                  <td className="px-4 py-2.5">
                    <CodePill code={link.code} shortUrl={link.shortUrl} />
                    {link.title && <div className="mt-0.5 text-xs text-[var(--muted)]">{link.title}</div>}
                  </td>
                  <td className="max-w-xs px-4 py-2.5">
                    <a
                      href={link.destination}
                      target="_blank"
                      rel="noreferrer"
                      className="block truncate text-[var(--muted)] hover:text-[var(--teal)] hover:underline"
                      title={link.destination}
                    >
                      {link.destination}
                    </a>
                  </td>
                  <td className="px-4 py-2.5">
                    <div className="flex flex-wrap gap-1">
                      {link.tags.map((t) => (
                        <Badge key={t}>{t}</Badge>
                      ))}
                    </div>
                  </td>
                  <td className="px-4 py-2.5 text-right font-mono text-[13px] text-[var(--fg)]">{link.clickCount}</td>
                  <td className="px-4 py-2.5">
                    <div className="flex justify-center">
                      <Switch
                        checked={link.isActive}
                        disabled={!editable}
                        onChange={() => void toggleActive(link)}
                        label={link.isActive ? "Disable link" : "Enable link"}
                      />
                    </div>
                  </td>
                  <td className="px-4 py-2.5">
                    <div className="flex justify-end gap-0.5 opacity-60 transition group-hover:opacity-100">
                      <Button variant="ghost" size="icon" aria-label="QR code" onClick={() => setQrLink(link)}>
                        <QrCode className="h-4 w-4" />
                      </Button>
                      <RouterLink to="/links/$linkId" params={{ linkId: link.id }}>
                        <Button variant="ghost" size="icon" aria-label="Analytics">
                          <BarChart3 className="h-4 w-4" />
                        </Button>
                      </RouterLink>
                      {editable && (
                        <Button variant="ghost" size="icon" aria-label="Edit" onClick={() => openEdit(link)}>
                          <Pencil className="h-4 w-4" />
                        </Button>
                      )}
                      {manageable && (
                        <>
                          <Button
                            variant="ghost"
                            size="icon"
                            aria-label="Share"
                            onClick={() => setShare({ type: "Link", id: link.id, name: `/${link.code}` })}
                          >
                            <Share2 className="h-4 w-4" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon"
                            aria-label="Delete"
                            onClick={async () => {
                              const ok = await dialogs.confirm({
                                title: "Delete link",
                                message: `Delete /${link.code}? It stops working and moves to the trash — a Global Admin can restore it.`,
                                tone: "danger",
                                confirmText: "Delete",
                              });
                              if (ok) {
                                del.mutate(link.id, {
                                  onError: (e) =>
                                    dialogs.toast({ message: e instanceof ApiError ? e.message : "Delete failed", tone: "error" }),
                                });
                              }
                            }}
                          >
                            <Trash2 className="h-4 w-4 text-[var(--danger)]" />
                          </Button>
                        </>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}

      {dialogOpen && (
        <LinkFormDialog
          workspaceId={workspaceId}
          folders={folders.data ?? []}
          existing={editing}
          defaultFolderId={folderId}
          open={dialogOpen}
          onClose={() => setDialogOpen(false)}
        />
      )}

      {share && (
        <ShareDialog
          resourceType={share.type}
          resourceId={share.id}
          resourceName={share.name}
          open={true}
          onClose={() => setShare(null)}
        />
      )}

      {qrLink && <QrDialog link={qrLink} open onClose={() => setQrLink(null)} />}
    </div>
  );
}
