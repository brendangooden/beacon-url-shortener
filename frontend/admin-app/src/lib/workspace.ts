interface WorkspaceLike {
  name: string;
  isPersonal: boolean;
  isOwner: boolean;
  ownerName: string | null;
}

/**
 * A workspace's display name. Every user's personal workspace is literally named "My Links", which
 * is useless once you're looking at someone else's — so a personal workspace you don't own shows the
 * owner's name instead. Your own workspaces keep their real name.
 */
export function workspaceLabel(w: WorkspaceLike): string {
  if (w.isPersonal && !w.isOwner && w.ownerName) {
    return w.ownerName;
  }
  return w.name;
}

/** A secondary owner line, or null when it would be redundant (you own it, or the name is the owner). */
export function workspaceSubLabel(w: WorkspaceLike): string | null {
  if (w.isOwner) return null;
  if (w.isPersonal) return "Personal workspace";
  return w.ownerName ? `Owned by ${w.ownerName}` : null;
}

// Stable per-workspace tile colour (named workspaces only; personal uses the amber star).
const TILE_COLORS = ["#2fc7ae", "#8394f7", "#f6799a", "#5eb0ef", "#a78bfa", "#34d399", "#e8a317"];

export function workspaceColor(id: string): string {
  let h = 0;
  for (let i = 0; i < id.length; i++) h = (h * 31 + id.charCodeAt(i)) >>> 0;
  return TILE_COLORS[h % TILE_COLORS.length];
}

/** One or two letters for a workspace tile, taken from its display name. */
export function workspaceInitial(label: string): string {
  const words = label.trim().split(/\s+/).filter(Boolean);
  if (words.length >= 2) return (words[0][0] + words[1][0]).toUpperCase();
  return label.trim().slice(0, 2).toUpperCase() || "?";
}
