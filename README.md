# 4L60 Diagnostics

Cross-platform, evidence-first diagnostics for the 1994 Buick Roadmaster Estate Wagon with the LT1 engine and 4L60E transmission. The application targets .NET 10 LTS and Avalonia 12 on Windows and Linux.

Public repository: <https://github.com/jmaietta/4L60-Diagnostics>

The current repository implements Phase 0 and Phase 1 plus the implementable pre-vehicle work: ALDL framing/checksum, incremental parsing, a bounded read-only A276 snapshot coordinator, Mode 1 request construction, conservative chatter control, echo filtering, response correlation, raw recording/replay, multi-sample A276 transmission decoding, source-backed DTC explanations, descriptive session analysis, verification-gated baseline comparisons, HTML reports, CSV export, and golden/malformed tests. The UI can run a deterministic drive-sequence demo or connect to a serial interface and reports the acquisition result honestly. Target-Roadmaster verification remains pending, so A276 definitions and baseline judgments are not yet eligible to produce vehicle diagnostic conclusions.

Saved captures use the `.lt1raw` format. In the desktop app, open **Saved sessions**, select a capture, and choose **Replay selected session**. The app verifies the file, excludes damaged records from decoding, replays the preserved bytes through the production parser, and opens recovered measurements without connecting to the car. **Open another file** loads a `.lt1raw` capture from another location.

## Install on Windows

Download the Windows ZIP from the project release, extract it, and double-click `Install.cmd`. The package includes its own .NET runtime, requires no administrator access, and creates Desktop and Start-menu shortcuts. It does not install .NET system-wide.

Until releases are code-signed, Windows may show an Unknown Publisher warning. Compare the ZIP's SHA-256 checksum with the checksum published on the release page before installing it.

## Install on Linux x64

Download `4L60-Diagnostics-linux-x64.tar.gz`, then run:

```bash
tar -xzf 4L60-Diagnostics-linux-x64.tar.gz
cd 4L60-Diagnostics-linux-x64
bash install.sh
```

The per-user installer creates an application-menu entry and `~/.local/bin/4l60-diagnostics`. It includes the .NET runtime and does not require root access. Serial-port permissions remain cable-specific; the installer intentionally does not install an unverified udev rule automatically.

The prerequisites and commands below are only for developers building from source.

## Developer prerequisites

- .NET SDK 10.0.302 or a compatible later 10.0 patch
- Windows 10/11 x64, or desktop Linux x64 with X11 and Avalonia's native dependencies

## Quick start

Windows PowerShell:

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\.dotnet\dotnet.exe run --project .\src\LT1Diagnostics.App
```

If .NET is installed system-wide, `dotnet run --project .\src\LT1Diagnostics.App` also works.

Linux:

```bash
bash scripts/build.sh
bash scripts/test.sh
dotnet run --project src/LT1Diagnostics.App
```

See [the architecture overview](docs/architecture/OVERVIEW.md), [project status](STATUS.md), and the governing [build plan](BUILD_PLAN.md).

## Safety and evidence status

- No PCM flashing or active transmission/engine controls are implemented.
- No ALDL message ID, offset, bit definition, scaling formula, DTC criterion, or normal range is implemented without an evidence-register entry.
- All current vehicle/diagnostic definition files are explicitly unverified and ineligible for production conclusions.
- Simulator values are test-only. They use documentary A276 framing through the production builder but are never represented as captured or verified vehicle traffic.
