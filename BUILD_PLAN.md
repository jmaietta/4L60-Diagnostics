# LT1 / 4L60E Cross-Platform Diagnostic Application
## Detailed Build Plan and Codex Handoff Specification

**Working project name:** `LT1Diagnostics`  
**Initial vehicle:** 1994 Buick Roadmaster Estate Wagon, 5.7L LT1, 4L60E  
**Primary platforms:** Windows 10/11 x64 and desktop Linux x64  
**Initial Linux target:** Linux Mint / Ubuntu / Debian using X11  
**Application framework:** .NET 10 LTS, C#, Avalonia 12  
**Document status:** Initial implementation specification  
**Primary objective:** Build a modern, understandable, evidence-based diagnostic application that rigorously evaluates the LT1 engine and especially the 4L60E transmission.

---

# 1. Executive Summary

The initial product is a cross-platform diagnostic instrument for 1994–1995 GM LT1 vehicles using the 8192-baud ALDL datastream. It will begin with the 1994 Buick Roadmaster Estate Wagon and its electronically controlled 4L60E transmission.

The application must not merely display raw parameters or trouble-code numbers. It must:

1. Acquire and preserve raw ALDL traffic.
2. Decode engine, transmission, electrical, and diagnostic parameters.
3. Run repeatable guided transmission tests.
4. Measure each test result against a condition-appropriate normal range.
5. Quantify the variance from normal.
6. Explain every code and abnormal result in plain English.
7. Rank likely causes without presenting inference as certainty.
8. Recommend the next test that best separates the likely causes.
9. Distinguish measured, calculated, inferred, and unavailable information.
10. Produce a readable diagnostic report with supporting evidence.

The application should become a modern equivalent of a GM Tech 1 plus a transmission analyst, but it must remain transparent about what the PCM can and cannot directly measure.

The first production milestone is deliberately read-only except for clearing DTCs. Active transmission commands will be implemented only after the passive acquisition and analysis system is validated.

---

# 2. Product Principles

## 2.1 Evidence before diagnosis

Every finding must be linked to evidence:

- Raw ALDL frame or frames
- Decoded signals
- Test conditions
- Time window
- Derived metric
- Baseline used
- Variance calculation
- DTC observation, when applicable

A conclusion such as “probable 1–2 clutch or hydraulic apply problem” must show why the application reached that conclusion.

## 2.2 No unexplained codes

Never present only:

> Code 73

Present:

> **DTC 73 — Pressure Control Solenoid Current Error**  
> The PCM commanded a pressure-control-solenoid current but observed a return current outside the expected relationship. GM may respond by commanding maximum line pressure, which can create harsh shifts. This code identifies an electrical/control discrepancy; it does not by itself prove the transmission is mechanically healthy or unhealthy.

The exact wording, code title, enable criteria, fail criteria, PCM fallback action, likely causes, and confirmatory tests must come from a versioned DTC knowledge base.

## 2.3 No universal “normal” number

Transmission behavior depends on:

- Vehicle profile
- Axle ratio
- Tire circumference
- Calibration ID
- Gear
- Throttle position
- Engine load
- Vehicle speed
- Fluid temperature
- Road grade
- TCC state
- Shift direction
- Whether the transmission is cold, warm, or heat-soaked

A normal range must therefore be selected or calculated for the actual operating condition. The application must not compare every 1–2 shift to one fixed number.

## 2.4 Separate facts from inference

Every result must carry a provenance label:

- **Measured:** Directly decoded from PCM data or an external sensor.
- **Calculated:** Computed from measured values using a documented formula.
- **Inferred:** Diagnostic interpretation based on measured/calculated evidence.
- **Reported by PCM:** A state or fault asserted by the PCM.
- **Not measured:** Required data is unavailable.
- **Insufficient data:** Data exists, but test validity requirements were not met.

## 2.5 Safe by default

The default application mode is passive. It must never unexpectedly:

- Command a gear
- Lock the torque converter
- Change line pressure
- Disable an injector
- Change ignition timing
- Enter programming mode
- Write a calibration

No PCM flashing is in the initial scope.

---

# 3. Scope

## 3.1 Version 1 scope

Version 1 must support:

- Windows and Linux
- FTDI-based USB-to-ALDL cable
- Automatic or assisted serial-device discovery
- 8192-baud ALDL framing and checksum validation
- 1994 Buick Roadmaster LT1 / 4L60E vehicle profile
- Connection health metrics
- Raw-frame recording
- Recorded-session playback
- Active and stored DTC reading
- DTC clearing with explicit confirmation
- Essential live engine data
- Essential live transmission data
- Guided passive transmission road tests
- Baseline and variance analysis
- Plain-English result commentary
- HTML and JSON diagnostic reports
- CSV export
- Unit, integration, replay, and hardware-in-the-loop tests
- A deterministic LT1 PCM simulator for development

## 3.2 Explicitly deferred

Defer until passive diagnostics are reliable:

- PCM flashing
- Calibration editing
- Automated tuning
- Forced gear selection
- Forced TCC lock/unlock
- Line-pressure manipulation
- Injector shutdown
- Timing manipulation
- Automated stall testing
- Mobile application
- Cloud accounts or telemetry
- Support for every 1994–1995 LT1 model
- Support for unrelated GM OBD-I controllers

## 3.3 Future vehicle expansion

After the Roadmaster profile is validated:

1. 1995 Buick Roadmaster
2. 1994–1995 Chevrolet Caprice LT1
3. 1994–1996 Impala SS, subject to protocol/profile validation
4. 1994–1995 F-body LT1
5. 1994–1995 Corvette LT1
6. Cadillac B/D-body derivatives only after bus-behavior testing

Vehicle-specific ALDL bus behavior must be modeled in profiles rather than hidden in transport code.

---

# 4. Source and Licensing Policy

## 4.1 Required reference materials

The project may use the following as technical references:

- EEHack feature and protocol documentation
- EEHack source archive
- LT1 Datastream Definitions archive
- `aldl-io` source archive and emulator
- 1994 Buick Roadmaster factory service manual
- GM powertrain and 4L60E diagnostic procedures
- Verified factory calibration and vehicle specifications
- FTDI driver documentation
- Official .NET and Avalonia documentation

## 4.2 Do not fabricate protocol definitions

Codex must not invent:

- ALDL message IDs
- Byte offsets
- Scaling formulas
- DTC bit positions
- Gear-command definitions
- Solenoid state encodings
- Factory pass/fail thresholds

When a definition has not been verified, create a typed placeholder marked `Unverified` and prevent it from being used for a production diagnostic conclusion.

## 4.3 License gate

Before copying any EEHack or `aldl-io` code:

1. Inspect the source archives for `LICENSE`, `COPYING`, headers, or other grant language.
2. Record the license and attribution requirements in `THIRD_PARTY_NOTICES.md`.
3. If reuse rights are unclear, do not copy implementation code.
4. Use public behavior and protocol documentation as references for a clean implementation.
5. Keep imported source archives outside the production source tree under `references/`.

The website calling a project “open source” is not sufficient by itself to determine redistribution terms.

---

# 5. User Experience

## 5.1 First-run workflow

1. Launch application.
2. Select or confirm:
   - 1994
   - Buick
   - Roadmaster Estate Wagon
   - 5.7L LT1
   - 4L60E
3. Application scans available serial/USB devices.
4. It identifies likely FTDI devices.
5. It displays connection instructions:
   - Connect cable
   - Use laptop battery power during initial testing
   - Turn ignition to RUN
   - Do not start engine until directed
6. Run connection self-test.
7. Display:
   - Device path
   - Baud rate
   - Packet success rate
   - Checksum failure rate
   - Timeout rate
   - Estimated sample rate
   - Bus-chatter state
8. Read DTC snapshot before any clearing.
9. Offer:
   - Quick health scan
   - Full transmission evaluation
   - Live data
   - Open recorded session

## 5.2 Test-result card

Every test result card must show:

- Test name
- Status: Normal / Watch / Abnormal / Severe / Invalid / Incomplete
- Observed result
- Expected result or range
- Variance from normal
- Sample count
- Repeatability
- Data-quality score
- DTCs observed during or after the test
- Plain-English interpretation
- Likely causes
- Recommended next test
- Expandable evidence view
- Baseline source and version

Example:

> **1–2 Shift — Abnormal**
>
> Observed shift duration: 0.91 seconds  
> Expected range for this load and temperature: 0.38–0.62 seconds  
> Variance: 0.29 seconds above the upper limit; 47% beyond the upper bound  
> RPM flare: 286 RPM  
> Repetitions: 4 valid shifts; abnormal pattern repeated 4/4 times  
> Data quality: 93/100  
>
> **Interpretation:** The PCM commanded the 1–2 shift normally, but the ratio transition was delayed and engine speed increased during the event. This pattern is more consistent with delayed hydraulic or friction-element application than with a missing shift command. Actual line pressure was not measured, so the application cannot yet distinguish weak hydraulic pressure from an internal apply leak.
>
> **Next test:** Repeat while recording external line pressure.

The expected range above is illustrative only and must not be shipped unless validated.

---

# 6. Diagnostic Data Model

Use immutable domain records where practical.

## 6.1 Core entities

### `VehicleProfile`

Fields:

- `ProfileId`
- `Manufacturer`
- `Year`
- `Model`
- `Body`
- `Engine`
- `Transmission`
- `PcmFamily`
- `CalibrationIds`
- `AldlDeviceAddress`
- `BusStrategy`
- `AxleRatioOptions`
- `DefaultAxleRatio`
- `TireSpecification`
- `TransmissionGearRatios`
- `SupportedSignals`
- `SupportedTests`
- `ProfileVersion`
- `SourceReferences`
- `VerificationStatus`

### `RawFrame`

Fields:

- `SessionId`
- `Sequence`
- `MonotonicTimestamp`
- `WallClockTimestamp`
- `Direction`
- `TransportId`
- `Bytes`
- `ChecksumExpected`
- `ChecksumObserved`
- `ChecksumValid`
- `EchoClassification`
- `RequestCorrelationId`
- `ParseStatus`
- `TransportDiagnostics`

Raw frames must never be overwritten by decoded data.

### `DecodedSample`

Fields:

- `SignalId`
- `Timestamp`
- `RawFrameSequence`
- `RawBytes`
- `RawNumericValue`
- `EngineeringValue`
- `Unit`
- `QualityFlags`
- `DefinitionVersion`

### `TestRun`

Fields:

- `TestRunId`
- `SessionId`
- `TestDefinitionId`
- `VehicleProfileId`
- `StartTime`
- `EndTime`
- `TestConditions`
- `ValidityResult`
- `MetricResults`
- `DtcObservations`
- `DiagnosticFindings`
- `OperatorNotes`

### `MetricResult`

Fields:

- `MetricId`
- `Provenance`
- `ObservedValue`
- `Unit`
- `ExpectedLow`
- `ExpectedHigh`
- `ExpectedNominal`
- `AbsoluteVariance`
- `PercentVarianceFromNominal`
- `PercentBeyondNearestBound`
- `NormalizedDeviation`
- `DurationOutsideRange`
- `SampleCount`
- `RepeatCount`
- `Repeatability`
- `Confidence`
- `Severity`
- `BaselineId`
- `EvidenceReferences`

### `DiagnosticFinding`

Fields:

- `FindingId`
- `System`
- `Title`
- `Severity`
- `Confidence`
- `PlainEnglishSummary`
- `TechnicalExplanation`
- `EvidenceFor`
- `EvidenceAgainst`
- `PossibleCauses`
- `NextTests`
- `Limitations`
- `RelatedDtcCodes`
- `SafetyImplications`

### `DtcDefinition`

Fields:

- `Code`
- `CodeFormat`
- `System`
- `Title`
- `PlainEnglishMeaning`
- `EnableCriteria`
- `FailureCriteria`
- `MaturityCriteria`
- `PcmFallbackAction`
- `DriverSymptoms`
- `LikelyCauses`
- `FalsePositiveConditions`
- `ConfirmatoryTests`
- `SafetyLevel`
- `SourceReferences`
- `DefinitionVersion`
- `VerificationStatus`

---

# 7. Baseline and Variance Engine

## 7.1 Baseline hierarchy

The engine must select the highest-quality applicable baseline in this order:

1. **Factory diagnostic threshold**
   - Explicitly defined by GM.
   - Used for code criteria or a specified service test.
2. **Calibration-derived expectation**
   - Derived from the actual PCM calibration, shift schedule, or commanded behavior.
3. **Vehicle-profile engineering range**
   - Verified for the specific vehicle/transmission configuration.
4. **Matched-condition cohort**
   - Future option based on validated known-good sessions from equivalent vehicles.
5. **Vehicle self-baseline**
   - A prior known-good session from the same vehicle.
6. **Internal-consistency baseline**
   - Derived from physical relationships, commanded versus observed states, repeated tests, or gear-ratio logic.

The result must state which baseline type was used.

## 7.2 Baseline dimensions

A baseline selector may include:

- Vehicle profile
- Calibration ID
- Gear or shift
- Throttle-position band
- Calculated engine-load band
- Vehicle-speed band
- Transmission-fluid-temperature band
- TCC commanded state
- Brake-switch state
- Road-test phase
- Direction of shift
- Cold/warm/hot state
- Axle ratio
- Tire circumference
- Data-acquisition rate
- Minimum sample count

## 7.3 Variance calculations

For a scalar expected range `[low, high]`:

- If `observed < low`, absolute out-of-range variance is `observed - low`.
- If `observed > high`, absolute out-of-range variance is `observed - high`.
- If inside range, out-of-range variance is zero.
- Percentage beyond bound is the absolute out-of-range variance divided by the absolute value of the nearest bound, when mathematically valid.
- Percentage from nominal is `(observed - nominal) / nominal`.
- Normalized deviation uses the distance outside the range divided by a configured tolerance width.

For time-series metrics also calculate:

- Minimum
- Maximum
- Mean
- Median
- Standard deviation
- Percentiles
- Time outside range
- Longest continuous excursion
- Number of excursions
- Slope
- Area outside range
- Event-to-event repeatability

Do not use percentage calculations when the denominator is zero or not meaningful. Return `NotApplicable` instead.

## 7.4 Initial severity model

Severity is not merely the size of a single variance. It should combine:

- Magnitude of deviation
- Persistence
- Repeatability
- Relevant DTC presence
- Safety relevance
- Data quality
- Whether multiple independent signals corroborate the result

Initial configurable categories:

- **Normal:** Within expected range with adequate data.
- **Watch:** Marginal variance, limited persistence, or uncertain baseline.
- **Abnormal:** Material and repeatable variance.
- **Severe:** Large variance, safety concern, protective PCM action, or likely immediate mechanical risk.
- **Invalid:** Test preconditions were violated.
- **Incomplete:** Required sample count or test phase was not achieved.

The numeric scoring function must live in a versioned configuration file, not be hard-coded in the UI.

## 7.5 Confidence model

Confidence must decrease when:

- Packet loss is high
- Sample rate is too low
- Required signals are missing
- Test conditions moved outside the valid envelope
- Only one event was captured
- A baseline is not factory-sourced
- Axle ratio or tire size is unknown
- The result depends on an unmeasured hydraulic value
- Different evidence conflicts

Confidence must increase when:

- The behavior repeats
- Independent signals agree
- A DTC and measured behavior corroborate one another
- External pressure data agrees with PCM command data
- The baseline is factory-sourced and condition-matched

---

# 8. Diagnostic Commentary Engine

## 8.1 Required commentary structure

For every abnormal test and every DTC, generate:

1. **What was observed**
2. **What normal would look like**
3. **How far the result differed**
4. **What the PCM reported**
5. **What the result means in plain English**
6. **Most likely causes**
7. **What this result does not prove**
8. **Best next test**
9. **Driving or mechanical risk**
10. **Evidence and source references**

## 8.2 Commentary generation strategy

Version 1 should use deterministic templates and a rules engine, not an LLM.

Reasons:

- Repeatability
- Offline operation
- Auditability
- No hallucinated repair advice
- Easier unit testing
- Clear regulatory and safety boundary

An optional LLM-generated narrative can be added later, but only from a structured, validated diagnostic object. The deterministic result remains the source of truth.

## 8.3 Cause ranking

A `CauseCandidate` should include:

- Cause
- System category
- Evidence supporting it
- Evidence contradicting it
- Required missing measurement
- Prior plausibility
- Confidence
- Discriminating next test

Avoid definitive language unless the test is directly conclusive.

Use:

- “Consistent with”
- “More likely than”
- “Cannot distinguish between”
- “Does not by itself prove”
- “Requires pressure measurement”
- “Electrical control appears normal”

Avoid:

- “The transmission needs rebuilding”
- “The pump is bad”
- “Replace the solenoid”

unless a validated diagnostic procedure establishes that conclusion.

## 8.4 Correlated-code analysis

The commentary engine must analyze code combinations.

Examples of future rule patterns:

- Pressure-control electrical DTC plus maximum-line-pressure symptoms
- Gear-ratio DTC plus a measured ratio failure during one specific shift
- TCC slip DTC plus sustained measured locked-slip variance
- TFT sensor DTC plus implausible temperature trace
- Multiple unrelated voltage-sensitive codes plus unstable system voltage

Each correlation rule must identify:

- Required codes
- Required signal behavior
- Conflicting evidence
- Resulting interpretation
- Next test

---

# 9. Transmission Test Suite

The test definitions must be data-driven. Each test includes:

- Preconditions
- Operator instructions
- Signals required
- Start trigger
- Stop trigger
- Invalidating conditions
- Metrics
- Baseline selector
- DTC snapshot points
- Safety constraints
- Commentary rules

## 9.1 Test 0 — Connection Integrity

**Purpose:** Prove that the diagnostic data is reliable enough to analyze.

Measure:

- Packet success rate
- Checksum failure rate
- Timeout rate
- Echo-removal accuracy
- Request/response latency
- Effective sample rate
- Longest acquisition gap
- Unexpected bus traffic
- Reconnect count

Invalid result:

- Data quality below configured minimum
- Excessive checksum failures
- Missing required messages
- Sample interval too slow for shift-event analysis

Commentary example:

> The cable connected, but the effective sample rate was too low to measure shift duration accurately. DTC reading remains usable; shift-timing conclusions are disabled for this session.

## 9.2 Test 1 — Key-On Engine-Off Preflight

Measure:

- Battery/system voltage
- Throttle position at rest
- Transmission-fluid-temperature plausibility
- Coolant-temperature plausibility
- Intake-air-temperature plausibility
- Range-switch state
- Brake-switch state
- Solenoid commands
- Active and stored DTCs

Variance checks:

- Sensor temperatures compared with one another after a long cold soak
- Throttle-position rest value versus validated range
- Voltage stability
- Range-switch agreement with selected gear

Interpretation must identify implausible sensor values before road testing.

## 9.3 Test 2 — Cold Start and Warm-Up

Measure:

- Transmission-fluid-temperature curve
- Coolant-temperature curve
- Voltage
- Idle speed
- TCC state
- Gear state
- DTC appearance
- Communication quality

Derived metrics:

- Temperature starting difference
- TFT rise rate
- ECT rise rate
- Sensor divergence
- Temperature plateaus or discontinuities
- Implausible jumps

Goal:

Detect a biased, intermittent, open, or shorted temperature input that would corrupt all later baseline comparisons.

## 9.4 Test 3 — Range and Brake Inputs

Operator cycles safely through:

- Park
- Reverse
- Neutral
- Drive
- Manual ranges supported by the vehicle

Measure:

- PCM range state
- Actual selector state entered by operator
- Brake-switch transitions
- Transition latency
- Intermittent or contradictory states

No vehicle movement is required for the initial range-input test.

## 9.5 Test 4 — Light-Throttle 1–2 Shift

Capture at least three valid repetitions within a configured throttle/load window.

Measure:

- Shift command timestamp
- Solenoid command transition
- Ratio-transition start
- Ratio-transition completion
- Shift duration
- Engine RPM flare
- Engine RPM drop
- Calculated ratio before/during/after
- Speed change
- Throttle stability
- TFT
- TCC state
- Pressure-control command/current when available
- DTCs before and after

Variance output:

- Each event versus matched baseline
- Mean event versus matched baseline
- Worst event
- Event-to-event coefficient of variation
- Percentage of events outside range

## 9.6 Test 5 — Moderate-Throttle 1–2 Shift

Same core analysis as Test 4, but use a higher validated load band.

The application must not assume the light-throttle baseline applies.

## 9.7 Test 6 — Light-Throttle 2–3 Shift

Measure the same event metrics, with shift-specific ratio and state logic.

Specific attention:

- Commanded versus achieved third gear
- Flare
- Tie-up
- Delayed ratio stabilization
- Post-shift slip
- DTC emergence

## 9.8 Test 7 — Moderate-Throttle 2–3 Shift

Repeat in a controlled, validated load band.

## 9.9 Test 8 — 3–4 Shift / Overdrive Apply

Measure:

- Command timing
- Ratio transition
- Shift duration
- Post-apply holding behavior
- TCC interaction
- Temperature
- Repeatability

The test must abort or invalidate if road speed, traffic, or throttle conditions make it unsafe or unrepeatable.

## 9.10 Test 9 — TCC Apply at Steady Cruise

Preconditions:

- Warm fluid
- Stable speed
- Stable throttle
- Appropriate gear
- Brake switch released
- TCC commanded on

Measure:

- Command-to-engagement delay
- Calculated slip before engagement
- Calculated slip after engagement
- Mean locked slip
- Peak locked slip
- Time above threshold
- Cycling or hunting
- Temperature effect
- DTCs

The application must identify whether the result is based on:

- A directly reported transmission input speed, if available
- A calculated input speed
- Engine-speed and ratio relationships

## 9.11 Test 10 — TCC Brake Release and Reapply

Operator lightly touches brake under safe conditions.

Measure:

- Brake-switch transition
- TCC release latency
- Engine-speed response
- Reapply eligibility
- Reapply command
- Reapply completion
- Unexpected cycling

## 9.12 Test 11 — Controlled Coastdown and Downshifts

Measure:

- Commanded downshift points
- Ratio transitions
- Harshness proxies
- RPM response
- TCC release
- Solenoid-state compliance
- DTCs

The application must not encourage aggressive manual downshifts.

## 9.13 Test 12 — Hot Repeatability

Repeat selected shift and TCC tests after the transmission reaches a validated warm or hot range.

Compare:

- Cold/warm versus hot shift duration
- Flare change
- Ratio stabilization
- TCC slip
- Pressure-command/current relationship
- External line pressure, when installed

This test is important for identifying temperature-sensitive leakage or pressure loss.

## 9.14 Test 13 — External Hydraulic Line-Pressure Test

Optional hardware:

- 4L60E pressure-port adapter
- Automotive-rated pressure transducer
- Protected signal-conditioning hardware
- USB DAQ or supported microcontroller interface

Measure:

- Actual line pressure
- Pressure-command signal/current
- Gear
- Throttle/load
- RPM
- Speed
- TFT
- Shift events

Analysis:

- Commanded-versus-actual pressure
- Pressure-rise latency
- Pressure during each shift
- Pressure loss when hot
- Pressure instability
- Pressure response by load
- Whether electrical command is normal while actual pressure is abnormal

The application must display that actual line pressure is **not measured** when no external transducer is connected.

## 9.15 Test 14 — Post-Test DTC and Change Summary

At the end of every guided sequence:

- Snapshot active and stored DTCs
- Compare with pre-test codes
- Identify newly matured codes
- Identify codes that changed status
- Associate each new code with the time window and operating conditions
- Explain each code
- Correlate codes with measured abnormalities

---

# 10. Gear-Ratio and Slip Analysis

## 10.1 Vehicle configuration dependencies

Calculated gear ratio requires verified:

- Transmission gear ratios
- Axle ratio
- Tire rolling circumference
- Vehicle-speed signal definition
- Engine-speed signal
- TCC state
- Converter-slip assumptions

The configuration screen must allow axle ratio and tire size confirmation. It should warn when defaults are being used.

## 10.2 Ratio state machine

Implement a state machine:

- `StableGear`
- `ShiftCommanded`
- `TransitionStarted`
- `Transitioning`
- `TransitionCompleted`
- `FailedToComplete`
- `RatioUnstable`
- `DataInvalid`

Do not calculate shift duration from a single RPM threshold. Use multiple signals and hysteresis.

## 10.3 Event validity

Invalidate or downgrade a shift event when:

- Throttle changes excessively
- Brake is applied
- Road speed is unstable before the event
- TCC state changes unexpectedly
- Packet gap overlaps the event
- Required signals are missing
- Shift is manually induced outside test design
- Wheel slip is suspected
- Calculated ratio is physically implausible

---

# 11. DTC System

## 11.1 Read behavior

Support:

- Current/active codes
- Stored/history codes
- Diagnostic flags
- Code snapshots
- Pre-test and post-test comparison
- Explicit clear operation

The application must capture and save all codes before offering to clear them.

## 11.2 DTC detail page

Display:

- Code
- Verified title
- Current/stored status
- First observed time in session
- Last observed time
- Number of observations
- Applicable operating conditions
- Plain-English meaning
- PCM detection logic
- PCM fallback action
- Expected driver symptoms
- Likely causes ranked by category
- What the code does not prove
- Recommended checks
- Related live-data graphs
- Source references
- Definition version

## 11.3 DTC knowledge-base format

Use JSON or YAML source files validated against a schema.

Example:

```yaml
code: "73"
system: "Transmission"
title: "Pressure Control Solenoid Current Error"
verificationStatus: "Verified"
plainEnglishMeaning: >
  The PCM detected that observed pressure-control-solenoid current did not
  agree with the commanded electrical state.
pcmFallbackAction:
  - "Use verified factory description here"
likelyCauses:
  - category: "Electrical"
    cause: "Open, short, resistance, connector, or wiring fault"
  - category: "Component"
    cause: "Pressure-control solenoid electrical fault"
confirmatoryTests:
  - "Compare commanded and returned current"
  - "Inspect circuit under load"
sourceReferences:
  - sourceId: "GM-FSM-1994-ROADMASTER"
    pageOrSection: "To be verified"
```

Do not ship `To be verified` entries as verified conclusions.

---

# 12. Software Architecture

## 12.1 Technology choices

- **Runtime:** .NET 10 LTS
- **Language:** C#
- **UI:** Avalonia 12
- **Pattern:** MVVM for UI, clean/hexagonal boundaries for core logic
- **Storage:** SQLite for indexed session metadata and findings
- **Raw logs:** Append-only binary framed format plus optional CSV/JSON export
- **Configuration:** Versioned JSON/YAML definitions
- **Tests:** xUnit or NUnit, chosen once and used consistently
- **Logging:** Structured local logs
- **Charts:** Cross-platform Avalonia-compatible charting library selected after a small performance spike
- **Packaging:** Self-contained Windows and Linux builds

Do not couple diagnostic logic to Avalonia view models.

## 12.2 Major layers

### Domain

Contains:

- Vehicle profiles
- Signal definitions
- Test definitions
- Baselines
- Metrics
- Findings
- DTC definitions
- Provenance
- Safety policy

No platform or UI dependencies.

### Protocol

Contains:

- ALDL frame construction
- Checksum
- Message definitions
- Request/response correlation
- Mode handling
- Dataset scheduling
- Parser
- Bus-master/chatter handling
- Echo handling

### Transport

Contains:

- `ITransport`
- Serial-port implementation
- Windows device enumeration
- Linux device enumeration
- Optional direct FTDI implementation
- Simulator transport
- Replay transport

### Acquisition

Contains:

- Message scheduler
- Sample clock
- Raw writer
- Decoding pipeline
- Data-quality monitor
- Reconnection strategy
- Backpressure handling

### Analysis

Contains:

- Test state machines
- Event detection
- Ratio analysis
- Slip analysis
- Temperature analysis
- Electrical correlation
- Baseline selection
- Variance engine
- Confidence engine
- Finding rules

### Knowledge

Contains:

- DTC catalog
- Commentary templates
- Cause rules
- Next-test rules
- Source registry
- Version validation

### Reporting

Contains:

- Report model
- HTML renderer
- JSON renderer
- CSV exporter
- Evidence links
- Graph snapshots

### UI

Contains:

- Setup
- Connection
- Dashboard
- DTC pages
- Guided tests
- Result cards
- Timeline
- Graphing
- Report viewer
- Settings

---

# 13. Proposed Repository Structure

```text
LT1Diagnostics/
├── LT1Diagnostics.sln
├── README.md
├── BUILD_PLAN.md
├── LICENSE
├── THIRD_PARTY_NOTICES.md
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── docs/
│   ├── architecture/
│   ├── protocol/
│   ├── diagnostics/
│   ├── safety/
│   └── decisions/
├── references/
│   ├── README.md
│   └── .gitignore
├── definitions/
│   ├── vehicles/
│   ├── signals/
│   ├── dtcs/
│   ├── tests/
│   ├── baselines/
│   ├── commentary/
│   └── schemas/
├── src/
│   ├── LT1Diagnostics.App/
│   ├── LT1Diagnostics.Domain/
│   ├── LT1Diagnostics.Protocol/
│   ├── LT1Diagnostics.Transport/
│   ├── LT1Diagnostics.Acquisition/
│   ├── LT1Diagnostics.Analysis/
│   ├── LT1Diagnostics.Knowledge/
│   ├── LT1Diagnostics.Reporting/
│   └── LT1Diagnostics.Simulator/
├── tests/
│   ├── LT1Diagnostics.Domain.Tests/
│   ├── LT1Diagnostics.Protocol.Tests/
│   ├── LT1Diagnostics.Transport.Tests/
│   ├── LT1Diagnostics.Analysis.Tests/
│   ├── LT1Diagnostics.Knowledge.Tests/
│   ├── LT1Diagnostics.Replay.Tests/
│   └── LT1Diagnostics.Hardware.Tests/
├── testdata/
│   ├── synthetic/
│   ├── recorded/
│   ├── golden/
│   └── malformed/
├── packaging/
│   ├── windows/
│   └── linux/
└── scripts/
    ├── build.ps1
    ├── build.sh
    ├── test.ps1
    └── test.sh
```

---

# 14. Transport Design

## 14.1 Transport interface

```csharp
public interface ITransport : IAsyncDisposable
{
    string TransportId { get; }
    TransportCapabilities Capabilities { get; }

    Task<IReadOnlyList<TransportDevice>> DiscoverAsync(
        CancellationToken cancellationToken);

    Task ConnectAsync(
        TransportDevice device,
        TransportSettings settings,
        CancellationToken cancellationToken);

    ValueTask WriteAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken);

    IAsyncEnumerable<TransportChunk> ReadAllAsync(
        CancellationToken cancellationToken);

    Task DisconnectAsync(CancellationToken cancellationToken);
}
```

## 14.2 Serial implementation

Begin with `System.IO.Ports` behind the interface, but do not let the rest of the application depend on it directly.

Requirements:

- 8192 baud support verification on Windows and Linux
- Byte-oriented reads and writes
- Monotonic timestamps
- Explicit read/write timeouts
- Cancellation
- Reconnect
- Device removal detection
- Input purge
- Echo classification
- No text encoding
- No `ReadLine`
- No UI-thread I/O

## 14.3 Direct FTDI fallback

If virtual serial-port timing is inadequate, add a direct FTDI/libftdi or D2XX implementation behind the same interface.

Do not blacklist Linux kernel FTDI drivers automatically. Direct mode should be an explicit advanced installation path.

## 14.4 Linux permissions

Provide a documented `udev` rule and an installer script that:

- Identifies supported FTDI vendor/product IDs
- Grants access to a dedicated group
- Does not require running the application as root
- Avoids globally insecure device permissions

## 14.5 Timing and quality instrumentation

Every read/write operation should record:

- Queued time
- Write start/end
- First response byte
- Last response byte
- Frame completion
- Timeout
- Retry
- Reconnect

This instrumentation is required to distinguish a vehicle problem from a communications problem.

---

# 15. Raw Log Format

## 15.1 Requirements

The native raw format must be:

- Append-only
- Crash-tolerant
- Versioned
- Checksummed
- Efficient
- Replayable
- Independent of current signal definitions
- Able to preserve invalid frames
- Able to preserve transport events and operator markers

## 15.2 Record types

- Session header
- Vehicle profile snapshot
- Transport connected
- Transport disconnected
- Bytes received
- Bytes transmitted
- Parsed frame
- Decode result
- DTC snapshot
- Operator marker
- Test state transition
- External sensor sample
- Application error
- Session footer

## 15.3 Replay

The replay transport must reproduce:

- Original timing
- Accelerated timing
- Step-by-step mode
- Deterministic event order
- Optional injected packet loss or corruption

All analysis must work identically on live and replay transports.

---

# 16. Simulator

Build a deterministic simulator before relying on the vehicle for every development cycle.

Simulator scenarios:

- Healthy idle
- Healthy road-test sequence
- Normal 1–2, 2–3, and 3–4 shifts
- Delayed 1–2 shift
- RPM flare
- Shift tie-up
- Failed 2–3 shift
- TCC excessive slip
- TCC cycling
- TFT sensor open/short/intermittent
- Pressure-control electrical fault
- Packet loss
- Bad checksum
- Serial echo
- Bus chatter
- Unexpected module messages
- Device disconnect and reconnect

The simulator must emit raw ALDL-like frames through `ITransport`, not bypass the parser.

---

# 17. UI Plan

## 17.1 Main navigation

- Home
- Connect
- Trouble Codes
- Live Data
- Transmission Evaluation
- Sessions
- Reports
- Vehicle Setup
- Settings
- About / Sources

## 17.2 Transmission evaluation workflow

Show one instruction at a time:

1. Safety and vehicle setup
2. Connection-quality validation
3. Initial DTC capture
4. KOEO preflight
5. Warm-up
6. Range/brake checks
7. Guided road-test stages
8. Optional pressure test
9. Final DTC capture
10. Analysis and report

The user should not need to understand ALDL message names.

## 17.3 Graphing

Support synchronized graph lanes for:

- Engine RPM
- Vehicle speed
- Calculated ratio
- Commanded gear
- Solenoid A/B
- TCC command
- Calculated TCC slip
- TFT
- Throttle/load
- Pressure-control command/current
- External line pressure
- DTC events
- Test-state markers

Allow clicking a finding to jump to the evidence interval.

---

# 18. Reporting

## 18.1 Human-readable report

Sections:

1. Vehicle and session
2. Hardware and software versions
3. Connection/data quality
4. DTC summary
5. Overall transmission assessment
6. Individual test results
7. Variances from normal
8. Correlated findings
9. Possible causes
10. Recommended next tests
11. Limitations
12. Evidence appendix
13. Sources and baseline versions

## 18.2 Machine-readable report

Export JSON containing:

- Raw identifiers
- Definitions used
- All metrics
- Baseline IDs
- Variance calculations
- DTC objects
- Findings
- Evidence references
- Version information

## 18.3 Report language

The report must never hide uncertainty.

Example:

> The 1–2 shift was 41% slower than the upper limit of the selected matched-condition baseline in four of four valid repetitions. The PCM’s shift-solenoid commands changed as expected. Because hydraulic pressure was not measured, the application cannot distinguish low available pressure from leakage within the apply circuit.

---

# 19. Safety Architecture

## 19.1 Modes

### Passive mode

Allowed:

- Read
- Decode
- Log
- Analyze
- Report

### Limited diagnostic mode

Allowed after explicit confirmation:

- Clear DTCs
- Request special diagnostic snapshots

### Expert active-test mode

Deferred. Future requirements:

- Separate enable setting
- Per-session acknowledgement
- Speed interlock
- Throttle interlock
- Brake-state interlock
- Gear/range interlock
- Temperature interlock
- Timeout
- Dead-man behavior
- Automatic return to PCM control
- Audit log
- Large visible active-command indicator

## 19.2 Road-test safety

The software must:

- State that a second person should operate the laptop during road testing
- Provide voice/audio cues later, but not require looking at the screen
- Allow tests to be aborted instantly
- Invalidate rather than pressure the user to complete an unsafe test
- Avoid aggressive acceleration requirements in Version 1
- Avoid stall-test automation
- Avoid forced downshifts

---

# 20. Testing Strategy

## 20.1 Unit tests

Required for:

- Checksum
- Frame length
- Parser state machine
- Echo removal
- Scaling formulas
- Units
- Baseline selection
- Variance math
- Confidence math
- Test state machines
- DTC lookup
- Commentary templates
- Cause ranking
- Report rendering

## 20.2 Property and fuzz tests

Use generated inputs for:

- Arbitrary byte streams
- Partial frames
- Back-to-back frames
- Invalid lengths
- Corrupt checksums
- Noise
- Echo
- Out-of-order data
- Extreme numeric values

The parser must never crash or silently reinterpret corrupt data as valid.

## 20.3 Golden-data tests

For each verified message definition:

- Raw input
- Expected decoded values
- Units
- Quality flags
- Definition version

For each diagnostic scenario:

- Recorded/synthetic session
- Expected test events
- Expected metrics
- Expected variance
- Expected finding
- Expected commentary fragments

## 20.4 Cross-platform CI

CI matrix:

- Windows x64
- Ubuntu x64
- Debug and Release
- Unit tests
- Replay tests
- Definition-schema validation
- Formatting/analyzer checks
- Self-contained publish smoke test

Hardware tests remain opt-in and run locally.

## 20.5 Vehicle validation

Validation progression:

1. Cable loopback/emulator
2. KOEO connection
3. Engine-idle capture
4. Stationary range/brake test
5. Low-risk road capture
6. Repeatable guided shifts
7. Compare against EEHack/known scanner
8. Compare DTC results
9. Optional pressure-transducer validation
10. Hot-repeatability testing

Do not enable production diagnostic labels until comparisons agree.

---

# 21. Build Phases and Acceptance Criteria

## Phase 0 — Repository and evidence setup

Deliver:

- Solution and project structure
- Build scripts
- CI
- Coding standards
- Architecture decisions
- Definition schemas
- Source registry
- License review document
- Placeholder vehicle profile marked unverified
- No invented protocol data

Acceptance:

- Builds and tests on Windows and Linux
- Empty Avalonia shell launches
- CI is green
- Definitions validate against schemas

## Phase 1 — Transport, raw capture, and simulator

Deliver:

- `ITransport`
- Serial transport
- Replay transport
- Simulator transport
- Raw log writer/reader
- Connection metrics
- Device enumeration
- Basic connection UI

Acceptance:

- Simulator session records and replays byte-for-byte
- Device disconnect does not crash app
- Corrupt input is preserved and flagged
- Windows and Linux builds pass

## Phase 2 — Verified ALDL protocol core

Deliver:

- Frame builder/parser
- Checksum
- Mode 1 request support
- Required bus control/chatter behavior
- Echo handling
- Request scheduler
- Verified Roadmaster message definitions
- Golden parser tests

Acceptance:

- Known reference frames decode exactly
- Invalid checksum never becomes a valid sample
- Parser survives fuzz testing
- Raw logs are sufficient to re-decode after definition changes

## Phase 3 — DTC reader and commentary

Deliver:

- Diagnostic snapshot request
- Current/stored code extraction
- Verified DTC catalog
- DTC detail UI
- Pre/post snapshots
- Deterministic commentary
- Clear-code workflow

Acceptance:

- Every displayed code has title, meaning, fallback action or explicit unknown, causes, and next tests
- No code displays as an unexplained integer
- Clear operation is impossible before saving a snapshot
- DTC results match a reference tool on the vehicle

## Phase 4 — Essential live data and data quality

Deliver:

- Live signal dashboard
- Signal-quality flags
- Session browser
- CSV/JSON export
- Synchronized graphs
- Connection-quality gating

Acceptance:

- Required signals display with correct units
- Dropouts are visible
- Test system refuses high-resolution analysis when acquisition quality is inadequate

## Phase 5 — Passive transmission event engine

Deliver:

- Gear-ratio model
- Shift state machine
- 1–2, 2–3, 3–4 event detection
- TCC state and slip analysis
- Temperature analysis
- Test validity rules
- Replay scenarios

Acceptance:

- Golden sessions produce deterministic event boundaries
- False shifts are not generated by ordinary RPM changes
- Invalid tests are labeled invalid rather than diagnosed

## Phase 6 — Baseline, variance, and findings

Deliver:

- Baseline repository
- Baseline selector
- Variance engine
- Severity/confidence engine
- Finding rules
- Commentary for every metric
- Evidence linking

Acceptance:

- Each test shows observed, expected, variance, quality, confidence, and interpretation
- Every baseline is versioned and sourced
- Missing baselines result in “No verified baseline,” not fabricated normality
- Results distinguish measured, calculated, and inferred facts

## Phase 7 — Guided full transmission evaluation

Deliver:

- Guided workflow
- Test instructions
- Precondition monitoring
- Test-state progress
- Automatic event collection
- Pre/post DTC comparison
- Full report

Acceptance:

- A user can complete a full passive evaluation without understanding raw ALDL fields
- Report includes all valid tests and clearly identifies missing tests
- Road-test stages can be safely skipped or aborted

## Phase 8 — External pressure integration

Deliver:

- External sensor interface abstraction
- Calibration procedure
- Pressure synchronization
- Pressure variance analysis
- Pressure-supported findings

Acceptance:

- Sensor timestamps align with ALDL timeline
- Disconnected or uncalibrated sensor cannot produce a pressure conclusion
- Report separates commanded electrical pressure control from measured hydraulic pressure

## Phase 9 — Active controls

Do not begin until all prior phases are validated.

Deliver only after a separate safety design review.

---

# 22. Initial Development Decisions

Unless evidence forces a change:

- Use .NET 10 LTS.
- Use Avalonia 12.
- Use a single cross-platform core.
- Use `System.IO.Ports` only behind `ITransport`.
- Use SQLite for metadata and append-only files for raw sessions.
- Use deterministic commentary in Version 1.
- Begin with passive testing.
- Treat Linux Mint/Ubuntu/Debian as first-class.
- Build simulator and replay before vehicle-dependent UI.
- Keep vehicle and test definitions data-driven.
- Do not copy unlicensed source code.
- Do not add cloud services.
- Do not add an LLM dependency.
- Do not add flashing.

---

# 23. Definition of Done for Version 1

Version 1 is done when:

1. It installs and launches on Windows and Linux.
2. It connects reliably to the 1994 Roadmaster through a supported FTDI ALDL cable.
3. It records raw sessions without losing provenance.
4. It reads and explains transmission and engine DTCs.
5. It runs the passive guided transmission test suite.
6. It measures valid shift and TCC events.
7. It compares results with versioned, condition-matched baselines.
8. It quantifies variance from normal.
9. It explains each abnormality and code in plain English.
10. It ranks possible causes and recommends the next discriminating test.
11. It identifies limitations such as unavailable line pressure.
12. It produces an evidence-linked HTML and JSON report.
13. Its parser and analysis engine pass replay, fuzz, and cross-platform tests.
14. Its results have been compared with a trusted reference scanner and, where applicable, a mechanical pressure gauge.

---

# 24. Reference Starting Points

These references are inputs, not unquestioned truth. Preserve local copies and record version/hash information.

- EEHack features: https://ecmhack.com/eehack-2/eehack-features/
- EEHack download and source: https://ecmhack.com/eehack-2/download-eehack/
- EEHack FAQ: https://ecmhack.com/eehack-2/eehack-faq/
- EEHack ALDL communications notes: https://ecmhack.com/misc/ee-aldl-communications/
- ECMHack downloads, including LT1 datastream definitions: https://ecmhack.com/misc/all-downloads/
- `aldl-io` Linux project and simulator notes: https://ecmhack.com/misc/raspberry-pi-dashboarddatalogger/
- Avalonia Linux guidance: https://docs.avaloniaui.net/docs/platform-specific-guides/linux
- Avalonia supported platforms: https://docs.avaloniaui.net/docs/supported-platforms
- .NET support policy: https://learn.microsoft.com/dotnet/core/releases-and-support
- `System.IO.Ports.SerialPort`: https://learn.microsoft.com/dotnet/api/system.io.ports.serialport

Also acquire and locally preserve the applicable 1994 Buick Roadmaster factory service manual sections for:

- ALDL/datastream definitions
- Transmission DTCs
- 4L60E electrical diagnosis
- 4L60E hydraulic pressure tests
- Shift-solenoid logic
- Pressure-control-solenoid logic
- TCC diagnosis
- Transmission-fluid-temperature diagnosis
- Gear-ratio and road-test procedures

---

# 25. Codex Working Rules

Codex must:

- Read this entire plan before changing files.
- Work in phases.
- Keep the repo buildable.
- Run tests after meaningful changes.
- Never invent protocol constants.
- Mark unknowns explicitly.
- Prefer complete files over isolated snippets.
- Record architectural decisions.
- Maintain `STATUS.md` with completed work, current blockers, and next steps.
- Maintain `PROTOCOL_EVIDENCE.md` linking every implemented message/offset/scaling formula to a source.
- Maintain `BASELINE_EVIDENCE.md` linking every normal range or threshold to a source.
- Never enable active transmission controls without a separate instruction.
- Never implement PCM flashing.
- Stop and document a blocker when a required technical definition is unavailable rather than fabricating it.

---

# 26. Initial Prompt for Codex

Copy the prompt below into the local Codex session after placing this file at the repository root as `BUILD_PLAN.md`.

```text
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
```
