import { Link as RouterLink } from "@tanstack/react-router";
import { useState } from "react";
import { ClicksByDayChart } from "@/components/charts";
import { PageHeader } from "@/components/PageHeader";
import { StatTile } from "@/components/StatTile";
import { Card, Spinner } from "@/components/ui";
import { useSelectedWorkspace } from "@/hooks/useSelectedWorkspace";
import { useWorkspaceAnalytics } from "@/queries/hooks";

const WINDOWS = [7, 30, 90];

export function AnalyticsPage() {
  const workspace = useSelectedWorkspace();
  const [days, setDays] = useState(30);
  const analytics = useWorkspaceAnalytics(workspace?.id ?? "", days);
  const spark = analytics.data?.clicksByDay.map((d) => d.count) ?? [];

  return (
    <div>
      <PageHeader
        title="Analytics"
        subtitle={workspace?.name}
        actions={<WindowSelector days={days} onChange={setDays} />}
      />

      {analytics.isLoading ? (
        <div className="flex items-center gap-2 text-[var(--muted)]">
          <Spinner /> Loading…
        </div>
      ) : analytics.data ? (
        <div className="space-y-6">
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-3">
            <StatTile label="Clicks" value={analytics.data.totalClicks} sub={`last ${analytics.data.windowDays} days`} spark={spark} />
            <StatTile label="Active links" value={analytics.data.activeLinks} />
            <StatTile label="Total links" value={analytics.data.totalLinks} />
          </div>

          <Card className="p-4">
            <h2 className="mb-3 font-display text-sm font-semibold text-[var(--fg)]">Clicks by day</h2>
            <ClicksByDayChart data={analytics.data.clicksByDay} />
          </Card>

          <Card className="overflow-hidden">
            <h2 className="border-b border-[var(--border)] px-4 py-3 font-display text-sm font-semibold text-[var(--fg)]">
              Top links
            </h2>
            {analytics.data.topLinks.length === 0 ? (
              <p className="px-4 py-6 text-sm text-[var(--muted)]">No clicks yet.</p>
            ) : (
              <table className="w-full text-sm">
                <tbody>
                  {analytics.data.topLinks.map((l) => (
                    <tr key={l.linkId} className="border-b border-[var(--border)] last:border-0 hover:bg-[var(--surface-2)]">
                      <td className="px-4 py-2.5">
                        <RouterLink
                          to="/links/$linkId"
                          params={{ linkId: l.linkId }}
                          className="font-mono text-[13px] text-[var(--teal)] hover:underline"
                        >
                          /{l.code}
                        </RouterLink>
                      </td>
                      <td className="max-w-md truncate px-4 py-2.5 text-[var(--muted)]" title={l.destination}>
                        {l.destination}
                      </td>
                      <td className="px-4 py-2.5 text-right font-mono text-[13px] text-[var(--fg)]">{l.clicks}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </Card>
        </div>
      ) : (
        <p className="text-[var(--muted)]">No analytics available.</p>
      )}
    </div>
  );
}

export function WindowSelector({ days, onChange }: { days: number; onChange: (d: number) => void }) {
  return (
    <div className="flex overflow-hidden rounded-lg border border-[var(--border)]">
      {WINDOWS.map((w) => (
        <button
          key={w}
          onClick={() => onChange(w)}
          className={
            w === days
              ? "bg-[var(--accent)] px-3 py-1.5 text-xs font-medium text-[var(--accent-fg)]"
              : "bg-[var(--surface)] px-3 py-1.5 text-xs font-medium text-[var(--muted)] hover:bg-[var(--surface-2)]"
          }
        >
          {w}d
        </button>
      ))}
    </div>
  );
}
