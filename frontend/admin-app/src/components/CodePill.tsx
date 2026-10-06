import { CopyButton } from "@/components/CopyButton";

/** The short code as the hero datum: a mono, amber-tinted pill with an inline copy control. */
export function CodePill({ code, shortUrl }: { code: string; shortUrl: string }) {
  return (
    <span className="inline-flex items-center gap-0.5 rounded-md border border-[var(--accent)]/30 bg-[var(--accent)]/10 py-0.5 pl-2 pr-1">
      <span className="font-mono text-[13px] font-medium leading-none text-[var(--fg)]">/{code}</span>
      <CopyButton value={shortUrl} />
    </span>
  );
}
