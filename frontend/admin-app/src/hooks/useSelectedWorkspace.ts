import { useWorkspace, useWorkspaces } from "@/queries/hooks";
import { useWorkspaceStore } from "@/state/workspace";
import type { WorkspaceSummary } from "@/lib/types";

/**
 * The currently selected workspace. Normally one of the user's own/shared workspaces; if a Global
 * Admin has drilled into another user's workspace (not in that list), it's fetched by id instead.
 */
export function useSelectedWorkspace(): WorkspaceSummary | undefined {
  const id = useWorkspaceStore((s) => s.selectedWorkspaceId);
  const { data } = useWorkspaces();
  const list = data ?? [];
  const inList = list.find((w) => w.id === id);

  // Only fetch a single workspace when the selected id isn't already in the sidebar list.
  const external = useWorkspace(id && !inList ? id : undefined);
  const fetched: WorkspaceSummary | undefined = external.data
    ? {
        id: external.data.id,
        name: external.data.name,
        isPersonal: external.data.isPersonal,
        access: external.data.access,
        isOwner: external.data.isOwner,
        linkCount: external.data.linkCount,
        ownerName: external.data.ownerName,
        ownerEmail: external.data.ownerEmail,
      }
    : undefined;

  return inList ?? fetched ?? list[0];
}
