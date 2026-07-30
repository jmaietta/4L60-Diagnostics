# Phase 2 evidence acquisition

The implementation can proceed without immediate vehicle access. Promotion from documentary support to verified Roadmaster behavior requires a small, targeted evidence set rather than a complete manual scan.

## Paper manual

First photograph the front cover, publication/form number, copyright/revision page, and table of contents. Then use the index to locate and photograph only the complete pages for:

- Data Link Connector/ALDL and scan-tool data lists for the 5.7L VIN P engine
- PCM identification, PROM/calibration identification, and connector information
- Transmission DTCs 24, 28, 58, 59, 72, 73, and 79–86
- Current versus history DTC behavior and clearing procedure
- 4L60-E pressure-switch, shift-solenoid, TCC, force-motor, TFT, and output-speed diagnosis
- Any chart defining enable criteria, failure criteria, fallback action, or expected scan-tool display values

Photograph each entire page square-on, in focus, with the page number visible. Do not crop away headers, footnotes, applicability notes, or continuation markers. Name files with the publication identity and printed page number. The raw photographs remain under ignored `references/`; the repository stores citations and hashes, not the copyrighted pages.

Online factory-manual reproductions can locate candidate pages and provide interim cross-checks. They do not remove the need to identify the exact paper publication/revision for final provenance.

## Vehicle capture when available

Record the exact vehicle, PCM identity when readable, cable model/chipset, operating system, application version, and definition version. Capture in this order:

1. Cable loopback/echo characterization off the vehicle when supported.
2. Key on, engine off, with passive traffic preserved before requests.
3. Mode 8 acknowledgement followed by A276 Mode 1 Message 4 identity response.
4. Repeated Mode 1 Message 1 transmission responses at KOEO.
5. Cold idle through warm idle without diagnostic conclusions enabled.
6. Stationary brake and range changes with normal safety precautions.
7. Comparison of decoded values and logged DTCs with a trusted reference scanner.

No forced gear, TCC, line-pressure, timing, injector, clearing, programming, or flashing command is part of Phase 2 validation.
