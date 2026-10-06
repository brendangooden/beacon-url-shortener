/** Relative luminance of a #rrggbb hex, 0 (black) .. 1 (white). */
function luminance(hex: string): number {
  const m = /^#?([0-9a-f]{6})$/i.exec(hex.trim());
  if (!m) return 0.5;
  const int = parseInt(m[1], 16);
  const chan = [(int >> 16) & 255, (int >> 8) & 255, int & 255].map((c) => {
    const s = c / 255;
    return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * chan[0] + 0.7152 * chan[1] + 0.0722 * chan[2];
}

/** Readable foreground (near-black or white) for text/icons sitting on the given accent color. */
export function accentForeground(hex: string): string {
  return luminance(hex) > 0.55 ? "#1A1206" : "#FFFFFF";
}

/** True when the string is a valid #rgb or #rrggbb hex. */
export function isHexColor(value: string): boolean {
  return /^#([0-9a-f]{3}|[0-9a-f]{6})$/i.test(value.trim());
}
