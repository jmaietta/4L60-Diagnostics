# 4L60 Diagnostics

A desktop diagnostic application for the 4L60E transmission in the 1994 Buick Roadmaster.

## Download the app

You do not need to install .NET or any developer tools. Choose your computer:

### Windows 10 or 11

[Download 4L60 Diagnostics for Windows](https://github.com/jmaietta/4L60-Diagnostics/releases/latest/download/4L60-Diagnostics-win-x64.zip)

1. Open your **Downloads** folder after the download finishes.
2. Right-click `4L60-Diagnostics-win-x64.zip` and select **Extract All**.
3. Open the extracted folder.
4. Double-click **Install.cmd**.
5. Open **4L60 Diagnostics** from the Desktop or Start menu.

Windows may show an **Unknown Publisher** warning because the installer is not yet code-signed.

### Linux x64

[Download 4L60 Diagnostics for Linux](https://github.com/jmaietta/4L60-Diagnostics/releases/latest/download/4L60-Diagnostics-linux-x64.tar.gz)

1. Open your **Downloads** folder after the download finishes.
2. Right-click `4L60-Diagnostics-linux-x64.tar.gz` and extract it.
3. Open the extracted `4L60-Diagnostics-linux-x64` folder in a terminal.
4. Run:

```bash
bash install.sh
```

5. Open **4L60 Diagnostics** from the application menu.

The Linux installer does not require root access. Serial-port permissions may still depend on the diagnostic cable.

## Current diagnostic status

The application can run a built-in demonstration, discover serial diagnostic cables, preserve raw ALDL traffic, replay saved `.lt1raw` sessions, decode the documentary A276 transmission snapshot, display source-backed DTC explanations, export reports, and show descriptive transmission timelines.

Vehicle validation against the target Roadmaster is still pending. Until that validation is complete, the app does not present its current A276 definitions or baseline comparisons as verified repair conclusions.

## Saved sessions

Open **Saved sessions** in the app, choose a `.lt1raw` capture, and select **Replay selected session**. Replay reads the saved data and never transmits anything to the vehicle.

## Developer information

The commands below are only for developers building the source code.

Prerequisites:

- .NET SDK 10.0.302 or a compatible later 10.0 patch
- Windows 10/11 x64, or desktop Linux x64 with X11 and Avalonia's native dependencies

Windows:

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\.dotnet\dotnet.exe run --project .\src\LT1Diagnostics.App
```

Linux:

```bash
bash scripts/build.sh
bash scripts/test.sh
dotnet run --project src/LT1Diagnostics.App
```

See [the architecture overview](docs/architecture/OVERVIEW.md), [project status](STATUS.md), and [build plan](BUILD_PLAN.md).

## Safety

- No PCM flashing is implemented.
- No forced gear, TCC, line-pressure, timing, or injector controls are implemented.
- Raw bytes and provenance are preserved so corrected definitions can re-decode old sessions.
- Simulator data is clearly separated from vehicle data.
