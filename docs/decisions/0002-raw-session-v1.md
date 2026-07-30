# ADR 0002: Use an append-only framed binary raw-session format

Status: Accepted — 2026-07-29

Version 1 uses a fixed preamble and fixed-size per-record headers followed by opaque payload bytes. Header and payload CRC-32 values detect storage corruption. Type IDs and per-record versions permit forward-compatible readers. A bounded payload length prevents malformed files from causing unbounded allocation.

CRC-32 protects the storage envelope only. It is not an ALDL checksum implementation and says nothing about whether captured vehicle traffic is valid.

