import { useNavigate } from "@tanstack/react-router";
import { Settings2, Share2 } from "lucide-react";
import { useState } from "react";
import { useDialogs } from "@/components/dialogs";
import { ShareDialog } from "@/components/ShareDialog";
import { canManage } from "@/lib/access";
import { ApiError } from "@/lib/api";
import { workspaceLabel } from "@/lib/workspace";
import { useDeleteWorkspace, useRenameWorkspace, useWorkspaces } from "@/queries/hooks";
import { useWorkspaceStore } from "@/state/workspace";
import type { WorkspaceSummary } from "@/lib/types";

/** The current workspace's name + owner in the folder sidebar, with share / rename / delete. */
export function WorkspaceHeader({ workspace }: { workspace: WorkspaceSummary }) {
  const dialogs = useDialogs();
  const navigate = useNavigate();
  const rename = useRenameWorkspace();
  const del = useDeleteWorkspace();
  const workspaces = useWorkspaces();
  const setWorkspace = useWorkspaceStore((s) => s.setWorkspace);
  const [sharing, setSharing] = useState(false);

  const manageable = canManage(workspace.access);

  const manage = async () => {
    const newName = await dialogs.prompt({
      title: "Rename workspace",
      label: "Workspace name",
      defaultValue: workspace.name,
      confirmText: "Save",
      message: 'Type a new name, or "delete" to remove this workspace and everything in it.',
    });
    if (newName === null) return;
    const trimmed = newName.trim();
    if (trimmed.toLowerCase() === "delete") {
      const ok = await dialogs.confirm({
        title: "Delete workspace",
        message: `Delete "${workspace.name}"? Its folders and links are permanently removed. This can't be undone.`,
        tone: "danger",
        confirmText: "Delete",
      });
      if (!ok) return;
      del.mutate(workspace.id, {
        onSuccess: () => {
          const next = (workspaces.data ?? []).find((w) => w.id !== workspace.id)?.id ?? "";
          setWorkspace(next);
          void navigate({ to: "/", search: {} });
        },
        onError: (e) => dialogs.toast({ message: e instanceof ApiError ? e.message : "Delete failed", tone: "error" }),
      });
    } else if (trimmed && trimmed !== workspace.name) {
      rename.mutate({ id: workspace.id, name: trimmed });
    }
  };

  return (
    <div className="group flex items-start gap-2 px-4 pt-3.5 pb-2.5">
      <div className="min-w-0 flex-1">
        <div className="truncate font-display text-[17px] font-semibold leading-tight text-white">{workspaceLabel(workspace)}</div>
        <div className="mt-0.5 truncate text-xs text-[var(--sidebar-muted)]">
          {workspace.isOwner ? "Your workspace" : `Owned by ${workspace.ownerName ?? "someone"}`}
        </div>
      </div>
      {manageable && (
        <div className="flex flex-none items-center opacity-0 transition group-hover:opacity-100">
          <button onClick={() => setSharing(true)} aria-label="Share workspace" title="Share workspace" className="rounded p-1 text-[var(--sidebar-muted)] hover:bg-white/10 hover:text-white">
            <Share2 className="h-4 w-4" />
          </button>
          {!workspace.isPersonal && (
            <button onClick={manage} aria-label="Rename or delete workspace" title="Rename or delete" className="rounded p-1 text-[var(--sidebar-muted)] hover:bg-white/10 hover:text-white">
              <Settings2 className="h-4 w-4" />
            </button>
          )}
        </div>
      )}
      {sharing && (
        <ShareDialog resourceType="Workspace" resourceId={workspace.id} resourceName={workspaceLabel(workspace)} open onClose={() => setSharing(false)} />
      )}
    </div>
  );
}
