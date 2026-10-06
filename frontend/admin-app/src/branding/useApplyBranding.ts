import { useEffect } from "react";
import { useBranding } from "@/queries/hooks";
import { accentForeground } from "@/lib/color";
import type { Branding } from "@/lib/types";

const FALLBACK_ACCENT = "#E8A317"; // beacon amber

/**
 * Applies runtime branding for all users: the admin's primary color becomes the document `--accent`
 * (the amber beacon is the fallback), and the app name becomes the document title.
 */
export function useApplyBranding(): Branding | undefined {
  const { data } = useBranding();
  const accent = data?.primaryColor || FALLBACK_ACCENT;
  const appName = data?.appName;

  useEffect(() => {
    const root = document.documentElement;
    root.style.setProperty("--accent", accent);
    root.style.setProperty("--accent-fg", accentForeground(accent));
  }, [accent]);

  useEffect(() => {
    if (appName) document.title = appName;
  }, [appName]);

  return data;
}
