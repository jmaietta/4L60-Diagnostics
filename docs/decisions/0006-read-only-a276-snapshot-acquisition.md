# ADR 0006: Read-only A276 snapshot acquisition

Status: Accepted, 2026-07-30

## Context

The Roadmaster connection path must obtain documentary A276 data without inventing a polling cadence or controlling unrelated modules. The available evidence identifies the PCM at `F4`, documents Modes 8, 9, and 0 for communications management, and documents Mode 1 Message 4 and Message 1 request/response sizes. Exact cable echo behavior and the target car's complete module population remain vehicle-verification items.

## Decision

The first live workflow is a bounded, one-shot snapshot:

1. Start the append-only raw recording and preserve initial bus traffic.
2. Parse checksum-valid frames from any address, but transmit nothing unless an `F4` PCM frame was observed.
3. Send the documented `F4` Mode 8 request. Do not send control requests to other observed addresses without address-specific evidence.
4. Remove only an exact expected cable echo, then correlate Message 4 identity and Message 1 transmission replies by documentary payload length.
5. Attempt documented `F4` Mode 9 followed by Mode 0 whenever Mode 8 was sent, including timeout and malformed-response paths.
6. Expose incomplete acquisition honestly; do not decode it into a diagnostic conclusion.

The production defaults use a 250 ms initial observation window, the 400 ms response window found in the reviewed TunerPro acquisition definition, and a 100 ms engineering echo-classification window. These are explicit operational settings, not vehicle normal ranges or an invented continuous polling schedule.

## Consequences

- Every received and transmitted byte is replayable, including chatter, echoes, malformed data, and restoration requests.
- The application cannot accidentally silence an address merely because an all-address parser discovered it.
- A single snapshot works before vehicle validation, while continuous acquisition and chatter renewal remain deferred until their timing is evidenced.
- A Mode 8 acknowledgement identical to a cable echo may remain classified as ambiguous; successful correlated data can still be retained with that provenance.
- No clear-code, actuator, flash, forced-gear, TCC, pressure, timing, or injector command is introduced.
