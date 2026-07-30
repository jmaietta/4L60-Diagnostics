# 0009 — Session analysis is descriptive until a baseline is verified

## Decision

The acquisition and replay paths retain every checksum-valid A276 Message 1 transmission observation with its monotonic timestamp. Analysis may calculate observed ranges and record command-state transitions without classifying them as normal, abnormal, pass, or fail.

Condition-matched comparisons are implemented behind a verification gate. An unverified baseline always returns `NotEvaluated`; it cannot produce a variance-based diagnostic conclusion.

## Consequences

- Simulator and replay sessions can exercise timelines, reports, and event detection deterministically.
- Corrected definitions can re-decode preserved raw sessions.
- Active continuous vehicle polling remains disabled until its Roadmaster-specific cadence and communications-restoration behavior are validated.
