# Source and release verification

Date: 2026-10-24 (Asia/Shanghai).

Version: Liangtu 0.7.2, Windows x64; installer revision r1. This report covers the source synchronized into `windows/` and the existing packages selected for the `windows-v0.7.2` pre-release.

## Fresh checks

| Check | Result |
| --- | --- |
| `pwsh -NoProfile -File ./test-safe.ps1` | 49 core tests and 99 safe/UI checks passed; no failures |
| Native C++ adapter build | Passed |
| `dotnet build ./src/Liangtu -c Release -p:UseSharedCompilation=false` | Passed; 0 warnings and 0 errors |
| Selected installer and portable ZIP | SHA-256 values match their existing sidecars |
| Portable ZIP | CRC check passed; all 483 manifest file hashes verified |
| Portable application metadata | `Liangtu/0.7.2`; application, core, and native DLLs match the existing release directory |
| Existing portable app `--verify-package` | 483 hashes, bundled .NET 8.0.28/WPF, and native DLL load passed |
| Installer file metadata | 0.7.2.1, matching version 0.7.2 and installer revision r1 |

The installer compile log records the same EXE filename and SHA-256 selected here. The selected packages are the existing local release artifacts, not replacements produced by this source sync.

## Release assets

| File | SHA-256 |
| --- | --- |
| `Liangtu-0.7.2-Setup-x64-r1.exe` | `7a6f8a84d6b33c756c10be8a4112c99bef0d66d24827299a886cbd4c52a04d54` |
| `Liangtu-0.7.2-Windows-x64-portable.zip` | `f6b1532ca4fa543756794f69311f015793a2e004ae0149cdf4404874c625de43` |

Both files are distributed with their corresponding `.sha256` sidecars. The application and installer are unsigned.

## Scope

The 151 source, script, resource, installer, and historical documentation files were copied from the current local development tree. Their contents are preserved apart from Git line-ending normalization and excess blank lines at end of file. Bilingual README files and release documentation were updated separately. Build outputs, caches, local logs, and installer binaries are excluded from the source commit.

Safe checks use off-screen WPF rendering, controlled clipboard writers, and simulated display geometry. They do not inject input, access the user's clipboard contents, or perform OLE drag and drop. The package verification does not access normal user data or show windows.

This run did not repeat installation/uninstallation, video playback, network mirror failover, or physical multi-monitor interaction. The existing version-specific notes describe earlier checks and their limitations. In particular, the desktop copy diagnostics in `verification-v0.7.2.md` report a clipboard-restoration failure; they are not an end-to-end pass. Some standalone installer harnesses require earlier isolated fixtures and are not generic release tests.

Windows 10, ARM64, and the full range of physical mixed-DPI arrangements are not validated by this run. The [previous source verification report](source-verification-v0.6.3.md) applies only to the earlier 0.6.3 snapshot.
