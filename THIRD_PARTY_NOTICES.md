# Third-party notices

No EEHack or `aldl-io` source code has been copied into this repository. Phase 2 uses a clean implementation based on documented wire behavior and independently constructed tests.

Reference-only archives inspected on 2026-07-30:

| Component | Archive hash (SHA-256) | License finding | Project use |
| --- | --- | --- | --- |
| EEHack 4.9.3 source | `41684A1D084D3E07DA80B6D40BAE0BB5CB1BB7AD12463C1105B0DCA68E3B894B` | `eehack/LICENSE.txt` contains the GNU Lesser General Public License v3 | Behavior comparison only; no code copied, linked, or distributed |
| `aldl-io` 1.6.2 | `CBC2DB2607DE3F5FA4465F214EE352AF75D20223AB8B03576DCB0333D386831D` | `aldl-io/LICENSE` contains a 3-clause BSD-style license, copyright 2014 Steve Haslin | Behavior comparison only; no code copied or linked |
| LT1 Datastream Definitions | `77E49889CED5D9C93B6ED48B7401AB47658C90DDFA76D75499DE856CB5D961E6` | No license, copyright statement, or provenance file is included in the archive | Facts are transcribed into original, source-cited definitions; the archive is ignored and is not redistributed |

The user-supplied *4L60-E Technician's Guide* identifies itself as copyright 1992 General Motors Powertrain Division and restricts reproduction. The PDF is ignored and is not part of the distributable repository.

NuGet dependencies used by this repository are centrally pinned in `Directory.Packages.props`:

| Component | Use | Declared license |
| --- | --- | --- |
| Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent 12.1.0 | Cross-platform desktop UI | MIT |
| System.IO.Ports 10.0.10 | Serial transport implementation | MIT |
| xUnit 2.9.3 and xunit.runner.visualstudio 3.1.4 | Tests | Apache-2.0 |
| Microsoft.NET.Test.Sdk 17.14.1 | Test host | MIT |
| coverlet.collector 6.0.4 | Test coverage | MIT |
| JsonSchema.Net 9.4.0 | Definition-schema tests | MIT |

The .NET SDK/runtime is obtained from Microsoft and is subject to its own license terms. Package license files included in restored packages remain authoritative. Review redistributed transitive packages and publish output before a public binary release.
