# ADR 0007: Replay saved sessions through the production raw reader and parser

Status: Accepted — 2026-07-30

The desktop application opens `.lt1raw` files through the versioned `RawSessionReader`, projects received-byte records into the read-only `ReplayTransport`, and sends replayed chunks through the production ALDL stream parser. Storage-corrupt records remain visible in integrity totals but are excluded from decoding. Replay never writes to a vehicle transport.

The Saved Sessions page lists local captures, accepts an external `.lt1raw` file through the native file picker, reports file and protocol integrity, and opens recovered measurements only when a complete A276 transmission snapshot exists. Replayed simulator data remains visibly labeled as demo data. Replayed vehicle data remains validation-pending until target-vehicle verification is complete.
