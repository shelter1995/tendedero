# Liangtu (晾图)

English · [简体中文](README.zh-CN.md)

Liangtu hangs screenshots on a clothesline at the top of your Windows desktop. Move the pointer to the top edge to bring them down, then copy, annotate, or drag them into another app.

This is a community Windows port of [Tendedero](https://github.com/alejandrobujan/tendedero) by Alejandro Buján, with its own name and icon. For the original macOS app, visit the upstream project.

[For users](#for-users) · [For developers](#for-developers) · [Windows source](https://github.com/shelter1995/tendedero/tree/windows-port/windows)

## For users

### Get a build

This fork currently publishes source code. There is no Windows installer or portable ZIP attached to [Releases](https://github.com/shelter1995/tendedero/releases) yet. Developers can build either package using the instructions below. The setup steps here apply if you already have a built package.

The app targets Windows 10/11 x64, with testing focused on Windows 11. Windows 10 still needs validation, and no native ARM64 build is provided. The live glass effect uses Windows 11 composition interfaces. Self-contained packages include the .NET desktop runtime, so users do not need to install it separately.

**Portable ZIP:** extract the entire archive into a folder, then run `Start.cmd` or `Liangtu.exe`. Keep all bundled files together, including `GlassNative.dll`.

**Installer:** run the setup executable and follow the wizard. It installs for the current user without administrator rights. Selecting the optional video component downloads FFmpeg; the image-only installation can run offline. Builds are currently unsigned.

### First use

1. Launch Liangtu. It stays in the system tray; move the pointer to the top edge or press `Ctrl+Alt+T` to show the clothesline.
2. Enable screenshot handling in the tray menu to receive Windows screenshots and recordings. `Win+Shift+S` still opens the Windows Snipping Tool. Liangtu also has its own region capture shortcut, `Ctrl+Alt+S`.
3. To receive images copied from other apps, enable “Hang copied images” (`挂上复制的图片`). This is off by default and ignores text, file lists, and common clipboard privacy markers.

Both global shortcuts can be changed or cleared in the tray menu. You can also choose card sizes, the number of items to keep, extra folders to watch, and whether the line appears over full-screen apps.

### Everyday controls

| Action | Result |
| --- | --- |
| Click a screenshot | Copy the original image after the double-click interval |
| Double-click | Open with the default application |
| Middle-click | Quick preview |
| Press and hold | Annotate an image or trim a video |
| Drag into another app | Offer the image/file to that app |
| Drag into a folder | Request a move through Windows Shell; the receiver determines the result |
| Right-click a card | Copy, open, reveal in Explorer, or remove it |
| Click the tray icon | Show or hide the line |
| Right-click the tray icon | Open settings and quit the app |

Removing an item from Liangtu's inbox sends its file to the Recycle Bin. Removing an external file normally takes it off the line without deleting it; its context menu also has an explicit Recycle Bin command. The retention limit can remove older inbox items, so save anything you want to keep elsewhere.

### Edit images and video

The image editor has drawing, highlighting, arrows, shapes, text, cropping, rotation, and a local eraser. Hold `Shift` for squares, circles, or lines at 45-degree increments. Use the mouse wheel to zoom and `Space` + drag or middle-button drag to pan. `F` fits the image to the window, and `Ctrl+1` shows actual size. View zoom does not change the exported pixel dimensions.

Use `Ctrl+Z` to undo, `Ctrl+Y` or `Ctrl+Shift+Z` to redo, and `Ctrl+S` to save. Saving an ordinary image writes back to that file; multi-frame images and WebP edits are saved as a separate PNG in the inbox.

Video preview supports playback and seeking. To trim, drag the timeline handles or enter start/end times, then save the selected segment. Selection changes support undo/redo. **Saving a video segment replaces the original file** after the new file has been generated successfully. Selection history cannot restore a video after it has been saved.

Video thumbnails and trimming require FFmpeg. For a portable build, put `ffmpeg.exe` in the application's `tools/` folder or make it available on `PATH`. Without it, image features still work and video thumbnails use a placeholder.

### Local data and updates

The app has no account, telemetry, screenshot upload, or automatic updater. Everyday use is local. The optional installer video download and opening external links are separate network actions.

| Location | Contents |
| --- | --- |
| `%LOCALAPPDATA%/Liangtu/Inbox` | Captures and copied images stored by Liangtu |
| `%LOCALAPPDATA%/Liangtu/settings.json` | Settings and paths to items on the line |
| `%LOCALAPPDATA%/Liangtu/app.log` | Local error log |

Before replacing or upgrading a build, save your edits and quit through the tray menu. The installer leaves user data in place when uninstalled. Removing a portable application folder also leaves the data directory intact. Start at login is optional.

The current “Check for Updates” menu item opens the upstream **macOS** releases page. It does not update this Windows port.

### Current limits and feedback

Some Windows-specific dialogs are still Chinese-only. Physical mixed-DPI multi-monitor setups, more drag-and-drop receivers, and Windows 10 need further testing. Automated checks of simulated displays do not cover every desktop setup.

When reporting a problem, include the app version, Windows version, display scaling and arrangement, reproduction steps, and the error message. Check screenshots and logs for private content or file paths before sharing them.

## For developers

The Windows code lives under `windows/` on the **`windows-port` branch**. The default branch carries this overview; use the branch-specific clone command below to get the Windows implementation. It uses C#/.NET 8 and WPF, with a small C++ adapter for Windows Composition. The upstream Swift project remains at the repository root.

### Build requirements

- A Windows x64 development machine and the .NET 8 SDK.
- PowerShell 7 (`pwsh`). Packaging uses APIs unavailable in Windows PowerShell 5.1.
- Visual Studio 2022 Build Tools with **Desktop development with C++**, the x64 MSVC toolchain, and a Windows 10/11 SDK. The native script expects the standard Visual Studio Installer and Windows Kits locations.
- FFmpeg and FFprobe on `PATH` for video integration checks.

The checked-in icons are ready to use. Regenerating them with `scripts/make-icon.py` requires Python and Pillow.

### Build and run from source

```powershell
git clone --branch windows-port https://github.com/shelter1995/tendedero.git
cd tendedero/windows
pwsh -NoProfile -File ./scripts/build-native.ps1
dotnet build ./src/Liangtu -c Release
dotnet ./src/Liangtu/bin/Release/net8.0-windows/Liangtu.dll --show
```

Run the remaining commands from `windows/`.

### Test

```powershell
dotnet run --project ./tests/Core.Tests -c Release
pwsh -NoProfile -File ./test-safe.ps1
```

The safe checks build the native adapter, render WPF controls off-screen, and exercise simulated display geometry. They do not inject mouse/keyboard input, read clipboard contents, or perform OLE drag and drop. The [source verification report](https://github.com/shelter1995/tendedero/blob/windows-port/windows/docs/source-verification.md) records the tested scope and results.

For video integration and scrubbing checks on a Windows desktop:

```powershell
pwsh -NoProfile -File ./scripts/test-video.ps1
pwsh -NoProfile -File ./scripts/test-video-scrub.ps1 -SourceVideo ./artifacts/video-fixtures/source.mp4
```

These checks generate synthetic media under `artifacts/` and may show test windows. Read `test.ps1`, `scripts/test-recording-intake.ps1`, and `scripts/test-installer.ps1` before running them; they exercise desktop or installation behavior.

### Package

```powershell
pwsh -NoProfile -File ./build.ps1
```

This builds the native adapter, runs core tests, and creates a self-contained portable ZIP under `dist/`, with a SHA-256 sidecar. It checks the published file manifest, bundled .NET/WPF runtime, and native DLL loading. The current package script pins .NET to 8.0.28 and needs NuGet access on the first build. It refuses to overwrite an existing release directory. FFmpeg is not included in the ZIP.

To build the installer, first create the portable package and place a compatible Inno Setup 6 installation under `.tools/InnoSetup/`, with `ISCC.exe` directly in that folder. The installer source uses enhanced archive extraction and download-page APIs.

```powershell
pwsh -NoProfile -File ./scripts/package-installer.ps1 -Version 0.6.3
```

The version argument must match the portable package. FFmpeg download details and license information are in [`installer/video-component.json`](https://github.com/shelter1995/tendedero/blob/windows-port/windows/installer/video-component.json). Build outputs, downloaded tools, caches, and logs are excluded from Git.

### Source layout

Paths below are relative to [`windows/`](https://github.com/shelter1995/tendedero/tree/windows-port/windows).

| Path | Purpose |
| --- | --- |
| `src/Liangtu/` | WPF app, Windows integration, and UI check entry points |
| `src/Liangtu.Core/` | Layout, gesture policies, pixel geometry, and video selection history |
| `native/GlassNative.cpp` | Native Windows Composition adapter |
| `tests/` | Core tests and a manual drag-and-drop receiver |
| `scripts/` and `installer/` | Build, packaging, and integration checks |
| `reference/tendedero/` | Pinned upstream license and localization resources |

## Credits and license

Liangtu is based on Tendedero v1.6.0 by Alejandro Buján, commit `32a3e47187fdea7b2fd43b3ab5a913f0ed33724a`. The upstream [license and name/icon terms](https://github.com/shelter1995/tendedero/blob/windows-port/windows/LICENSE) are preserved. Tendedero's source code is MIT-licensed; its name and icon are excluded from that grant. Liangtu uses separate branding and is not an official Tendedero release.

Upstream translations retain their attribution. The Inno Setup Chinese translation retains its contributor header, and packaging preserves the applicable .NET and FFmpeg notices.
