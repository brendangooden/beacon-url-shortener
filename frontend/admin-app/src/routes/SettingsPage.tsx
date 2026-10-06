import { ExternalLink, ImageOff, Upload } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { PageHeader } from "@/components/PageHeader";
import { Button, Card, ErrorBanner, Input, Label, Spinner } from "@/components/ui";
import { useBranding, useDeleteLogo, useUpdateBranding, useUploadLogo } from "@/queries/hooks";
import { ApiError, rootUrl } from "@/lib/api";
import { accentForeground, isHexColor } from "@/lib/color";

const ACCEPTED = ["image/png", "image/jpeg", "image/svg+xml", "image/webp", "image/gif", "image/x-icon"];
const MAX_BYTES = 512 * 1024;

export function SettingsPage() {
  const branding = useBranding();
  const update = useUpdateBranding();
  const uploadLogo = useUploadLogo();
  const deleteLogo = useDeleteLogo();

  const [appName, setAppName] = useState("");
  const [tagline, setTagline] = useState("");
  const [homeUrl, setHomeUrl] = useState("");
  const [color, setColor] = useState("#E8A317");
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [dragging, setDragging] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);

  // Hydrate the form once branding loads.
  useEffect(() => {
    if (branding.data) {
      setAppName(branding.data.appName);
      setTagline(branding.data.tagline ?? "");
      setHomeUrl(branding.data.homeUrl ?? "");
      setColor(branding.data.primaryColor || "#E8A317");
    }
  }, [branding.data]);

  const save = () => {
    setError(null);
    setSaved(false);
    if (!appName.trim()) {
      setError("App name is required.");
      return;
    }
    if (color && !isHexColor(color)) {
      setError("Primary color must be a hex value like #7c3aed.");
      return;
    }
    update.mutate(
      {
        appName: appName.trim(),
        tagline: tagline.trim() || null,
        homeUrl: homeUrl.trim() || null,
        primaryColor: color || null,
      },
      {
        onSuccess: () => {
          setSaved(true);
          setTimeout(() => setSaved(false), 2000);
        },
        onError: (e) => setError(e instanceof ApiError ? e.message : "Save failed."),
      },
    );
  };

  const handleFile = (file: File | undefined) => {
    setError(null);
    if (!file) return;
    if (!ACCEPTED.includes(file.type)) {
      setError(`Unsupported image type (${file.type || "unknown"}). Use PNG, JPEG, SVG, WebP, GIF or ICO.`);
      return;
    }
    if (file.size > MAX_BYTES) {
      setError(`Logo is ${(file.size / 1024).toFixed(0)} KB; the max is 512 KB.`);
      return;
    }
    uploadLogo.mutate(file, { onError: (e) => setError(e instanceof ApiError ? e.message : "Upload failed.") });
  };

  if (branding.isLoading) {
    return (
      <div className="flex items-center gap-2 text-[var(--muted)]">
        <Spinner /> Loading…
      </div>
    );
  }

  const logoUrl = branding.data?.hasLogo ? branding.data.logoUrl : null;
  const validColor = isHexColor(color) ? color : "#E8A317";

  return (
    <div>
      <PageHeader
        title="Branding"
        subtitle="Applies to every user and the public 404 landing page"
        actions={
          <Button onClick={save} disabled={update.isPending}>
            {update.isPending ? "Saving…" : saved ? "Saved ✓" : "Save changes"}
          </Button>
        }
      />

      {error && (
        <div className="mb-4">
          <ErrorBanner message={error} />
        </div>
      )}

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-[1fr_320px]">
        <div className="space-y-6">
          <Card className="space-y-4 p-4">
            <h2 className="font-display text-sm font-semibold text-[var(--fg)]">Identity</h2>
            <div>
              <Label>App name</Label>
              <Input value={appName} onChange={(e) => setAppName(e.target.value)} placeholder="Beacon" />
            </div>
            <div>
              <Label>Tagline</Label>
              <Input value={tagline} onChange={(e) => setTagline(e.target.value)} placeholder="Short links, on point." />
            </div>
            <div>
              <Label>Home URL</Label>
              <Input value={homeUrl} onChange={(e) => setHomeUrl(e.target.value)} placeholder="https://example.com" />
            </div>
            <div>
              <Label>Primary color</Label>
              <div className="flex items-center gap-2">
                <input
                  type="color"
                  value={validColor}
                  onChange={(e) => setColor(e.target.value)}
                  aria-label="Primary color swatch"
                  className="h-9 w-10 cursor-pointer rounded-md border border-[var(--border)] bg-[var(--surface)]"
                />
                <Input value={color} onChange={(e) => setColor(e.target.value)} placeholder="#7c3aed" className="max-w-[140px] font-mono" />
                <span className="text-xs text-[var(--muted)]">Used for the primary button + active nav beacon.</span>
              </div>
            </div>
          </Card>

          <Card className="space-y-3 p-4">
            <h2 className="font-display text-sm font-semibold text-[var(--fg)]">Logo</h2>
            <div
              onDragOver={(e) => {
                e.preventDefault();
                setDragging(true);
              }}
              onDragLeave={() => setDragging(false)}
              onDrop={(e) => {
                e.preventDefault();
                setDragging(false);
                handleFile(e.dataTransfer.files[0]);
              }}
              className={
                "flex flex-col items-center justify-center gap-2 rounded-lg border-2 border-dashed p-6 text-center transition " +
                (dragging ? "border-[var(--accent)] bg-[var(--accent)]/5" : "border-[var(--border)]")
              }
            >
              {logoUrl ? (
                <img src={rootUrl(logoUrl)} alt="Current logo" className="max-h-16" />
              ) : (
                <ImageOff className="h-8 w-8 text-[var(--muted)]/50" />
              )}
              <p className="text-sm text-[var(--muted)]">
                Drag an image here, or{" "}
                <button className="font-medium text-[var(--teal)] hover:underline" onClick={() => fileInput.current?.click()}>
                  browse
                </button>
              </p>
              <p className="text-xs text-[var(--muted)]">PNG, JPEG, SVG, WebP, GIF or ICO · max 512 KB</p>
              <input
                ref={fileInput}
                type="file"
                accept={ACCEPTED.join(",")}
                className="hidden"
                onChange={(e) => handleFile(e.target.files?.[0])}
              />
            </div>
            <div className="flex items-center gap-2">
              <Button variant="secondary" size="sm" onClick={() => fileInput.current?.click()} disabled={uploadLogo.isPending}>
                <Upload className="h-4 w-4" /> {uploadLogo.isPending ? "Uploading…" : "Upload logo"}
              </Button>
              {logoUrl && (
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => deleteLogo.mutate()}
                  disabled={deleteLogo.isPending}
                >
                  Remove logo
                </Button>
              )}
            </div>
          </Card>

          <Card className="space-y-2 p-4">
            <h2 className="font-display text-sm font-semibold text-[var(--fg)]">Sharing</h2>
            {branding.data?.shareEmailDomain ? (
              <p className="text-sm text-[var(--muted)]">
                Links, folders, and workspaces can only be shared with{" "}
                <span className="font-mono text-[var(--fg)]">@{branding.data.shareEmailDomain}</span> addresses.
              </p>
            ) : (
              <p className="text-sm text-[var(--muted)]">Sharing is allowed with any email address.</p>
            )}
            <p className="text-xs text-[var(--muted)]">
              Set by the deployment via the <span className="font-mono">Sharing:AllowedEmailDomain</span> configuration.
            </p>
          </Card>
        </div>

        {/* Live preview of the public 404 landing */}
        <div>
          <Label>404 landing preview</Label>
          <Landing404Preview appName={appName || "Beacon"} tagline={tagline} homeUrl={homeUrl} color={validColor} logoUrl={logoUrl} />
        </div>
      </div>
    </div>
  );
}

function Landing404Preview({
  appName,
  tagline,
  homeUrl,
  color,
  logoUrl,
}: {
  appName: string;
  tagline: string;
  homeUrl: string;
  color: string;
  logoUrl: string | null;
}) {
  return (
    <Card className="overflow-hidden">
      <div className="h-1.5" style={{ backgroundColor: color }} />
      <div className="flex flex-col items-center gap-3 px-6 py-10 text-center">
        {logoUrl ? (
          <img src={rootUrl(logoUrl)} alt={appName} className="max-h-10" />
        ) : (
          <span className="font-display text-xl font-bold text-[var(--fg)]">{appName}</span>
        )}
        <div className="font-display text-4xl font-bold" style={{ color }}>
          404
        </div>
        <p className="text-sm text-[var(--muted)]">This short link doesn’t exist or has expired.</p>
        {tagline && <p className="text-xs text-[var(--muted)]">{tagline}</p>}
        <span
          className="mt-1 inline-flex items-center gap-1 rounded-lg px-3 py-1.5 text-sm font-medium"
          style={{ backgroundColor: color, color: accentForeground(color) }}
        >
          {homeUrl ? "Go to homepage" : appName} <ExternalLink className="h-3.5 w-3.5" />
        </span>
      </div>
    </Card>
  );
}
