import QRCode from "qrcode";
import { Download } from "lucide-react";
import { useEffect, useState } from "react";
import { CopyButton } from "@/components/CopyButton";
import { Button, Dialog, Spinner } from "@/components/ui";
import type { Link } from "@/lib/types";

/** Drop the http(s):// scheme, then replace runs of non-alphanumerics with a single underscore. */
function underscoreEscape(value: string): string {
  return value
    .replace(/^https?:\/\//i, "")
    .replace(/[^a-zA-Z0-9]+/g, "_")
    .replace(/^_+|_+$/g, "");
}

/** <short-url>_<target-url>, scheme-stripped + underscore-escaped, capped to a safe length. */
function fileNameFor(shortUrl: string, destination: string): string {
  const base = `${underscoreEscape(shortUrl)}_${underscoreEscape(destination)}`;
  return `${base.slice(0, 200)}.png`;
}

export function QrDialog({ link, open, onClose }: { link: Link; open: boolean; onClose: () => void }) {
  const [dataUrl, setDataUrl] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    // Client-side render of the SHORT url. Beacon ink on white for scan contrast.
    QRCode.toDataURL(link.shortUrl, {
      width: 512,
      margin: 2,
      errorCorrectionLevel: "M",
      color: { dark: "#0E1726", light: "#FFFFFF" },
    })
      .then((url) => {
        if (!cancelled) setDataUrl(url);
      })
      .catch(() => {
        if (!cancelled) setDataUrl(null);
      });
    return () => {
      cancelled = true;
    };
  }, [open, link.shortUrl]);

  const download = () => {
    if (!dataUrl) return;
    const a = document.createElement("a");
    a.href = dataUrl;
    a.download = fileNameFor(link.shortUrl, link.destination);
    document.body.appendChild(a);
    a.click();
    a.remove();
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={`QR code · /${link.code}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Close
          </Button>
          <Button onClick={download} disabled={!dataUrl}>
            <Download className="h-4 w-4" /> Download PNG
          </Button>
        </>
      }
    >
      <div className="flex flex-col items-center gap-4 py-1">
        {/* Where it points */}
        <div className="w-full">
          <p className="mb-1 text-[11px] font-semibold uppercase tracking-wider text-[var(--muted)]">Destination</p>
          <a
            href={link.destination}
            target="_blank"
            rel="noreferrer"
            className="block truncate text-sm text-[var(--fg)] hover:text-[var(--teal)] hover:underline"
            title={link.destination}
          >
            {link.destination}
          </a>
        </div>

        {dataUrl ? (
          <img
            src={dataUrl}
            alt={`QR code for ${link.shortUrl}`}
            className="h-56 w-56 rounded-lg border border-[var(--border)] bg-white p-1"
          />
        ) : (
          <div className="flex h-56 w-56 items-center justify-center">
            <Spinner />
          </div>
        )}

        {/* The short link, with an explicit copy control */}
        <div className="w-full">
          <p className="mb-1 text-[11px] font-semibold uppercase tracking-wider text-[var(--muted)]">Short link</p>
          <div className="flex items-center gap-2 rounded-lg border border-[var(--border)] bg-[var(--surface-2)] px-3 py-2">
            <a
              href={link.shortUrl}
              target="_blank"
              rel="noreferrer"
              className="min-w-0 flex-1 truncate font-mono text-sm text-[var(--teal)] hover:underline"
            >
              {link.shortUrl}
            </a>
            <CopyButton value={link.shortUrl} className="flex-none" />
          </div>
        </div>
      </div>
    </Dialog>
  );
}
