# Renaming a Link keeps its old ShortCode as a permanent forward

A Link's ShortCode was immutable until now. We're adding **Rename**: an Editor can give a Link a
new ShortCode. The old ShortCode becomes a **Retired code** — it keeps resolving to the Link's
current Destination (one hop, same 302 as any live code — not a hop through the new short URL), and
is never reusable by any Link, including this one. Renaming twice (A→B→C) makes both A and B resolve
straight to C's Destination; there is no hop-through-B chain. Rename requires only Editor access,
the same as any other Link edit.

## Consequences

- **Namespace cost.** Every rename permanently burns a code — there is no cooldown or reuse. Chosen
  over reuse-after-expiry because reclaiming a code a real person may have bookmarked or shared risks
  sending old traffic to an unrelated future Link. Accepted for now with no cap on renames per Link,
  since this is a staff-only internal tool, not public self-serve — revisit if squatting/exhaustion
  ever becomes real.
- **Soft-delete interaction.** Trashing a Link (existing soft-delete, see `AddLinkSoftDelete`) 404s
  its Retired codes too, consistent with the current code — both resurrect together on restore.
  Purging a Link permanently frees all of its codes, current and retired, matching purge's existing
  "gone forever, code reusable" semantics.
- **New uniqueness scope.** A new code (via Rename, or a vanity code at Create) must be unique
  against both current ShortCodes and all Retired codes — not just the current-code index.
