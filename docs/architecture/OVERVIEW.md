# Architecture overview

LT1Diagnostics uses clean boundaries around immutable domain concepts and byte-preserving acquisition. Platform and UI details point inward; domain and analysis code never point outward to Avalonia, serial ports, or operating-system APIs.

| Project | Responsibility | Allowed production dependencies |
| --- | --- | --- |
| Domain | Definitions, provenance, connection/data-quality models | None |
| Protocol | ALDL framing, incremental parsing, A276 requests/decoding, echo filtering, response correlation, and explicit schedules | Domain |
| Transport | Transport contracts, serial and replay implementations | Domain |
| Acquisition | Recording, raw-session I/O, replay projection, protocol-health monitoring, and the bounded read-only A276 snapshot coordinator | Domain, Protocol, Transport |
| Analysis | Descriptive transmission events, observed ranges, and verification-gated baseline comparison | Domain |
| Knowledge | Versioned DTC catalogs and deterministic source-backed explanations | Domain |
| Reporting | Deterministic HTML reports and CSV measurement export | Domain, Analysis, Knowledge |
| Simulator | Deterministic transport-level scenarios built through the production frame builder | Domain, Protocol, Transport |
| App | Avalonia composition root and UI | All required feature projects |

The dependency graph is acyclic. In particular, replay accepts transport chunks and acquisition projects raw-session records into those chunks, avoiding an Acquisition↔Transport cycle.

Desktop replay uses the same versioned raw reader, replay transport, and ALDL parser as automated replay tests. The Avalonia layer supplies only the native file picker and presentation state; file validation and byte decoding remain in Acquisition and Protocol.

## Raw-session invariants

- File preamble and records are explicitly versioned.
- Records append to the stream and are never rewritten.
- Each record has a header CRC-32 and payload CRC-32.
- Unknown record type IDs remain readable.
- Source-reported corruption and storage-integrity failure are separate facts.
- Truncated final headers/payloads are returned as flagged records when possible, allowing recovery after interrupted writes.
- Raw bytes remain independent of current signal definitions, enabling future re-decoding.

## Definition eligibility gate

Schemas require `verificationStatus` and `productionEligible`. Unverified definitions are constrained to `productionEligible: false`; unverified tests and commentary are also disabled. Application code must enforce the same rule when definition loading is added.

The Phase 2 definition loader enforces the same gate at runtime. It can decode and expose documentary values for engineering validation while marking every result ineligible for diagnostic conclusions until its manifest is verified. Raw bytes remain the authority and can be re-decoded with a corrected manifest.

## Platform boundary

`System.IO.Ports` appears only in the Transport project. The UI discovers transports through `ITransport`; analysis and domain projects contain no OS-specific or Avalonia dependencies.

## Live acquisition boundary

The desktop path records before it transmits. Its current workflow observes checksum-valid bus traffic, requires an observed `F4` PCM, sends only documentary `F4` communications-management and Mode 1 requests, correlates Message 4 and Message 1 responses, and attempts Mode 9/Mode 0 restoration. Other observed module addresses are evidence, not a transmission allowlist. Continuous polling is intentionally deferred because no target-specific renewal/cadence evidence has been accepted.

## Analysis and reporting boundary

Acquisition and replay preserve all valid Message 1 observations with their monotonic timestamps. The App maps decoded observations into Domain models; Analysis does not reference Protocol, Transport, Avalonia, or platform APIs. Reports accept immutable Domain/Analysis data and generate deterministic HTML or CSV. Reports can describe recorded values, data quality, command transitions, and documentary DTC information, but the baseline gate prevents an unverified range from producing a normal/abnormal conclusion.
