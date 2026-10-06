import { Trash2, UserPlus } from "lucide-react";
import { useState } from "react";
import { useDialogs } from "@/components/dialogs";
import { Badge, Button, Dialog, ErrorBanner, Input, Select, Spinner } from "@/components/ui";
import { useBranding, useChangeShare, useCreateShare, useDeleteShare, useShares } from "@/queries/hooks";
import { ApiError } from "@/lib/api";
import type { AccessLevel, ResourceType } from "@/lib/types";

const LEVELS: AccessLevel[] = ["Viewer", "Editor", "Manager"];

export function ShareDialog({
  resourceType,
  resourceId,
  resourceName,
  open,
  onClose,
}: {
  resourceType: ResourceType;
  resourceId: string;
  resourceName: string;
  open: boolean;
  onClose: () => void;
}) {
  const dialogs = useDialogs();
  const branding = useBranding();
  const domain = branding.data?.shareEmailDomain ?? null;
  const shares = useShares(resourceType, resourceId);
  const create = useCreateShare();
  const change = useChangeShare(resourceType, resourceId);
  const remove = useDeleteShare(resourceType, resourceId);

  // With a domain policy the user types only the local part (fname.lname); we append "@domain".
  const [entry, setEntry] = useState("");
  const [level, setLevel] = useState<AccessLevel>("Viewer");
  const [error, setError] = useState<string | null>(null);

  const trimmed = entry.trim();
  const email = domain ? (trimmed ? `${trimmed}@${domain}` : "") : trimmed;

  const add = () => {
    setError(null);
    if (!email) return;
    create.mutate(
      { resourceType, resourceId, email, level },
      {
        onSuccess: () => setEntry(""),
        onError: (e) => setError(e instanceof ApiError ? e.message : "Could not share."),
      },
    );
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={`Share ${resourceType.toLowerCase()} · ${resourceName}`}
      footer={
        <Button variant="secondary" onClick={onClose}>
          Done
        </Button>
      }
    >
      <div className="space-y-4">
        {error && <ErrorBanner message={error} />}

        <div className="flex items-end gap-2">
          <div className="flex-1">
            {domain ? (
              <div className="flex items-stretch overflow-hidden rounded-lg border border-[var(--border)] bg-[var(--surface)] focus-within:ring-2 focus-within:ring-[var(--accent)]">
                <input
                  value={entry}
                  onChange={(e) => setEntry(e.target.value.replace(/@.*$/, ""))}
                  onKeyDown={(e) => e.key === "Enter" && add()}
                  placeholder="fname.lname"
                  className="min-w-0 flex-1 bg-transparent px-3 text-sm text-[var(--fg)] placeholder:text-[var(--muted)]/70 focus:outline-none"
                />
                <span className="flex select-none items-center border-l border-[var(--border)] bg-[var(--surface-2)] px-2.5 text-sm text-[var(--muted)]">
                  @{domain}
                </span>
              </div>
            ) : (
              <Input
                type="email"
                value={entry}
                onChange={(e) => setEntry(e.target.value)}
                onKeyDown={(e) => e.key === "Enter" && add()}
                placeholder="person@example.com"
              />
            )}
          </div>
          <div className="w-28">
            <Select value={level} onChange={(e) => setLevel(e.target.value as AccessLevel)} aria-label="Access level">
              {LEVELS.map((l) => (
                <option key={l} value={l}>
                  {l}
                </option>
              ))}
            </Select>
          </div>
          <Button onClick={add} disabled={create.isPending || !email}>
            <UserPlus className="h-4 w-4" /> Share
          </Button>
        </div>

        <div>
          <h3 className="mb-2 text-xs font-medium text-[var(--muted)]">People with access</h3>
          {shares.isLoading ? (
            <div className="flex items-center gap-2 text-[var(--muted)]">
              <Spinner /> Loading…
            </div>
          ) : (shares.data?.length ?? 0) === 0 ? (
            <p className="text-sm text-[var(--muted)]">
              Not shared with anyone yet. The owner{" "}
              <Badge>always has access</Badge> and access inherits down.
            </p>
          ) : (
            <ul className="divide-y divide-[var(--border)] rounded-lg border border-[var(--border)]">
              {shares.data!.map((s) => (
                <li key={s.id} className="flex items-center gap-2 px-3 py-2">
                  <div className="min-w-0 flex-1">
                    <div className="truncate text-sm text-[var(--fg)]">{s.granteeName || s.granteeEmail}</div>
                    <div className="truncate text-xs text-[var(--muted)]">{s.granteeEmail}</div>
                  </div>
                  <div className="w-28">
                    <Select
                      value={s.level}
                      onChange={(e) =>
                        change.mutate(
                          { grantId: s.id, level: e.target.value as AccessLevel },
                          { onError: (err) => dialogs.toast({ message: err instanceof ApiError ? err.message : "Failed", tone: "error" }) },
                        )
                      }
                      aria-label={`Access for ${s.granteeEmail}`}
                    >
                      {LEVELS.map((l) => (
                        <option key={l} value={l}>
                          {l}
                        </option>
                      ))}
                    </Select>
                  </div>
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={`Remove ${s.granteeEmail}`}
                    onClick={() =>
                      remove.mutate(s.id, {
                        onError: (err) => dialogs.toast({ message: err instanceof ApiError ? err.message : "Failed", tone: "error" }),
                      })
                    }
                  >
                    <Trash2 className="h-4 w-4 text-[var(--danger)]" />
                  </Button>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </Dialog>
  );
}
