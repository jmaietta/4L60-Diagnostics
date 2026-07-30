# ADR 0001: Enforce an acyclic project dependency direction

Status: Accepted — 2026-07-29

Domain has no production project dependencies. Protocol, Transport, Analysis, and Knowledge may depend on Domain. Acquisition composes Domain, Protocol, and Transport. Reporting uses Domain, Analysis, and Knowledge. Simulator uses Domain and Transport. App is the composition root.

This prevents platform/UI details from contaminating diagnostic logic and makes live, simulated, and replay acquisition interchangeable.

