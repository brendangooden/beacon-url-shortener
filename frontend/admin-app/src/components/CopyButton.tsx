import { Check, Copy } from "lucide-react";
import { useState } from "react";
import { cn } from "@/lib/cn";

export function CopyButton({ value, className }: { value: string; className?: string }) {
  const [copied, setCopied] = useState(false);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      setTimeout(() => setCopied(false), 1200);
    } catch {
      // clipboard blocked — ignore
    }
  };

  return (
    <button
      onClick={copy}
      aria-label={`Copy ${value}`}
      title="Copy short link"
      className={cn(
        "inline-flex h-6 w-6 items-center justify-center rounded text-[var(--muted)] transition hover:bg-[var(--surface)] hover:text-[var(--fg)]",
        className,
      )}
    >
      {copied ? <Check className="h-3.5 w-3.5 text-[var(--teal)]" /> : <Copy className="h-3.5 w-3.5" />}
    </button>
  );
}
