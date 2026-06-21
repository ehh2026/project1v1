Runtime Tuning Panel Plan - Gaps Review

Identified issues to tighten before implementation:

1. The apply/reload flow does not specify how the panel reports field-level validation errors back to the user after a failed load or partial edit. The plan says Apply is disabled while invalid, but it does not define when or how error text is refreshed after config reload or toggling panel visibility.
2. The cache invalidation rules are a little ambiguous. The plan distinguishes `needsRecreate`, `assetVariantChanged`, and `compositePlanChanged`, but it does not spell out whether every composite-plan change should also force a geometry/hash refresh, or only the subset that affects layout geometry. That distinction matters for avoiding stale composite output.
3. The reload path says to "use the same apply/recreate decision path" after copying values, but it does not explicitly state whether reload should preserve the current panel edit state or overwrite it with disk values before validation completes.

Suggested follow-up:
- Add a short decision table for each tuning field, indicating whether it triggers marker recreation, marker repositioning, cache clearing, or no runtime action.
- Define the status/error behavior for apply, save, and reload so the UI feedback is deterministic.
