import { Link as RouterLink } from "@tanstack/react-router";
import { BarChart3, Globe, Search } from "lucide-react";
import { useState } from "react";
import { CodePill } from "@/components/CodePill";
import { PageHeader } from "@/components/PageHeader";
import { useMe } from "@/queries/hooks";
import { useAdminLinks } from "@/queries/hooks";
import { Badge, Button, Card, ErrorBanner, Input, Spinner } from "@/components/ui";

/** Global-Admin only: every link across every workspace. Read-only overview. */
export function AllLinksPage() {
  const me = useMe();
  const isGlobalAdmin = me.data?.isGlobalAdmin ?? false;
  const [search, setSearch] = useState("");
  const links = useAdminLinks(search, isGlobalAdmin);

  if (me.isSuccess && !isGlobalAdmin) {
    return (
      <div>
        <PageHeader title="All links" subtitle="Every link across all workspaces" />
        <ErrorBanner message="This view is for Global Admins only." />
      </div>
    );
  }

  return (
    <div>
      <PageHeader title="All links" subtitle="Every link across all workspaces" />

      <div className="mb-4 flex items-center gap-2">
        <div className="relative max-w-xs flex-1">
          <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--muted)]" />
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search code, destination, workspace…"
            className="pl-8"
          />
        </div>
        {links.data && <span className="text-sm text-[var(--muted)]">{links.data.length} links</span>}
      </div>

      {links.isLoading ? (
        <div className="flex items-center gap-2 text-[var(--muted)]">
          <Spinner /> Loading links…
        </div>
      ) : links.isError ? (
        <ErrorBanner message="Could not load links." />
      ) : (links.data?.length ?? 0) === 0 ? (
        <Card className="p-10 text-center">
          <Globe className="mx-auto mb-3 h-8 w-8 text-[var(--muted)]/50" />
          <p className="text-[var(--muted)]">No links found.</p>
        </Card>
      ) : (
        <Card className="overflow-hidden">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-[var(--border)] text-left text-xs font-medium text-[var(--muted)]">
                <th className="px-4 py-2.5">Short link</th>
                <th className="px-4 py-2.5">Workspace</th>
                <th className="px-4 py-2.5">Destination</th>
                <th className="px-4 py-2.5 text-right">Clicks</th>
                <th className="px-4 py-2.5 text-center">Active</th>
                <th className="px-4 py-2.5" />
              </tr>
            </thead>
            <tbody>
              {links.data!.map((link) => (
                <tr key={link.id} className="border-b border-[var(--border)] last:border-0 hover:bg-[var(--surface-2)]">
                  <td className="px-4 py-2.5">
                    <CodePill code={link.code} shortUrl={link.shortUrl} />
                    {link.title && <div className="mt-0.5 text-xs text-[var(--muted)]">{link.title}</div>}
                  </td>
                  <td className="px-4 py-2.5">
                    <div className="text-[var(--fg)]">{link.workspaceName}</div>
                    {link.ownerName && <div className="text-xs text-[var(--muted)]">{link.ownerName}</div>}
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
                  <td className="px-4 py-2.5 text-right font-mono text-[13px] text-[var(--fg)]">{link.clickCount}</td>
                  <td className="px-4 py-2.5">
                    <div className="flex justify-center">
                      <Badge tone={link.isActive ? "teal" : "neutral"}>{link.isActive ? "Active" : "Off"}</Badge>
                    </div>
                  </td>
                  <td className="px-4 py-2.5">
                    <div className="flex justify-end">
                      <RouterLink to="/links/$linkId" params={{ linkId: link.id }} search={{ ws: link.workspaceId }}>
                        <Button variant="ghost" size="icon" aria-label="Analytics">
                          <BarChart3 className="h-4 w-4" />
                        </Button>
                      </RouterLink>
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
