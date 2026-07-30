# ADR 0004: Keep Phase 1 simulator envelopes visibly non-production

Status: Accepted — 2026-07-29

Until verified ALDL framing exists, simulator scenarios emit a deterministic internal `SIM` envelope through `ITransport`. The envelope exercises transport, recording, corruption, disconnect, and replay behavior without inventing vehicle message IDs or checksums. Phase 2 will replace scenario payloads with verified/golden ALDL frames while retaining the same transport path.

