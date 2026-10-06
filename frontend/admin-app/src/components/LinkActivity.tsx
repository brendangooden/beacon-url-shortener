import {
  ArrowRight,
  FolderInput,
  Pencil,
  Plus,
  Power,
  PowerOff,
  Tag,
  Timer,
} from "lucide-react";
import { useLinkEvents } from "@/queries/hooks";
import { Spinner } from "@/components/ui";
import type { LinkEvent, LinkEventType } from "@/lib/types";

const rtf = new Intl.RelativeTimeFormat(undefined, { numeric: "auto" });
const UNITS: [Intl.RelativeTimeFormatUnit, number][] = [
  ["year", 31536000],
  ["month", 2592000],
  ["day", 86400],
  ["hour", 3600],
  ["minute", 60],
];

function timeAgo(iso: string): string {
  const secs = (Date.now() - new Date(iso).getTime()) / 1000;
  if (secs < 45) return "just now";
  for (const [unit, size] of UNITS) {
    if (secs >= size) return rtf.format(-Math.round(secs / size), unit);
  }
  return "just now";
}

const ICONS: Record<LinkEventType, typeof Plus> = {
  Created: Plus,
  DestinationChanged: ArrowRight,
  FolderMoved: FolderInput,
  MetadataChanged: Pencil,
  Enabled: Power,
  Disabled: PowerOff,
  ExpiryChanged: Timer,
  Renamed: Tag,
};

function headline(e: LinkEvent): string {
  switch (e.type) {
    case "Created":
      return "created this link";
    case "DestinationChanged":
      return "changed the destination";
    case "FolderMoved":
      return `moved it from ${e.oldValue ?? "?"} to ${e.newValue ?? "?"}`;
    case "MetadataChanged":
      return `updated ${e.newValue ?? "details"}`;
    case "Enabled":
      return "enabled the link";
    case "Disabled":
      return "disabled the link";
    case "ExpiryChanged":
      return e.newValue === "never" ? "removed the expiry" : `set expiry to ${e.newValue}`;
    case "Renamed":
      return `renamed the code from /${e.oldValue ?? "?"} to /${e.newValue ?? "?"}`;
    default:
      return "made a change";
  }
}

export function LinkActivity({ workspaceId, linkId }: { workspaceId: string; linkId: string }) {
  const events = useLinkEvents(workspaceId, linkId, true);

  if (events.isLoading) {
    return (
      <div className="flex items-center gap-2 py-6 text-[var(--muted)]">
        <Spinner /> Loading activity…
      </div>
    );
  }
  if (events.isError) {
    return <p className="py-6 text-sm text-[var(--muted)]">Could not load activity.</p>;
  }
  const list = events.data ?? [];
  if (list.length === 0) {
    return <p className="py-6 text-sm text-[var(--muted)]">No activity recorded yet.</p>;
  }

  return (
    <ol className="space-y-3 py-1">
      {list.map((e, i) => {
        const Icon = ICONS[e.type] ?? Pencil;
        const showDiff = e.type === "DestinationChanged" && e.oldValue && e.newValue;
        return (
          <li key={i} className="flex gap-3">
            <div className="mt-0.5 flex h-7 w-7 flex-none items-center justify-center rounded-full bg-[var(--surface-2)] text-[var(--muted)]">
              <Icon className="h-3.5 w-3.5" />
            </div>
            <div className="min-w-0 flex-1">
              <p className="text-sm text-[var(--fg)]">
                <span className="font-medium">{e.actorName}</span>{" "}
                <span className="text-[var(--muted)]">{headline(e)}</span>
              </p>
              {showDiff && (
                <p className="mt-0.5 truncate text-xs text-[var(--muted)]" title={`${e.oldValue} → ${e.newValue}`}>
                  <span className="line-through">{e.oldValue}</span> → {e.newValue}
                </p>
              )}
              <p className="mt-0.5 text-[11px] text-[var(--muted)]">{timeAgo(e.createdOnUtc)}</p>
            </div>
          </li>
        );
      })}
    </ol>
  );
}
