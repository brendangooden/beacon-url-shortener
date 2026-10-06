import { Link as RouterLink } from "@tanstack/react-router";
import { ArrowLeft } from "lucide-react";
import { useState } from "react";
import { BreakdownList, ClicksByDayChart } from "@/components/charts";
import { PageHeader } from "@/components/PageHeader";
import { StatTile } from "@/components/StatTile";
import { WindowSelector } from "@/routes/AnalyticsPage";
import { Card, Spinner } from "@/components/ui";
import { useSelectedWorkspace } from "@/hooks/useSelectedWorkspace";
import { useLinkAnalytics } from "@/queries/hooks";

export function LinkAnalyticsPage({ linkId, workspaceIdOverride }: { linkId: string; workspaceIdOverride?: string }) {
  const workspace = useSelectedWorkspace();
  const [days, setDays] = useState(30);
  // A shared link may live in a workspace that isn't the selected one — honor the ?ws override.
  const workspaceId = workspaceIdOverride ?? workspace?.id ?? "";
  const analytics = useLinkAnalytics(workspaceId, linkId, days);
  const spark = analytics.data?.clicksByDay.map((d) => d.count) ?? [];

  return (
    <div>
      <PageHeader
        title={
          <span className="flex items-center gap-2">
            <RouterLink to="/" className="text-[var(--muted)] hover:text-[var(--fg)]" aria-label="Back to links">
              <ArrowLeft className="h-4 w-4" />
            </RouterLink>
            <span className="font-mono text-[var(--teal)]">/{analytics.data?.code ?? "…"}</span>
          </span>
        }
        subtitle="Link analytics"
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
            <StatTile label="Unique visitors" value={analytics.data.uniqueVisitors} />
          </div>

          <Card className="p-4">
            <h2 className="mb-3 font-display text-sm font-semibold text-[var(--fg)]">Clicks by day</h2>
            <ClicksByDayChart data={analytics.data.clicksByDay} />
          </Card>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <Card className="p-4">
              <BreakdownList title="Referrers" data={analytics.data.topReferrers} />
            </Card>
            <Card className="p-4">
              <BreakdownList title="Browser" data={analytics.data.byBrowser} />
            </Card>
            <Card className="p-4">
              <BreakdownList title="OS" data={analytics.data.byOs} />
            </Card>
            <Card className="p-4">
              <BreakdownList title="Device" data={analytics.data.byDevice} />
            </Card>
            <Card className="p-4">
              <BreakdownList title="Country" data={analytics.data.byCountry} />
            </Card>
          </div>
        </div>
      ) : (
        <p className="text-[var(--muted)]">No analytics available.</p>
      )}
    </div>
  );
}
