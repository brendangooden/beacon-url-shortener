import type { AccessLevel } from "@/lib/types";

/** Editor or Manager can create/edit/move/disable. */
export const canEdit = (access?: AccessLevel): boolean => access === "Editor" || access === "Manager";

/** Only Manager can re-share and delete. */
export const canManage = (access?: AccessLevel): boolean => access === "Manager";
