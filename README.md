> **Development has moved to [shelter1995/liangtu](https://github.com/shelter1995/liangtu).** Use the independent repository for current source, issues, and releases. This fork and its existing downloads remain available for older installations.

# Liangtu · screenshots on a clothesline for Windows

Hang screenshots at the top of your screen, copy or annotate them, drag them to another app, collect them in a basket, or pin them to your desktop.

Liangtu is a community Windows port of [Tendedero](https://github.com/alejandrobujan/tendedero) by Alejandro Buján. It uses its own name and icon and is not an official Tendedero Windows release. [简体中文](README.zh-CN.md)

## Download 0.10.7

- [Windows x64 installer](https://github.com/shelter1995/tendedero/releases/download/windows-v0.10.7/Liangtu-0.10.7-Setup-x64-r1.exe), recommended for most users. Includes the .NET desktop runtime and installs for the current user.
- [Portable Windows x64 ZIP](https://github.com/shelter1995/tendedero/releases/download/windows-v0.10.7/Liangtu-0.10.7-Windows-x64-portable.zip). Extract the entire archive and run Start.cmd or Liangtu.exe.
- [Release notes and SHA-256 files](https://github.com/shelter1995/tendedero/releases/tag/windows-v0.10.7). Use Installer-r1.zip for in-app updates of installed copies, or portable.zip for portable copies.

Save your edits and quit the old tray process before upgrading. Version 0.8.2 and earlier require a manual upgrade; later versions include a Windows update entry. Existing settings and inbox files use the same data directory.

Targets Windows 10/11 x64, with validation focused on Windows 11. Builds are unsigned and Windows may show an unknown-publisher prompt. SHA-256 files verify integrity. No native ARM64 build is available.

## Everyday controls

Move the pointer to the top edge, or press Ctrl+Alt+T, to show/hide the line. Ctrl+Alt+S captures a region. Both shortcuts can be changed or cleared. Win+Shift+S continues to use the Windows Snipping Tool; enable screenshot handling to receive its images and recordings. Images copied from other applications require the separate “Hang copied images” option, off by default.

Click a card to copy the original, double-click to open, hold the image to annotate, or drag to share. Hold the clip for 450ms to pin the image. Desktop pins support dragging and wheel zoom; Esc, close, or “Return to line” hangs the image back. The separate basket action stores it instead.

The basket provides history, search, multi-selection, preview, remounting, and saved copies. Taking down a card or exceeding the presentation capacity preserves its file. Only the explicit Recycle Bin command deletes files.

Image and video annotation share text, pen, highlighter, arrows, shapes, selection and transformations. Video adds trim and annotation time ranges. Saved annotations are flattened into pixels rather than stored as an editable project.

Seven themes cover the shelf and editing windows. Version 0.10.7 adds continuous frame transitions between the shelf and desktop pins, a separate toolbar fade, and faster gesture responses while retaining bounded-window handoffs that avoid full-screen shelf resizing.

The line normally avoids full-screen applications. Game do-not-disturb hides pins and suppresses automatic reveals and sounds while continuing collection; the display hotkey remains available for an explicit reveal. Measured capture origins take priority. Unmeasured third-party entries use the clipboard-event pointer as a placement heuristic, not a claim that every capture tool exposes its exact selection.

## Video and local data

The installer optionally downloads FFmpeg/FFprobe, with fallback sources, fixed SHA-256 verification, and an offline component ZIP option. Portable packages do not bundle FFmpeg; use a local installation or tools/ directory. Image features work without it. See the [video component instructions](https://github.com/shelter1995/tendedero/blob/windows-port/windows/installer/video-component-readme.txt).

Settings and inbox data are stored in `%LOCALAPPDATA%/Liangtu`. Screenshot processing is local, with no account, telemetry, or screenshot uploads. Update checks/downloads and video component installation require a connection. Uninstalling keeps data by default; removal must be chosen explicitly. Saving or trimming can replace the original file, so review the result before saving.

## Build from source

The Windows implementation lives in [windows/ on the windows-port branch](https://github.com/shelter1995/tendedero/tree/windows-port/windows). Build requirements: Windows x64, .NET 8 SDK, PowerShell 7, Visual C++ Build Tools, and the Windows SDK.

```powershell
git clone --branch windows-port https://github.com/shelter1995/tendedero.git
cd tendedero/windows
pwsh -File test-safe.ps1
pwsh -File build.ps1
```

`src/Liangtu` contains WPF and Windows integration; `src/Liangtu.Core` contains layout/gesture policy. Build and verification scripts are under `scripts`, with Inno Setup definitions under `installer`. Installer packaging requires Inno Setup in `.tools/InnoSetup`. The upstream Swift project remains at the repository root.

The current revision passed 486 controlled regression checks and was approved after local use. This does not cover every graphics driver, remote session, Windows 10 installation, or physical mixed-DPI setup. See the [0.10.7 verification record](https://github.com/shelter1995/tendedero/blob/windows-port/windows/docs/verification-v0.10.7.md). Include the version, display scaling/layout, reproduction steps, and error text when reporting issues; review screenshots and logs for private content before sharing.

The original MIT license and name/icon exclusions are preserved. Packages include applicable .NET, FFmpeg and other notices. Liangtu uses independent branding and generated assets.
