import { Link, Outlet, useRouterState } from "@tanstack/react-router";
import { BarChart3, Globe, Inbox, LogOut, Menu, Settings, Trash2 } from "lucide-react";
import { useEffect, useState, type ReactNode } from "react";
import { Breadcrumb } from "@/components/Breadcrumb";
import { RailFolders } from "@/components/RailFolders";
import { WorkspaceHeader } from "@/components/WorkspaceHeader";
import { WorkspaceRail } from "@/components/WorkspaceRail";
import { Spinner } from "@/components/ui";
import { useApplyBranding } from "@/branding/useApplyBranding";
import { isEntraConfigured, signOut } from "@/auth/entra";
import { useSelectedWorkspace } from "@/hooks/useSelectedWorkspace";
import { useMe, useWorkspaces } from "@/queries/hooks";
import { useWorkspaceStore } from "@/state/workspace";
import { rootUrl } from "@/lib/api";
import { cn } from "@/lib/cn";

export function AppShell() {
  const branding = useApplyBranding();
  const me = useMe();
  const workspaces = useWorkspaces();
  const { selectedWorkspaceId, setWorkspace } = useWorkspaceStore();
  const [navOpen, setNavOpen] = useState(false);

  const path = useRouterState({ select: (s) => s.location.pathname });
  const list = workspaces.data ?? [];
  const selected = useSelectedWorkspace();
  const isGlobalAdmin = me.data?.isGlobalAdmin ?? false;

  useEffect(() => {
    if (!selectedWorkspaceId && list.length > 0) setWorkspace(list[0].id);
  }, [selectedWorkspaceId, list, setWorkspace]);

  useEffect(() => setNavOpen(false), [path]);

  // The left navigation: workspace rail + the current workspace's folder sidebar.
  const leftNav = (
    <div className="flex h-full">
      <WorkspaceRail isGlobalAdmin={isGlobalAdmin} />
      <FolderSidebar
        appName={branding?.appName ?? "Beacon"}
        logoUrl={branding?.hasLogo ? branding.logoUrl : null}
        path={path}
        isGlobalAdmin={isGlobalAdmin}
        userName={me.data?.name || me.data?.email || ""}
        workspace={selected}
      />
    </div>
  );

  return (
    <div className="flex h-screen overflow-hidden">
      {/* Desktop left nav */}
      <div className="hidden flex-none md:block">{leftNav}</div>

      {/* Mobile drawer */}
      {navOpen && (
        <div className="fixed inset-0 z-40 md:hidden" onClick={() => setNavOpen(false)}>
          <div className="absolute inset-0 bg-black/50" />
          <div className="absolute left-0 top-0 h-full" onClick={(e) => e.stopPropagation()}>
            {leftNav}
          </div>
        </div>
      )}

      <main className="flex min-w-0 flex-1 flex-col">
        {/* Top bar: mobile menu + breadcrumb */}
        <header className="flex h-[60px] flex-none items-center gap-3 border-b border-[var(--border)] px-4 md:px-6">
          <button onClick={() => setNavOpen(true)} aria-label="Open menu" className="rounded-md p-1 text-[var(--muted)] hover:bg-[var(--surface-2)] md:hidden">
            <Menu className="h-5 w-5" />
          </button>
          <Breadcrumb />
        </header>

        <div className="flex-1 overflow-y-auto">
          <div className="mx-auto max-w-6xl px-4 py-6 md:px-8">
            {workspaces.isLoading ? (
              <div className="flex items-center gap-2 text-[var(--muted)]">
                <Spinner /> Loading…
              </div>
            ) : selected ? (
              <Outlet />
            ) : (
              <p className="text-[var(--muted)]">No workspace available.</p>
            )}
          </div>
        </div>
      </main>
    </div>
  );
}

function FolderSidebar({
  appName,
  logoUrl,
  path,
  isGlobalAdmin,
  userName,
  workspace,
}: {
  appName: string;
  logoUrl: string | null;
  path: string;
  isGlobalAdmin: boolean;
  userName: string;
  workspace: ReturnType<typeof useSelectedWorkspace>;
}) {
  return (
    <aside className="flex h-full w-[236px] flex-none flex-col border-r border-white/[.07] bg-[var(--sidebar)]">
      {/* App brand strip — aligns with the main top bar */}
      <div className="flex h-[60px] flex-none items-center border-b border-white/[.06] px-4">
        {logoUrl ? (
          <img src={rootUrl(logoUrl)} alt={appName} className="h-7" />
        ) : (
          <span className="font-display text-lg font-bold tracking-tight text-white">{appName}</span>
        )}
      </div>

      {workspace && <WorkspaceHeader workspace={workspace} />}

      <div className="flex-1 overflow-y-auto px-2 pb-2">
        {workspace && <RailFolders workspaceId={workspace.id} workspaceAccess={workspace.access} />}

        <div className="mx-2 my-3 border-t border-white/[.07]" />

        <nav className="space-y-0.5">
          <NavItem to="/analytics" icon={<BarChart3 className="h-4 w-4" />} label="Analytics" active={path.startsWith("/analytics")} />
          <NavItem to="/shared" icon={<Inbox className="h-4 w-4" />} label="Shared with me" active={path.startsWith("/shared")} />
          {isGlobalAdmin && <NavItem to="/all-links" icon={<Globe className="h-4 w-4" />} label="All links" active={path.startsWith("/all-links")} />}
          {isGlobalAdmin && <NavItem to="/trash" icon={<Trash2 className="h-4 w-4" />} label="Trash" active={path.startsWith("/trash")} />}
          {isGlobalAdmin && <NavItem to="/settings" icon={<Settings className="h-4 w-4" />} label="Settings" active={path.startsWith("/settings")} />}
        </nav>
      </div>

      <div className="flex flex-none items-center gap-2 border-t border-white/[.07] px-4 py-3 text-sm">
        <div className="flex h-7 w-7 items-center justify-center rounded-full bg-white/10 text-xs font-semibold text-white">
          {(userName || "?").slice(0, 1).toUpperCase()}
        </div>
        <div className="min-w-0 flex-1">
          <div className="truncate text-xs text-white">{userName || "—"}</div>
          {isGlobalAdmin && <div className="text-[10px] font-medium text-[var(--accent)]">Global Admin</div>}
        </div>
        {isEntraConfigured && (
          <button onClick={signOut} aria-label="Sign out" title="Sign out" className="rounded-md p-1.5 text-[var(--sidebar-muted)] transition hover:bg-white/10 hover:text-white">
            <LogOut className="h-4 w-4" />
          </button>
        )}
      </div>
    </aside>
  );
}

function NavItem({ to, icon, label, active }: { to: string; icon: ReactNode; label: string; active: boolean }) {
  return (
    <Link
      to={to}
      className={cn(
        "flex items-center gap-2.5 rounded-md px-3 py-2 text-sm font-medium transition",
        active ? "beacon-active" : "text-[var(--sidebar-fg)] hover:bg-white/5 hover:text-white",
      )}
    >
      <span className={active ? "text-[var(--accent)]" : "text-[var(--sidebar-muted)]"}>{icon}</span>
      {label}
    </Link>
  );
}
