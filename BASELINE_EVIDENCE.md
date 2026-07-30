# Baseline evidence register

## Implemented diagnostic thresholds and normal ranges

None. No shift duration, slip, temperature, pressure, voltage, sensor, data-quality, severity, or confidence threshold is currently eligible for a production conclusion.

The baseline placeholder contains only `null` expected values and is marked `Unverified` and `productionEligible: false`.

Phase 2 adds A276 scaling formulas and source-reported values only. A scaling formula is not a normal range. The user-supplied 4L60-E guide is registered as mechanical evidence, but none of its general specifications has been promoted into a condition-matched Roadmaster diagnostic baseline.

The Portal-Diagnostov Roadmaster page contains a scan-data table with nominal values under a stated warm-idle condition. These are useful candidates for later extraction, but the reproduction lacks a factory manual edition and page identity and the table is not transmission-test-specific. No value from it has been promoted to a production baseline.

## Evidence required before implementation

Each baseline must identify its hierarchy type, exact source, vehicle/calibration applicability, operating-condition dimensions, units, sample requirements, version, and verification status. Factory thresholds must cite the applicable factory procedure. Engineering/cohort/self baselines must document derivation and limitations.

## Implemented comparison gate

The analysis layer now has a condition-matched baseline model and a deterministic variance evaluator. This is infrastructure, not baseline data. An unverified baseline returns `NotEvaluated` and no variance judgment. A verified baseline can report within/below/above status, absolute variance from the nearest boundary, and percentage variance. There are still no production-eligible Roadmaster baseline values in the repository.
