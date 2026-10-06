import { Sparkline } from "@/components/charts";
import { Card } from "@/components/ui";

export function StatTile({
  label,
  value,
  sub,
  spark,
}: {
  label: string;
  value: number;
  sub?: string;
  spark?: number[];
}) {
  return (
    <Card className="p-4">
      <div className="flex items-start justify-between gap-2">
        <div>
          <div className="text-xs font-medium text-[var(--muted)]">{label}</div>
          <div className="mt-1 font-display text-2xl font-semibold tabular-nums text-[var(--fg)]">
            {value.toLocaleString()}
          </div>
          {sub && <div className="text-xs text-[var(--muted)]">{sub}</div>}
        </div>
        {spark && spark.length > 1 && <Sparkline data={spark} className="mt-1 shrink-0" />}
      </div>
    </Card>
  );
}
