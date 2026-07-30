You are the lead engineer for a new cross-platform automotive diagnostic application.

Read BUILD_PLAN.md completely before making any changes. Treat it as the product and architecture specification.

The application will diagnose the 1994 Buick Roadmaster LT1 engine and rigorously test its 4L60E transmission over the 8192-baud ALDL interface. It must run on both Windows and Linux. It must measure test results against condition-matched normal ranges, quantify variances, explain every DTC and abnormal result in plain English, rank possible causes, and recommend the next discriminating test.

Begin with Phase 0 and Phase 1 only:

1. Inspect the current directory and report what already exists.
2. Create the repository and solution structure specified in BUILD_PLAN.md.
3. Use .NET 10 LTS, C#, and Avalonia 12.
4. Create a cross-platform Avalonia desktop shell.
5. Create the domain, protocol, transport, acquisition, analysis, knowledge, reporting, and simulator projects with correct references and strict dependency boundaries.
6. Implement the initial ITransport abstraction.
7. Implement a deterministic simulator transport and replay transport.
8. Implement a versioned append-only raw-session format with reader/writer tests.
9. Implement connection/data-quality metric domain models.
10. Add Windows and Linux build/test scripts.
11. Add CI for Windows and Ubuntu.
12. Add JSON schemas and clearly marked unverified placeholders for vehicle, signal, DTC, test, baseline, and commentary definitions.
13. Create and maintain:
    - STATUS.md
    - THIRD_PARTY_NOTICES.md
    - docs/architecture/OVERVIEW.md
    - docs/decisions/
    - PROTOCOL_EVIDENCE.md
    - BASELINE_EVIDENCE.md
14. Add unit tests, replay tests, malformed-input tests, and simulator scenarios.
15. Run the complete test suite and fix failures.

Critical constraints:

- Do not invent ALDL message IDs, byte offsets, bit definitions, scaling formulas, DTC criteria, or normal ranges.
- Do not copy EEHack or aldl-io source until you inspect and document the applicable license.
- Do not implement PCM flashing.
- Do not implement forced gear, TCC, line-pressure, timing, or injector controls.
- Do not bury platform-specific code in the domain, protocol, or analysis projects.
- Do not create fake production diagnostic conclusions from placeholder data.
- Preserve raw bytes and provenance so future definition corrections can re-decode old sessions.
- Keep the solution buildable on both Windows and Linux after every milestone.
- Use deterministic, testable code rather than an LLM for diagnostic commentary.

Before coding, give me a concise execution plan based on the actual directory contents. Then proceed autonomously through Phase 0 and Phase 1. When complete, provide:

- A summary of files created or changed
- Architecture decisions made
- Commands to build, test, and run on Windows
- Commands to build, test, and run on Linux
- Test results
- Known blockers
- The exact next recommended task for Phase 2
