import type { ReactNode } from "react";

export function PageHeader({
  title,
  subtitle,
  actions,
}: {
  title: ReactNode;
  subtitle?: ReactNode;
  actions?: ReactNode;
}) {
  return (
    <div className="sticky top-0 z-10 -mx-4 mb-6 border-b border-[var(--border)] bg-[var(--bg)]/85 px-4 py-4 backdrop-blur md:-mx-8 md:px-8">
      <div className="flex items-center gap-3">
        <div className="min-w-0">
          <h1 className="truncate font-display text-lg font-semibold text-[var(--fg)]">{title}</h1>
          {subtitle && <p className="truncate text-sm text-[var(--muted)]">{subtitle}</p>}
        </div>
        {actions && <div className="ml-auto flex shrink-0 items-center gap-2">{actions}</div>}
      </div>
    </div>
  );
}
