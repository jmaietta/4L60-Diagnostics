# 0008 — Documentary DTC knowledge remains visibly gated

## Decision

Application-specific repair information may populate deterministic, plain-English DTC explanations before the car is available, provided every definition is versioned, source-referenced, paraphrased, marked `Unverified`, and ineligible for production conclusions.

The runtime loader rejects an unverified definition that claims `productionEligible: true`. The UI and reports label documentary explanations as awaiting vehicle validation. A code identifies a detected condition; it is never presented as proof that a named part failed.

## Consequences

- Ranked causes and the first discriminating check can be implemented and tested now.
- Missing definitions receive an honest fallback instead of fabricated advice.
- Promotion requires applicable primary evidence or target-vehicle corroboration and a definition-version change.
