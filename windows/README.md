# Liangtu (晾图) for Windows

Liangtu is a community Windows port of [Tendedero v1.6.0](https://github.com/alejandrobujan/tendedero/tree/v1.6.0), implemented in C#/.NET 8 and WPF with a small native C++ adapter for Windows Composition. This source snapshot is version **0.6.3**.

It uses its own name and icon. It is maintained in this fork and is not an official Tendedero release. The macOS implementation remains at the repository root.

[中文说明与版本记录](README.zh-CN.md)

## What works

Screenshots hang on a line that appears when the pointer reaches the top edge of the screen. Click to copy the original image, double-click to open it, or drag it to another application. The tray menu controls screenshot monitoring, optional clipboard image capture, and customizable global shortcuts.

The Windows implementation also includes region capture, image annotation with undo/redo, and video preview and trimming. Video trimming has a filmstrip timeline, range handles, precise time entry, and undo/redo for selection changes. FFmpeg is an optional dependency for video processing.

The app stores settings, logs, and its image inbox under `%LOCALAPPDATA%/Liangtu`. It has no accounts, telemetry, or automatic updater. The app itself operates locally; the optional installer can download FFmpeg when the video component is selected.

## Requirements

- Windows x64. The intended target is Windows 10/11, but Windows 10 and the full range of display configurations have not been fully validated. The live glass effect uses Windows 11 composition interfaces. ARM64 builds are not provided.
- .NET 8 SDK to build from source.
- Visual Studio 2022 Build Tools with **Desktop development with C++**, the x64 MSVC toolchain, and a Windows 10/11 SDK. The native build script locates MSVC using `vswhere` in the standard Visual Studio Installer directory and expects the SDK under `C:\Program Files (x86)\Windows Kits\10`.
- PowerShell 7 (`pwsh`). Packaging uses .NET path APIs that are not available in Windows PowerShell 5.1.
- Optional: FFmpeg on `PATH` or in the built application's `tools/` directory for video thumbnails and trimming.

The checked-in icons are ready to use. Regenerating them with `scripts/make-icon.py` requires Python and Pillow.

## Build and run

Clone this fork's Windows branch, then run the commands from `windows/`:

```powershell
git clone --branch windows-port https://github.com/shelter1995/tendedero.git
cd tendedero/windows
pwsh -NoProfile -File ./scripts/build-native.ps1
dotnet build ./src/Liangtu -c Release
dotnet ./src/Liangtu/bin/Release/net8.0-windows/Liangtu.dll --show
```

For a self-contained portable package with the .NET runtime included:

```powershell
pwsh -NoProfile -File ./build.ps1
```

This builds the native adapter, runs the core tests, publishes the app, checks the package manifest and native DLL loading, and creates `dist/Liangtu-0.6.3-Windows-x64-portable.zip` with a SHA-256 sidecar. Run `dist/Liangtu-0.6.3/Start.cmd` or `run.cmd` to open it.

The packaging script pins the bundled runtime to 8.0.28. It needs NuGet access on the first build and refuses to overwrite an existing release directory. Keep `GlassNative.dll` beside the application. FFmpeg is not included in the portable ZIP.

To build the optional installer, first build the portable package, then place a compatible Inno Setup 6 installation at `.tools/InnoSetup/` with `ISCC.exe` directly inside that directory:

```powershell
pwsh -NoProfile -File ./scripts/package-installer.ps1 -Version 0.6.3
```

The installer source uses enhanced archive extraction and the download-page APIs. It installs for the current user and offers an optional FFmpeg download; the URL, checksum, and license information are recorded in `installer/video-component.json`. The compiler and downloaded tools are not committed to this repository.

## Tests and current limits

```powershell
# Core policies, geometry, and video selection history
dotnet run --project ./tests/Core.Tests -c Release

# Native build and automated WPF checks with isolated test data
pwsh -NoProfile -File ./test-safe.ps1
```

`test-safe.ps1` exercises off-screen UI rendering and simulated input/display geometry. It does not establish that physical multi-monitor setups or cross-application drag and drop work in every environment.

Additional desktop/media checks are available for deliberate interactive testing:

```powershell
# Requires FFmpeg on PATH; creates synthetic media under artifacts/
pwsh -NoProfile -File ./scripts/test-video.ps1
pwsh -NoProfile -File ./scripts/test-video-scrub.ps1 -SourceVideo ./artifacts/video-fixtures/source.mp4
```

`test.ps1`, `scripts/test-recording-intake.ps1`, and `scripts/test-installer.ps1` also exercise desktop or installation behavior; read those scripts before running them. They are not part of the source-upload verification.

The existing [0.6.3 verification notes](docs/verification-v0.6.3.md) record earlier automated and manual checks. Historical notes refer to local `artifacts/` outputs, which are deliberately excluded from Git. Fresh checks of this source layout are recorded in [source verification](docs/source-verification.md).

Real mixed-DPI multi-monitor use, more drag-and-drop receivers, and Windows 10 still need further validation. Several Windows-specific dialogs are Chinese-only. Builds are unsigned.

## Source layout

| Path | Contents |
| --- | --- |
| `src/Liangtu/` | WPF application, Windows integration, and UI check entry points |
| `src/Liangtu.Core/` | Layout, gesture policies, pixel geometry, and video selection history |
| `native/GlassNative.cpp` | Windows Composition adapter, built locally |
| `tests/` | Core tests and a manual drag-and-drop receiver |
| `scripts/` | Native build, packaging, and media/installer checks |
| `installer/` | Inno Setup source and optional video-component metadata |
| `reference/tendedero/` | Pinned upstream license and localization resources needed by the build |

## Credits

Based on Tendedero by Alejandro Buján, commit `32a3e47187fdea7b2fd43b3ab5a913f0ed33724a`. The layout and interaction rules were ported for Windows. Upstream localization files are retained at their original relative paths so the build is self-contained.

The upstream [license and name/icon terms](LICENSE) are preserved. Liangtu has separate branding and does not imply endorsement by the original author. The Inno Setup Chinese translation retains its original contributor header. FFmpeg and .NET runtime license notices are handled separately by the packaging scripts.
