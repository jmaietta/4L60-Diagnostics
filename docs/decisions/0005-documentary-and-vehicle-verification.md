# ADR 0005: Separate documentary support from vehicle verification

Status: Accepted - 2026-07-30

Phase 2 protocol facts carry two independent questions: whether a source documents the value, and whether the value has been observed on the target Roadmaster/cable combination. The A276 specification explicitly identifies 1994 LT1 B/D-car use, pin M, 8192 baud, message address, request layouts, response lengths, fields, bits, and scaling. It is sufficient to implement deterministic candidate decoding and golden documentary vectors.

It is not sufficient to claim target-vehicle validation because the archive lacks publisher/version provenance and the car is temporarily unavailable. Therefore:

- Generic envelope, length, checksum, Mode 1, and Modes 8/9 behavior may be marked documented when A276 and the independent ECMHack communication notes agree.
- A276 Roadmaster fields remain `Unverified` and `productionEligible: false` until a raw capture and trusted reference tool agree.
- The parser always preserves and reports invalid/raw input regardless of definition eligibility.
- The UI may display protocol health from documented framing, but it must not generate production diagnostic conclusions from capture-pending definitions.
- Promotion to `Verified` requires an evidence-register update naming the capture, calibration identity when available, reference tool, reviewer, and date.

This permits useful implementation before vehicle access without weakening the evidence gate.
