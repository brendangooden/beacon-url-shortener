import { useNavigate } from "@tanstack/react-router";
import { FolderClosed, Layers, Plus, Share2 } from "lucide-react";
import { useState } from "react";
import { useDialogs } from "@/components/dialogs";
import { ShareDialog } from "@/components/ShareDialog";
import { cn } from "@/lib/cn";
import { canEdit, canManage } from "@/lib/access";
import { useCreateFolder, useDeleteFolder, useFolders, useRenameFolder } from "@/queries/hooks";
import { useWorkspaceStore } from "@/state/workspace";
import type { AccessLevel, Folder } from "@/lib/types";

/** Folder list in the ink-navy rail. Actions gate on each folder's own access.
 *  `nested` renders it indented beneath a workspace row (the workspace name is the header). */
export function RailFolders({
  workspaceId,
  workspaceAccess,
  nested,
}: {
  workspaceId: string;
  workspaceAccess: AccessLevel;
  nested?: boolean;
}) {
  const navigate = useNavigate();
  const dialogs = useDialogs();
  const selected = useWorkspaceStore((s) => s.selectedFolderId);
  const setFolder = useWorkspaceStore((s) => s.setFolder);

  const folders = useFolders(workspaceId);
  const create = useCreateFolder(workspaceId);
  const rename = useRenameFolder(workspaceId);
  const del = useDeleteFolder(workspaceId);

  const [adding, setAdding] = useState(false);
  const [name, setName] = useState("");
  const [sharing, setSharing] = useState<Folder | null>(null);

  const pick = (id: string | null) => {
    setFolder(id);
    void navigate({ to: "/", search: id ? { folder: id } : {} });
  };

  const submitNew = () => {
    const n = name.trim();
    if (!n) return;
    create.mutate(n, {
      onSuccess: () => {
        setName("");
        setAdding(false);
      },
    });
  };

  const list = folders.data ?? [];

  return (
    <div className={nested ? "" : "mt-1"}>
      <div className="mb-1 flex items-center justify-between px-3">
        <span className="text-[11px] font-semibold uppercase tracking-wider text-[var(--sidebar-muted)]">
          {nested ? "" : "Folders"}
        </span>
        {canEdit(workspaceAccess) && (
          <button
            onClick={() => setAdding((v) => !v)}
            aria-label="New folder"
            title="New folder"
            className="text-[var(--sidebar-muted)] transition hover:text-white"
          >
            <Plus className="h-3.5 w-3.5" />
          </button>
        )}
      </div>

      <RailRow icon={<Layers className="h-4 w-4" />} label="All links" active={selected === null} onClick={() => pick(null)} />
      {list.map((f) => (
        <RailRow
          key={f.id}
          icon={<FolderClosed className="h-4 w-4" />}
          label={f.name}
          count={f.linkCount}
          active={selected === f.id}
          onClick={() => pick(f.id)}
          onShare={canManage(f.access) ? () => setSharing(f) : undefined}
          onManage={
            canEdit(f.access)
              ? async () => {
                  const canDelete = canManage(f.access);
                  const newName = await dialogs.prompt({
                    title: "Rename folder",
                    label: "Folder name",
                    defaultValue: f.name,
                    confirmText: "Save",
                    message: canDelete ? 'Type a new name, or "delete" to remove this folder.' : undefined,
                  });
                  if (newName === null) return;
                  const trimmed = newName.trim();
                  if (canDelete && trimmed.toLowerCase() === "delete") {
                    const ok = await dialogs.confirm({
                      title: "Delete folder",
                      message: `Delete "${f.name}"? Its links move to the workspace root.`,
                      tone: "danger",
                      confirmText: "Delete",
                    });
                    if (ok) {
                      del.mutate(f.id);
                      if (selected === f.id) pick(null);
                    }
                  } else if (trimmed && trimmed !== f.name) {
                    rename.mutate({ id: f.id, name: trimmed });
                  }
                }
              : undefined
          }
        />
      ))}

      {adding && (
        <div className="px-2 pt-1">
          <input
            autoFocus
            value={name}
            onChange={(e) => setName(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") submitNew();
              if (e.key === "Escape") setAdding(false);
            }}
            onBlur={submitNew}
            placeholder="Folder name…"
            className="h-8 w-full rounded-md border border-white/10 bg-white/5 px-2 text-sm text-white placeholder:text-[var(--sidebar-muted)] focus:outline-none focus:ring-1 focus:ring-[var(--accent)]"
          />
        </div>
      )}

      {sharing && (
        <ShareDialog
          resourceType="Folder"
          resourceId={sharing.id}
          resourceName={sharing.name}
          open={true}
          onClose={() => setSharing(null)}
        />
      )}
    </div>
  );
}

function RailRow({
  icon,
  label,
  count,
  active,
  onClick,
  onManage,
  onShare,
}: {
  icon: React.ReactNode;
  label: string;
  count?: number;
  active: boolean;
  onClick: () => void;
  onManage?: () => void;
  onShare?: () => void;
}) {
  return (
    <div
      className={cn(
        "group relative mx-2 flex items-center gap-1.5 rounded-md px-2 py-1.5 text-sm transition",
        active ? "beacon-active" : "text-[var(--sidebar-fg)] hover:bg-white/5 hover:text-white",
      )}
    >
      <button className="flex flex-1 items-center gap-2 text-left" onClick={onClick}>
        <span className={active ? "text-[var(--accent)]" : "text-[var(--sidebar-muted)]"}>{icon}</span>
        <span className="flex-1 truncate">{label}</span>
        {count !== undefined && <span className="font-mono text-[11px] text-[var(--sidebar-muted)]">{count}</span>}
      </button>
      {onShare && (
        <button
          onClick={onShare}
          aria-label={`Share ${label}`}
          className="text-[var(--sidebar-muted)] opacity-0 transition group-hover:opacity-100 hover:text-white"
        >
          <Share2 className="h-3.5 w-3.5" />
        </button>
      )}
      {onManage && (
        <button
          onClick={onManage}
          aria-label={`Manage ${label}`}
          className="text-[var(--sidebar-muted)] opacity-0 transition group-hover:opacity-100 hover:text-white"
        >
          ⋯
        </button>
      )}
    </div>
  );
}
