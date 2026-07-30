# Coding standards

- Target `net10.0` and C# 14 with nullable reference types enabled.
- Treat compiler and analyzer warnings as errors; performance suggestions explicitly downgraded in `.editorconfig` are non-contractual.
- Prefer immutable records at domain boundaries and cancellation-aware async I/O.
- Preserve raw input and provenance before decoding or interpretation.
- Do not place UI, serial-port, filesystem, or operating-system dependencies in Domain or Analysis.
- Never encode unverified protocol constants, diagnostic criteria, or normal ranges in production code.
- Keep deterministic diagnostic behavior in versioned rules/templates; do not add an LLM runtime dependency.
- Add malformed/truncated input tests for every binary parser.

