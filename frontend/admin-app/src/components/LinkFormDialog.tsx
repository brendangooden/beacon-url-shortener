import { useState } from "react";
import { ChevronDown, ChevronRight, Pencil } from "lucide-react";
import { CodePill } from "@/components/CodePill";
import { LinkActivity } from "@/components/LinkActivity";
import { useDialogs } from "@/components/dialogs";
import { Button, Dialog, ErrorBanner, Input, Label, Select, Textarea } from "@/components/ui";
import { useBranding, useCreateLink, useRenameLink, useUpdateLink } from "@/queries/hooks";
import { ApiError } from "@/lib/api";
import { cn } from "@/lib/cn";
import type { Folder, Link } from "@/lib/types";

function toLocalInput(iso: string | null): string {
  if (!iso) return "";
  const d = new Date(iso);
  const off = d.getTimezoneOffset();
  return new Date(d.getTime() - off * 60000).toISOString().slice(0, 16);
}

type Tab = "details" | "activity";

export function LinkFormDialog({
  workspaceId,
  folders,
  existing,
  defaultFolderId,
  open,
  onClose,
}: {
  workspaceId: string;
  folders: Folder[];
  existing: Link | null;
  defaultFolderId?: string | null;
  open: boolean;
  onClose: () => void;
}) {
  const isEdit = existing !== null;
  const create = useCreateLink(workspaceId);
  const update = useUpdateLink(workspaceId);
  const rename = useRenameLink(workspaceId);
  const dialogs = useDialogs();
  const branding = useBranding();
  // Host shown in the live code preview, e.g. "r.example.com" (scheme stripped for readability).
  const displayBase = (branding.data?.shortBaseUrl ?? "").replace(/^https?:\/\//i, "").replace(/\/$/, "");

  const [tab, setTab] = useState<Tab>("details");
  const [destination, setDestination] = useState(existing?.destination ?? "https://");
  const [code, setCode] = useState("");
  const [title, setTitle] = useState(existing?.title ?? "");
  // New link: preselect the folder currently being viewed. Edit: keep the link's own folder.
  const [folderId, setFolderId] = useState(existing?.folderId ?? defaultFolderId ?? "");
  const [notes, setNotes] = useState(existing?.notes ?? "");
  // Tags are preserved but no longer edited here — carry the existing ones through untouched.
  const [tags] = useState(existing?.tags ?? []);
  const [expiry, setExpiry] = useState(toLocalInput(existing?.expiresOnUtc ?? null));
  // Show the low-priority fields expanded only when an edited link already uses them.
  const [showMore, setShowMore] = useState(isEdit && (!!existing?.expiresOnUtc || !!existing?.notes));
  const [error, setError] = useState<string | null>(null);

  const pending = create.isPending || update.isPending;

  // Empty or scheme-only "https://" counts as no destination.
  const bareDestination = destination.trim().replace(/^https?:\/\/$/i, "");
  const hasDestination = bareDestination.length > 0;
  const trimmedCode = code.trim();

  const submit = async () => {
    setError(null);
    // Default to https:// when the user typed a bare host without a scheme.
    const dest = /^https?:\/\//i.test(bareDestination) ? bareDestination : `https://${bareDestination}`;
    const body = {
      destination: dest,
      folderId: folderId || null,
      title: title.trim() || null,
      notes: notes.trim() || null,
      tags,
      expiresOnUtc: expiry ? new Date(expiry).toISOString() : null,
    };

    try {
      if (isEdit) {
        await update.mutateAsync({ id: existing.id, body });
      } else {
        await create.mutateAsync({ ...body, code: trimmedCode || null });
      }
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Something went wrong.");
    }
  };

  const renameCode = async () => {
    if (!existing) return;
    const newCode = await dialogs.prompt({
      title: "Rename short link",
      message:
        `Choose a new code for /${existing.code}. The old code (/${existing.code}) keeps forwarding to the same ` +
        "destination, but it retires permanently — no one, including you, can ever claim it again.",
      label: "New code",
      defaultValue: existing.code,
      placeholder: "e.g. summer-sale",
      required: true,
      confirmText: "Rename",
    });
    if (!newCode || newCode === existing.code) return;

    try {
      await rename.mutateAsync({ id: existing.id, code: newCode });
      dialogs.toast({ message: `Renamed to /${newCode}`, tone: "success" });
      // The dialog holds a snapshot of `existing` — close it so a stale code can't be shown or reused.
      onClose();
    } catch (e) {
      dialogs.toast({ message: e instanceof ApiError ? e.message : "Could not rename the link.", tone: "error" });
    }
  };

  const footer =
    tab === "activity" ? (
      <Button variant="secondary" onClick={onClose}>
        Close
      </Button>
    ) : (
      <>
        <Button variant="secondary" onClick={onClose} disabled={pending}>
          Cancel
        </Button>
        <Button onClick={submit} disabled={pending || !hasDestination}>
          {isEdit ? "Save" : "Create"}
        </Button>
      </>
    );

  return (
    <Dialog open={open} onClose={onClose} title={isEdit ? "Edit link" : "New link"} footer={footer}>
      {isEdit && (
        <div className="mb-4 flex gap-1 border-b border-[var(--border)]">
          <TabButton active={tab === "details"} onClick={() => setTab("details")}>
            Details
          </TabButton>
          <TabButton active={tab === "activity"} onClick={() => setTab("activity")}>
            Activity
          </TabButton>
        </div>
      )}

      {tab === "activity" && isEdit ? (
        <LinkActivity workspaceId={workspaceId} linkId={existing.id} />
      ) : (
        <div className="space-y-4">
          {error && <ErrorBanner message={error} />}

          {isEdit && (
            <div>
              <Label>Short link</Label>
              <div className="flex items-center gap-2">
                <CodePill code={existing.code} shortUrl={existing.shortUrl} />
                <Button variant="ghost" size="icon" aria-label="Rename short link" onClick={renameCode} disabled={rename.isPending}>
                  <Pencil className="h-3.5 w-3.5" />
                </Button>
              </div>
            </div>
          )}

          {/* Required */}
          <div>
            <label className="mb-1 flex items-center gap-1 text-xs font-semibold text-[var(--fg)]">
              Destination URL <span className="text-[var(--accent)]" aria-hidden>*</span>
            </label>
            <Input
              autoFocus
              value={destination}
              onChange={(e) => setDestination(e.target.value)}
              placeholder="https://example.com/page"
              onKeyDown={(e) => {
                if (e.key === "Enter" && hasDestination && !pending) void submit();
              }}
            />
          </div>

          {/* Optional, most relevant first */}
          <div>
            <Label>Title</Label>
            <Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Optional label" />
          </div>

          {!isEdit && (
            <div>
              <Label>Custom code</Label>
              <Input
                value={code}
                onChange={(e) => setCode(e.target.value)}
                placeholder="Optional — leave blank to auto-generate"
              />
              {displayBase && (
                <p className="mt-1.5 truncate font-mono text-xs text-[var(--muted)]">
                  {displayBase}/
                  {trimmedCode ? (
                    <span className="text-[var(--teal)]">{trimmedCode}</span>
                  ) : (
                    <span className="italic opacity-70">auto-generated</span>
                  )}
                </p>
              )}
            </div>
          )}

          <div>
            <Label>Folder</Label>
            <Select value={folderId} onChange={(e) => setFolderId(e.target.value)}>
              <option value="">No folder</option>
              {folders.map((f) => (
                <option key={f.id} value={f.id}>
                  {f.name}
                </option>
              ))}
            </Select>
          </div>

          {/* Low-priority fields, collapsed by default */}
          <div className="border-t border-[var(--border)] pt-3">
            <button
              type="button"
              onClick={() => setShowMore((v) => !v)}
              className="flex items-center gap-1 text-xs font-medium text-[var(--muted)] transition hover:text-[var(--fg)]"
            >
              {showMore ? <ChevronDown className="h-3.5 w-3.5" /> : <ChevronRight className="h-3.5 w-3.5" />}
              More options
            </button>
            {showMore && (
              <div className="mt-3 space-y-4">
                <div>
                  <Label>Expires</Label>
                  <Input type="datetime-local" value={expiry} onChange={(e) => setExpiry(e.target.value)} />
                </div>
                <div>
                  <Label>Notes</Label>
                  <Textarea value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="Optional notes" />
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </Dialog>
  );
}

function TabButton({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        "-mb-px border-b-2 px-3 py-1.5 text-sm font-medium transition",
        active
          ? "border-[var(--accent)] text-[var(--fg)]"
          : "border-transparent text-[var(--muted)] hover:text-[var(--fg)]",
      )}
    >
      {children}
    </button>
  );
}
