# Protocol evidence register

## Evidence state

Phase 2 documentary implementation is present. Generic envelope/checksum behavior is corroborated by two sources. A276 explicitly names 1994 5.7L LT1 VIN P `1,4B 6D` cars, ALDL pin M, and 8192 baud, but the archive provides no publisher/version/license provenance. Consequently, the A276 definition is `Unverified`, `productionEligible: false`, and target-vehicle capture verification remains pending.

No captured Roadmaster frame is currently classified as verified vehicle evidence.

## Source identities

| Source | Identity | Local artifact/hash | Reviewed |
| --- | --- | --- | --- |
| `LT1-DATASTREAM-DEFINITIONS-2026-07-30` | ECMHack LT1 Datastream Definitions archive; `A276.DS` | Ignored `references/LT1-Datastream-Definitions.zip`; SHA-256 `77E49889CED5D9C93B6ED48B7401AB47658C90DDFA76D75499DE856CB5D961E6` | Codex, 2026-07-30 |
| `ECMHACK-ALDL-COMMS-2026-07-30` | ECMHack, “ALDL Communications,” downloaded/inspected 2026-07-30 | `https://ecmhack.com/misc/ee-aldl-communications/` | Codex, 2026-07-30 |
| `GM-4L60E-TECH-GUIDE-1992` | GM Powertrain Division, *Hydra-matic 4L60-E Technician's Guide*, copyright 1992 | User-supplied ignored PDF; SHA-256 `1D0F715F3BB02CF3B53BF3F94FCAE673619AA57131AC414AE19F9993166CC027` | Codex, 2026-07-30 |
| `PORTAL-DIAGNOSTOV-1994-ROADMASTER-CODES-2026-07-30` | Online reproduction of *1994 Buick Roadmaster - G - Tests W/Codes - 5.7L* | External page only; no local copyrighted copy retained | Codex, 2026-07-30 |
| `TUNERPRO-EE-AUTO-2010` | Robert Saar, TunerPro `EE_Auto.adx`, written 2010-08-11 | Ignored reference; SHA-256 `C70587A6B9C027126F576E95D08AC7255B9BF0191480E114AF980C2073CE63E1` | Codex, 2026-07-30 |

The GM guide supports transmission function and the solenoid/gear truth table only. It does not support byte-level ALDL definitions.

The Portal-Diagnostov page corroborates the application-specific DTC titles represented in A276 (including transmission Codes 28, 58/59, 72/73, 75, 79-85, and 90) and supplies candidate diagnostic aids. It does not describe frame structure, data offsets, or scaling. It is therefore registered for future DTC/commentary work and cannot promote the A276 protocol definition by itself.

Mike Shimniok's *Decoding GM's ALDL with Teensy 3.6* was reviewed and explicitly excluded as protocol evidence. It concerns a `1227747` ECM transmitting a 160-baud pulse-width-encoded, 9-bit stream. The Roadmaster A276 definition specifies 8192 baud and a different framed request/response protocol. Only the article's general practice of comparing a raw logic-analyzer capture with an independent decoder/reference tool carries over to the planned vehicle-verification procedure.

The TunerPro `$EE` acquisition definition independently agrees with A276 on 8192 baud, exact Mode 1 request bytes/checksums for Messages 0, 1, and 2, their response payload sizes, and the implemented core Message 1 transmission offsets and conversions. It is a community-authored Y/F-body definition rather than a source-identified GM Roadmaster document. An archived Gearhead-EFI thread documents later B-body use and shows that B-body connection behavior may require silencing additional modules. This strongly corroborates the decoder but does not replace a target-car communication test.

There is one material cross-source ambiguity: A276 labels Message 1 words 22-23 both an "absolute value" and "signed" slip value. TunerPro treats it as unsigned; EEHack marks it signed but uses its own offset-style signed conversion and contains a different multiplier spelling. Until a captured raw value is compared with a trusted scanner or independently calculated engine/input speed difference, the application must preserve the raw word and must not use this field for a production conclusion.

## Implemented framing and requests

| Implemented fact | Value | Evidence |
| --- | --- | --- |
| A276 target device address | `0xF4` | `A276.DS`, every documented request/response header |
| Data pin and baud rate | Pin `M`, 8192 baud | `A276.DS`, specification header |
| Envelope | device, encoded length, mode, payload, checksum | `A276.DS`, Modes 0/1/7/8/9/10; ECMHack “ALDL Message Format” |
| Encoded-length bias | Total frame length plus decimal 82 (`0x52`) | A276 request/response lengths independently agree with ECMHack “LENGTH” description |
| Checksum | Modulo-256 sum of all bytes including checksum equals zero; builder uses two's-complement of the preceding-byte sum | ECMHack “CHECKSUM”; independently validated against every A276 request layout |
| Mode 1 request | `F4 57 01 <dataset> <checksum>` | `A276.DS`, Mode 1 Messages 0/1/2/4/6 |
| Mode 1 response sizes | Dataset 0: 60 data bytes; 1: 46; 2: 53; 4: 45; 6: 38 | `A276.DS`, Mode 1 response descriptions |
| Disable/enable normal communications | Modes `0x08` and `0x09`, four-byte request/ack envelope | `A276.DS`, Modes 8 and 9; ECMHack chatter-control descriptions |
| Return to normal mode | Mode `0x00`, four-byte request | `A276.DS`, Mode 0 |

No chatter-renewal interval is implemented because neither reviewed source provides a target-specific duration. Scheduling periods must be explicitly supplied; the code has no fabricated default.

## Implemented read-only snapshot sequence

The desktop connection path now runs the bounded sequence recorded in ADR 0006. It preserves initial traffic, requires a checksum-valid `F4` observation before transmitting, sends `F4` Mode 8, requests Message 4 and Message 1, then attempts `F4` Mode 9 and Mode 0 restoration. It does not transmit to other observed addresses. This is narrower than community reports that some B-body tools silence additional modules because the reviewed evidence does not yet establish the exact safe command/address set for this vehicle.

Exact-echo handling is conservative: only the complete expected request is removed. Because the documented four-byte Mode 8/9 acknowledgement is byte-identical to the request, a single matching frame may be an adapter echo or an acknowledgement and is retained as an explicit ambiguity. Simulator/replay tests cover exact echo, dropped replies, corrupted checksums, raw recording, response correlation, and restoration after failure.

The one-shot snapshot does not establish a continuous polling or chatter-renewal cadence. That remains pending target-cable/vehicle observation.

## Implemented A276 Mode 1 Message 1 fields

Offsets below are zero-based within the 46 data bytes, not within the full frame.

| Offset | Source words | Field | Implemented conversion |
| --- | --- | --- | --- |
| 0 | 1 | Logged malfunction word 7 / DTC 24 bit | Bit map exactly as A276 |
| 2 | 3 | Logged malfunction word 9 / DTCs 76, 75, 74, 73, 72, 59, 58, 28 | Bit map exactly as A276 |
| 3 | 4 | Logged malfunction word 10 / DTCs 86 through 79 | Bit map exactly as A276 |
| 4 | 5 | Logged malfunction word 11 / DTCs 94, 93, 92, 91, 90, 89, 87 | Bit map exactly as A276; source bit 6 is unused |
| 5 | 6 | TPS input | volts = `5N/255` |
| 6 | 7 | Non-defaulted throttle position | raw counts |
| 7–8 | 8–9 | Filtered engine speed | big-endian `N/8` rpm |
| 9 | 10 | Filtered vehicle speed | `N/2` mph |
| 10 | 11 | Current torque signal pressure | `N` psi; not represented as measured line pressure |
| 11 | 12 | Force-motor reference current | `N/51.2` A |
| 12 | 13 | Force-motor actual current | `N/51.2` A |
| 13 | 14 | Force-motor duty cycle | `N/2.55` percent |
| 14 | 15 | Range flags | A276 bit map |
| 15 | 16 | Transmission ignition voltage | `N/10` V |
| 16 | 17 | Commanded gear | `N+1` |
| 19 | 20 | Latest 1–2 shift error | `N/40` s |
| 20 | 21 | Latest 2–3 shift error | `N/40` s |
| 21–22 | 22–23 | Slip source value | signed big-endian `N/8` rpm; A276's simultaneous “absolute” and “signed” wording is recorded as a capture-verification ambiguity |
| 23 | 24 | Latest 1–2 shift time | `N/40` s |
| 24 | 25 | Latest 2–3 shift time | `N/40` s |
| 25–26 | 26–27 | Transmission PROM ID | big-endian `N` |
| 27 | 28 | 3–2 PWM solenoid duty cycle | `N/2.55` percent |
| 29–30 | 30–31 | Raw output speed | big-endian `N/8` rpm |
| 31 | 32 | Filtered coolant temperature | `0.75N-40` °C |
| 32 | 33 | Transmission-fluid temperature | `0.75N-40` °C |
| 33–34 | 34–35 | TCC PWM duty cycle | big-endian `N/655.36` percent |
| 37 | 38 | TCC control/enable and shift-solenoid A/B commands | A276 bit map |
| 43 | 44 | Start/end-of-shift, motion, ignition-off status | A276 bit map |

The versioned machine-readable definition is `definitions/protocol/a276-1994-bd-lt1.unverified.json`. The source-title DTC flag catalog is `definitions/dtc-catalogs/a276-mode1-message1-transmission.unverified.json`. Neither can produce a production diagnostic conclusion.

## Simulator and golden data

The simulator constructs synthetic data through the production ALDL frame builder and emits valid documentary A276 envelopes. Synthetic values remain test-only and `ContainsVerifiedProtocolData` remains false. The bad-checksum scenario deliberately corrupts one built frame and is rejected by the production parser.

`testdata/golden/a276-documentary-vectors.json` contains exact A276 request vectors. Its evidence status is `DocumentaryOnlyVehicleCapturePending`; it is not represented as a vehicle capture.

## Phase 2 evidence gate

Before a protocol entry is promoted to vehicle-verified, record:

- Source identifier, title, publisher/author, version/date, and acquisition URL or physical-document identity
- Local reference path and cryptographic hash where redistribution permits storage
- Page, section, table, or source line that supports the definition
- Independent corroboration or vehicle/reference-tool capture
- Verification reviewer and date

No entry may be promoted from `Unverified` to `Verified` based solely on a placeholder, example, or simulator output.

## Documentary DTC explanation slice

The application now carries source-referenced, paraphrased definitions for transmission Codes 28, 58, 59, 72, 73, 75, 79, 80, 81, 82, 84, 85, and 90. These definitions use the already registered application-specific Roadmaster diagnostic reproduction for meaning, criteria where stated, circuit/mechanical cause ordering, and the first check. The compiled A276 flag location remains a separate protocol fact.

Every definition is `Unverified` and `productionEligible: false`. The loader enforces that gate, the UI labels the explanations as awaiting vehicle validation, and missing codes use a non-diagnostic fallback. No copyrighted repair prose or diagrams are committed.

## Remaining promotion evidence

- Identify the paper factory manual by exact cover title, form number, revision, and applicable pages.
- Record KOEO and idle raw sessions from the target Roadmaster and exact cable.
- Compare requests, response sizes, decoded values, and DTC status against a trusted reference scanner.
- Record PCM calibration/PROM identity when available.
- Resolve the signed-versus-absolute slip wording and cable echo behavior.
