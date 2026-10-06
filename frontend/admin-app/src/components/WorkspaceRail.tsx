import { useNavigate } from "@tanstack/react-router";
import { LayoutGrid, Plus, Star } from "lucide-react";
import { useState } from "react";
import { BrowseWorkspacesDialog } from "@/components/BrowseWorkspacesDialog";
import { useDialogs } from "@/components/dialogs";
import { useSelectedWorkspace } from "@/hooks/useSelectedWorkspace";
import { ApiError } from "@/lib/api";
import { cn } from "@/lib/cn";
import { workspaceColor, workspaceInitial, workspaceLabel } from "@/lib/workspace";
import { useCreateWorkspace, useWorkspaces } from "@/queries/hooks";
import { useWorkspaceStore } from "@/state/workspace";
import type { WorkspaceSummary } from "@/lib/types";

/** The Beacon signal mark, matching the favicon. */
function BeaconMark() {
  return (
    <div className="grid h-10 w-10 flex-none place-items-center rounded-xl bg-gradient-to-br from-[#f0b73a] to-[var(--accent)] text-[#1c1400] shadow-[0_4px_14px_rgba(232,163,23,.28)]">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" className="h-[22px] w-[22px]">
        <path d="M12 2v4" /><path d="m6.3 7.8 2.9 2.9" /><path d="M2 13h4" /><path d="M22 13h-4" /><path d="m17.7 7.8-2.9 2.9" />
        <path d="M12 22a6 6 0 0 0 6-6c0-3-2.7-5.4-6-9-3.3 3.6-6 6-6 9a6 6 0 0 0 6 6Z" />
      </svg>
    </div>
  );
}

/** Far-left workspace switcher: one coloured tile per workspace. Owners are legible at a glance. */
export function WorkspaceRail({ isGlobalAdmin }: { isGlobalAdmin: boolean }) {
  const navigate = useNavigate();
  const dialogs = useDialogs();
  const setWorkspace = useWorkspaceStore((s) => s.setWorkspace);
  const workspaces = useWorkspaces();
  const selected = useSelectedWorkspace();
  const create = useCreateWorkspace();
  const [browseOpen, setBrowseOpen] = useState(false);

  const list = workspaces.data ?? [];
  // A Global Admin who drilled into someone else's workspace: show it as an extra tile.
  const external = selected && !list.some((w) => w.id === selected.id) ? selected : null;
  const tiles = external ? [...list, external] : list;

  const pick = (id: string) => {
    setWorkspace(id);
    void navigate({ to: "/", search: {} });
  };

  const newWorkspace = async () => {
    const name = await dialogs.prompt({
      title: "New workspace",
      label: "Workspace name",
      placeholder: "e.g. Marketing",
      required: true,
      confirmText: "Create",
    });
    if (!name) return;
    create.mutate(name, {
      onSuccess: (w) => pick(w.id),
      onError: (e) => dialogs.toast({ message: e instanceof ApiError ? e.message : "Could not create workspace", tone: "error" }),
    });
  };

  return (
    <nav className="flex w-[68px] flex-none flex-col items-center gap-2.5 border-r border-white/[.07] bg-[var(--rail)] py-3.5">
      <BeaconMark />
      <span className="h-px w-6 bg-white/[.13]" />

      <div className="flex w-full flex-1 flex-col items-center gap-2.5 overflow-y-auto [scrollbar-width:none]">
        {tiles.map((w) => (
          <Tile key={w.id} w={w} active={w.id === selected?.id} onClick={() => pick(w.id)} viewing={external?.id === w.id} />
        ))}
      </div>

      <RailButton label="New workspace" onClick={newWorkspace}>
        <Plus className="h-[18px] w-[18px]" />
      </RailButton>
      {isGlobalAdmin && (
        <RailButton label="Browse all workspaces" sub="Global Admin" solid onClick={() => setBrowseOpen(true)}>
          <LayoutGrid className="h-[18px] w-[18px]" />
        </RailButton>
      )}

      {browseOpen && (
        <BrowseWorkspacesDialog
          onOpen={(id) => {
            pick(id);
            setBrowseOpen(false);
          }}
          onClose={() => setBrowseOpen(false)}
        />
      )}
    </nav>
  );
}

function Tile({ w, active, onClick, viewing }: { w: WorkspaceSummary; active: boolean; onClick: () => void; viewing: boolean }) {
  const label = workspaceLabel(w);
  return (
    <button onClick={onClick} className="group relative grid h-11 w-11 flex-none place-items-center">
      {/* active amber bar */}
      {active && <span className="absolute -left-[14px] top-1/2 h-[22px] w-1 -translate-y-1/2 rounded-r bg-[var(--accent)]" />}
      {/* ring on active */}
      <span className={cn("pointer-events-none absolute -inset-1 rounded-2xl border-2 transition-colors", active ? "border-[var(--accent)]" : "border-transparent")} />
      <span
        className={cn(
          "grid h-11 w-11 place-items-center rounded-[13px] font-display text-[15px] font-semibold text-white transition group-hover:-translate-y-px",
          w.isPersonal && "bg-[var(--surface-2)] text-[var(--accent)] ring-1 ring-white/[.13]",
        )}
        style={w.isPersonal ? undefined : { backgroundColor: workspaceColor(w.id) }}
      >
        {w.isPersonal ? <Star className="h-[18px] w-[18px]" fill="currentColor" stroke="none" /> : workspaceInitial(label)}
      </span>
      <Tip title={label} sub={viewing ? `Viewing · owned by ${w.ownerName}` : w.isOwner ? "Your workspace" : `Owned by ${w.ownerName}`} />
    </button>
  );
}

function RailButton({ label, sub, solid, onClick, children }: { label: string; sub?: string; solid?: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      onClick={onClick}
      className={cn(
        "group relative grid h-11 w-11 flex-none place-items-center rounded-[13px] text-[var(--sidebar-muted)] transition hover:border-[var(--accent)] hover:bg-[var(--surface)] hover:text-white",
        solid ? "border border-white/[.13]" : "border border-dashed border-white/[.13]",
      )}
    >
      {children}
      <Tip title={label} sub={sub} />
    </button>
  );
}

function Tip({ title, sub }: { title: string; sub?: string }) {
  return (
    <span className="pointer-events-none absolute left-14 top-1/2 z-40 -translate-x-1 -translate-y-1/2 whitespace-nowrap rounded-lg border border-white/[.13] bg-[#05080f] px-2.5 py-1.5 opacity-0 shadow-[0_8px_24px_rgba(0,0,0,.4)] transition group-hover:translate-x-0 group-hover:opacity-100">
      <span className="block font-display text-[13px] font-semibold text-white">{title}</span>
      {sub && <span className="block text-[11.5px] text-[var(--sidebar-muted)]">{sub}</span>}
    </span>
  );
}
