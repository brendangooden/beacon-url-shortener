import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { CountByKey, DayCount } from "@/lib/types";

const TEAL = "#0E9E9C";

export function ClicksByDayChart({ data }: { data: DayCount[] }) {
  if (data.length === 0) {
    return <EmptyChart label="No clicks in this window" />;
  }

  const rows = data.map((d) => ({ day: d.day.slice(5), count: d.count })); // MM-DD

  return (
    <div className="h-56 w-full">
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={rows} margin={{ top: 8, right: 8, bottom: 0, left: -18 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" vertical={false} />
          <XAxis dataKey="day" tick={{ fontSize: 11, fill: "var(--muted)" }} tickLine={false} axisLine={false} />
          <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: "var(--muted)" }} tickLine={false} axisLine={false} />
          <Tooltip
            cursor={{ fill: "var(--surface-2)" }}
            contentStyle={{ fontSize: 12, borderRadius: 8, border: "1px solid var(--border)" }}
          />
          <Bar dataKey="count" fill={TEAL} radius={[3, 3, 0, 0]} maxBarSize={34} />
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}

/** Tiny inline sparkline for stat tiles — no axes, no chrome. */
export function Sparkline({ data, className }: { data: number[]; className?: string }) {
  if (data.length < 2) return null;
  const w = 96;
  const h = 28;
  const max = Math.max(...data, 1);
  const step = w / (data.length - 1);
  const points = data.map((v, i) => `${(i * step).toFixed(1)},${(h - (v / max) * (h - 4) - 2).toFixed(1)}`).join(" ");
  return (
    <svg viewBox={`0 0 ${w} ${h}`} className={className} width={w} height={h} aria-hidden="true">
      <polyline points={points} fill="none" stroke={TEAL} strokeWidth={1.75} strokeLinejoin="round" strokeLinecap="round" />
    </svg>
  );
}

export function BreakdownList({ title, data }: { title: string; data: CountByKey[] }) {
  const max = data.reduce((m, d) => Math.max(m, d.count), 0);
  return (
    <div>
      <h4 className="mb-2.5 text-sm font-semibold text-[var(--fg)]">{title}</h4>
      {data.length === 0 ? (
        <p className="text-sm text-[var(--muted)]">No data</p>
      ) : (
        <ul className="space-y-2">
          {data.map((d) => (
            <li key={d.key} className="text-sm">
              <div className="flex items-center justify-between">
                <span className="truncate pr-2 text-[var(--fg)]" title={d.key}>
                  {d.key}
                </span>
                <span className="font-mono text-xs text-[var(--muted)]">{d.count}</span>
              </div>
              <div className="mt-1 h-1.5 w-full rounded-full bg-[var(--surface-2)]">
                <div
                  className="h-1.5 rounded-full bg-[var(--teal)]"
                  style={{ width: `${max === 0 ? 0 : Math.round((d.count / max) * 100)}%` }}
                />
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function EmptyChart({ label }: { label: string }) {
  return (
    <div className="flex h-56 items-center justify-center rounded-lg border border-dashed border-[var(--border)] text-sm text-[var(--muted)]">
      {label}
    </div>
  );
}
