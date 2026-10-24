# Source upload verification

Date: 2026-10-24 (Asia/Shanghai).

This check uses the isolated `windows/` source layout prepared for the `windows-port` branch of `shelter1995/tendedero`. It snapshots Liangtu 0.6.3 on top of Tendedero v1.6.0 (`32a3e47187fdea7b2fd43b3ab5a913f0ed33724a`).

## Results from this checkout

| Check | Result |
| --- | --- |
| `pwsh -NoProfile -File ./test-safe.ps1` | 49 core tests passed; 76 safe/UI checks passed; no failures |
| Native C++ adapter build | Passed |
| `dotnet build ./src/Liangtu -c Release -p:UseSharedCompilation=false` | Passed with 0 warnings and 0 errors |
| `pwsh -NoProfile -File ./scripts/package-portable.ps1` | Self-contained win-x64 package built successfully |
| Published application `--verify-package` | 483 file hashes checked; bundled .NET 8.0.28 and WPF loaded; native DLL loaded |
| Copied source snapshot | SHA-256 verified for all 127 copied files before Git whitespace cleanup; final contents checked against the snapshot allowing only CRLF/LF conversion and removal of terminal blank lines |
| Upstream resources | All 11 localization files and the upstream license match the pinned upstream text, apart from line endings or trailing whitespace |
| Upload file inventory | No build outputs, runtime DLLs, executables, archives, tool installations, caches, or logs included |

Environment: Windows x64, .NET SDK 8.0.422, PowerShell 7.6.5, local MSVC/Windows SDK toolchain.

The initial portable publish could not reach NuGet from the restricted execution environment (`NU1301`). The same packaging command succeeded after network access was allowed. No application code changes were needed.

## Scope

The safe checks render WPF controls off-screen and simulate display geometry. They do not inject mouse or keyboard input, access clipboard contents, or perform OLE drag and drop. The package check does not show windows or access normal user data.

Video playback/scrubbing, installer execution, system screenshot capture, and physical multi-monitor interaction were not rerun for this source upload. Earlier results are recorded separately in the version-specific verification notes and are not fresh checks of this checkout. Windows 10 and ARM64 are not validated by this run.

The English README, source-layout notes, ignore rules, and this report were added for publication. Application code and existing build/test scripts retain their content; line endings and extra terminal blank lines were normalized for Git. Test logs and the generated portable package remain local and are excluded from Git.
