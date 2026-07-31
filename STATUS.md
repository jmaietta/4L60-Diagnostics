# Project status

Last updated: 2026-07-30

## Current milestone

Phase 0 and Phase 1 are complete. The implementable pre-vehicle work is build-clean under the product name **4L60 Diagnostics**: the desktop serial path creates a raw session, observes the bus, requires a checksum-valid `F4` PCM, runs a bounded diagnostic Message 4/Message 1 snapshot, correlates replies, and attempts Mode 9/Mode 0 restoration. Multi-sample replay, a deterministic drive-sequence demo, descriptive transmission timelines, source-backed DTC explanations, verification-gated variance logic, HTML reports, CSV export, and self-contained Windows/Linux installer packages are now implemented. Vehicle promotion and sustained live polling remain pending the target 1994 Roadmaster and a reference scanner/cable.

The application remains read-only. It does not implement PCM flashing or forced gear, TCC, line-pressure, timing, or injector controls.

## Implemented

### Foundation and desktop application

- .NET 10 solution with Avalonia 12 desktop shell, centrally pinned dependencies, strict compiler/analyzer settings, and enforced project dependency boundaries
- Modern cross-platform desktop workspace with Segoe UI Variable on Windows and a cross-platform fallback, a 15px body-text baseline, high-contrast typography, aligned status grids, plain-English diagnostic-cable instructions, fixed custom button templates, complete page navigation, serial-device discovery, Connect/Disconnect lifecycle, cancellation, fault display, acquisition progress, quality metrics, and raw-session filename display
- Connection UX now removes the non-actionable `SELECT` label, hides Connect until a cable exists, visibly distinguishes disabled actions, and reports found/not-found/failed cable scans in plain English
- Built-in first-run tutorial with a one-click guided simulator demo, persistent loading feedback, an explicit successful-demo banner, basic workflow, Roadmaster connection checklist, status glossary, screen guide, and visible safety boundary
- Clickable Windows desktop shortcut targeting the latest self-contained Windows review build; the user does not need a system-wide .NET installation or terminal command to launch it
- Cross-platform serial transport, deterministic simulator transport, replay transport, and initial `ITransport` abstraction
- Versioned, append-only, checksummed raw-session format preserving raw bytes and provenance, with recording/replay and malformed-input coverage
- Functional Saved Sessions workflow: local capture listing, native `.lt1raw` file picker, selected-session replay, storage-integrity reporting, corrupt-record exclusion, production-parser re-decoding, and automatic routing to recovered measurements
- Multi-sample session history with timestamps, a readable transmission timeline, observed RPM/speed/slip/temperature ranges, and descriptive commanded-gear/TCC state changes
- Deterministic HTML diagnostic report and CSV measurement export through native save dialogs; reports keep the raw-session provenance and validation warning visible
- Self-contained Windows package with a double-click per-user installer, Desktop/Start-menu shortcuts, uninstall entry, no administrator requirement, and no system-wide .NET installation
- Self-contained Linux x64 package with a per-user `install.sh`, application-menu entry, `~/.local/bin/4l60-diagnostics` launcher, uninstaller, no root requirement, and no system-wide .NET installation; native install/uninstall smoke is enforced in Ubuntu CI
- Copyable Windows and Linux installer links in the app, with visible selectable URLs and user-first README installation steps that do not require navigating GitHub's release interface
- Connection and data-quality domain models without fabricated thresholds
- Windows/Linux scripts, Windows/Ubuntu CI, self-contained packaging configuration, schemas, architecture records, source/evidence registers, and notices

### Phase 2 protocol work

- Incremental 8192-baud ALDL envelope parser with bounded buffering, fragmentation/noise recovery, encoded-length validation, modulo-256 checksum validation, and raw-byte preservation
- Exact-echo filter that removes only a complete expected transmit echo and preserves mismatched or expired tentative bytes
- A276 request builder for Mode 1 datasets 0, 1, 2, 4, and 6, plus documented Modes 0, 8, and 9
- Explicit request scheduling and response correlation; no invented chatter-renewal interval or polling defaults
- Bounded read-only acquisition coordinator that preserves initial chatter, discovers checksum-valid module addresses, requires observed `F4` before transmission, sends only documented `F4` requests, and restores communications after success, timeout, or malformed input
- Conservative exact-echo/acknowledgement handling, including an explicit ambiguous classification when a byte-identical Mode 8/9 frame cannot be distinguished from cable echo
- Deterministic interactive snapshot simulator with success, absent-PCM, dropped-response, and corrupted-response behavior
- Typed decoder for the documented A276 Mode 1 Message 1 transmission fields and logged DTC flag words
- Versioned JSON protocol manifest loader, definition validation, generic data-driven decoder, and production-eligibility gate
- Machine-readable unverified A276 definition and transmission DTC flag catalog
- Deterministic simulator frames generated through the production frame builder, including a deliberately malformed checksum scenario
- Documentary golden request vectors and parser/replay/simulator integration coverage
- UI link monitor wired to the acquisition result, including raw byte/chunk totals, valid/invalid frame counts, observed addresses, protocol state, and restoration status
- Source-backed, paraphrased DTC knowledge definitions for Codes 28, 58, 59, 72, 73, 75, 79, 80, 81, 82, 84, 85, and 90, including plain-English meaning, ranked causes, and the first discriminating check
- Runtime DTC loader with an enforced unverified/production-ineligible gate and an honest fallback for codes without an explanation
- Condition-matched baseline model and variance evaluator; unverified baselines return `NotEvaluated` and cannot produce a normal/abnormal conclusion

The simulator exercises the same coordinator, request builder, parser, correlator, recorder, and restoration path used by serial connections. Its bytes remain synthetic and must not be mistaken for a completed live Roadmaster validation.

## Evidence acquired

- `LT1-DATASTREAM-DEFINITIONS-2026-07-30`: contains `A276.DS`, which explicitly identifies 1994/1995 LT1 VIN P `1,4B 6D`, ALDL pin M, 8192 baud, requests, response sizes, byte fields, conversions, and DTC bits. Archive SHA-256: `77E49889CED5D9C93B6ED48B7401AB47658C90DDFA76D75499DE856CB5D961E6`. It contains no provenance/license file, so derived definitions remain `Unverified` and `productionEligible: false`.
- `EEHACK-SOURCE-4.9.3`: LGPL-3.0 license inspected; archive SHA-256 `41684A1D084D3E07DA80B6D40BAE0BB5CB1BB7AD12463C1105B0DCA68E3B894B`. No source copied.
- `ALDL-IO-SOURCE-1.6.2`: 3-clause BSD-style license inspected; archive SHA-256 `CBC2DB2607DE3F5FA4465F214EE352AF75D20223AB8B03576DCB0333D386831D`. No source copied.
- User-supplied *Hydra-matic 4L60-E Technician's Guide* PDF: SHA-256 `1D0F715F3BB02CF3B53BF3F94FCAE673619AA57131AC414AE19F9993166CC027`. It is useful mechanical/transmission evidence, not application-specific ALDL byte evidence.
- Portal-Diagnostov *1994 Buick Roadmaster - G - Tests W/Codes - 5.7L*: useful secondary evidence for application-specific DTC titles, diagnostic aids, and candidate warm-idle scan values. It does not contain ALDL byte layouts and lacks factory edition/page identity, so it cannot independently verify protocol fields or production baselines.
- Bot Thoughts *Decoding GM's ALDL with Teensy 3.6*: reviewed and registered as a non-applicable ALDL variant. It covers a `1227747` ECM's 160-baud pulse-width/9-bit stream, not this car's 8192-baud A276 protocol; only its capture-and-independent-comparison methodology is reusable.
- Robert Saar's TunerPro `EE_Auto.adx`: independently matches A276's 8192-baud Mode 1 request bytes, response sizes, and core Message 1 transmission offsets/conversions. It is a community Y/F-body definition, not a licensed Roadmaster factory artifact. SHA-256: `C70587A6B9C027126F576E95D08AC7255B9BF0191480E114AF980C2073CE63E1`.
- Archived Gearhead-EFI `$EE` work documents a later definition tested on B-body cars and the need to silence additional Roadmaster/Caprice bus modules. This is useful implementation evidence, but the exact attachment license is unknown and no content was imported.
- A 185-page third-party scan identifies itself as the 2007 ATSG *GM 4L60E/65E/70E & 4L80E/85E Diagnostic Code Book* (SHA-256 `4D8208E1DE9057F139E540886C1E861AED247C85228D43E551E6EA4B6D0BF6FC`). It is technically useful secondary DTC evidence, but no redistribution permission was established; the PDF and its prose are not committed.
- The user owns a paper factory manual. Its exact cover/form/revision and cited pages have not yet been photographed and registered.

See `docs/SOURCE_REGISTRY.md`, `PROTOCOL_EVIDENCE.md`, and `BASELINE_EVIDENCE.md` for the evidence boundary.

## Verification results

- Latest full Release solution test: 91 non-hardware tests passed, 0 failed.
- Totals by project: Domain 3, Analysis 4, Protocol 24, Replay 20, Transport 9, Knowledge 18, App/reporting integration 13 (91 non-hardware tests total).
- App tests cover the simulator lifecycle, every workspace navigation target, guided-demo routing through both the async API and the actual button command interface, retained multi-sample state, serial discovery/connection, actionable discovery failures, valid raw-session replay, malformed-file rejection, DTC explanation mapping, HTML/CSV generation, and report saving.
- One hardware test remains opt-in and is excluded by the normal `Category!=Hardware` filter.
- Final `dotnet format --verify-no-changes`: passed.
- Self-contained publishes pass for `win-x64` and `linux-x64`. The Windows archive is `artifacts/4L60-Diagnostics-win-x64.zip` with SHA-256 `88AEEC6AEEE58A84F2D8F6235A19457E12C73B57EBE11463DB22573DAFB6F5B6`. The Linux archive is `artifacts/4L60-Diagnostics-linux-x64.tar.gz` with SHA-256 `5C55F5724234BA724FA9E49EF4970B517D0DBD43FCC654BE1DE8178514A2ED11`.
- Windows five-second launch smoke passed for the renamed executable. Windows and Linux archive contents and installer syntax were validated locally; native Linux install/uninstall is an Ubuntu CI smoke test.
- Native Linux execution is unavailable on this Windows host because WSL is not installed; Ubuntu CI is the native Linux verification path.

## Known blockers and intentionally unverified items

- The target car will not be available for several days. No target-vehicle KOEO/idle capture or trusted-scanner comparison exists yet.
- The exact cable/adapter echo behavior and sustained 8192-baud behavior have not been measured on the vehicle.
- Community reports indicate extra B-body modules can affect acquisition, but the reviewed evidence does not yet establish a safe address-specific silence command for each module. The coordinator therefore observes every valid address but controls only the documented `F4` PCM.
- A276's transmission-slip wording is internally ambiguous (both absolute and signed); the decoder preserves the documented signed representation but marks it unverified pending capture.
- No target-specific chatter-disable renewal interval is evidenced or implemented.
- Sustained active live polling is intentionally not enabled. The repository can preserve and analyze multiple samples from simulator/replay data, but choosing a Roadmaster-safe polling/renewal cadence before measuring the real bus would be an invented protocol behavior and could interfere with other modules.
- No condition-matched normal ranges are production-eligible. The app must not issue diagnostic conclusions from the documentary or simulator values.
- The paper factory manual still needs exact bibliographic identity and targeted photographs of the relevant diagnostic pages; a complete scan is not required.
- The A276 archive lacks publisher/version/license provenance. It is a documentary seed, not sufficient evidence for `Verified` status.
- Cable-specific VID/PID coverage and udev automation remain disabled pending exact hardware evidence.
- The Windows installer is not code-signed, so Windows may display an Unknown Publisher/SmartScreen warning. Public distribution needs a signing certificate and release signing.
- Git remote `origin` is configured for the public repository `https://github.com/jmaietta/4L60-Diagnostics`, and the initial application is published on `main`. Git and GitHub CLI authentication both work when the Windows user keyring is available. The first CI run exposed runtime-specific NuGet lock-file contamination from local installer packaging; the lock files were regenerated portably and both package scripts now use runtime lock files under ignored `obj` directories so the error cannot recur.

## Plain-English evidence boundary

Three separate information sets are required, and no single manual supplies all three:

1. **Raw-byte translation:** which request to send and how each returned byte becomes RPM, temperature, shift time, DTC bits, and so on. A276 supplies this and independent LT1 tools substantially corroborate it. The implementation can be built now; the remaining Roadmaster-specific check is a short live capture, especially for multi-module silence/echo behavior and the ambiguous slip word.
2. **Diagnostic meaning:** what a code or abnormal value means, what fallback the PCM applies, likely circuit/mechanical causes, and the next discriminating test. The factory repair manual and its online reproductions supply this. They are useful even if they contain no packet offsets.
3. **Normal ranges:** what is acceptable under a specific temperature, throttle, load, gear, and shift condition. Factory DTC thresholds and test specifications can seed this, but rigorous condition-matched shift baselines may also require verified captures or a documented self/cohort baseline. The program must report "No verified baseline" where this evidence does not exist.

Therefore, the paper repair manual is not required to make the ALDL decoder function. It is required (or must be replaced by equivalent sourced repair information) to make the diagnostic explanations and next-test recommendations trustworthy. A real vehicle session is required once to validate that the software communicates correctly with this Roadmaster and to resolve the few field-level ambiguities; it is not required to continue implementation now.

## Exact next task

The next Phase 2 task is target-vehicle read-only verification. Record the exact cable identity and PCM/PROM identity; capture raw KOEO Message 4 and Message 1 exchanges plus a warm-idle session; compare request/response bytes, decoded values, and DTC status with a trusted reference scanner; identify actual module addresses and echo behavior; resolve the slip-word ambiguity; hash the raw session; and promote only matching fields into a new versioned verified definition.

The pre-vehicle DTC knowledge, timeline, variance gate, report/export, and Windows packaging slices are complete. The exact next engineering task remains target-vehicle read-only verification: identify the cable and PCM/PROM, record KOEO and warm-idle raw sessions, compare them against a trusted scanner, determine actual module/echo behavior, and then run a short controlled capture to establish a safe polling/renewal strategy. Only facts that match should be promoted to a verified definition.
