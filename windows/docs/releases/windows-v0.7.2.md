## 简体中文

晾图是 Tendedero 的社区 Windows 移植版。截图会挂在屏幕顶端，鼠标移到顶边即可展开，支持复制、拖放、图片标注和视频裁剪。

### 下载与安装

大多数用户请选择 **`Liangtu-0.7.2-Setup-x64-r1.exe`**。安装包自带 .NET 桌面运行时，安装到当前用户目录，不需要管理员权限。

如果希望免安装使用，下载 **`Liangtu-0.7.2-Windows-x64-portable.zip`**，完整解压后运行 `Start.cmd` 或 `Liangtu.exe`。

视频缩略图和裁剪需要 FFmpeg。安装版可按需下载视频组件，优先使用南京大学镜像，失败后尝试官方来源；也支持将指定组件 ZIP 放在安装程序旁边离线安装。便携版不包含 FFmpeg，缺少组件时仍可使用图片功能。

### 这个版本带来了什么

0.7 系列加入了图片内直接输入文字，可调整字体、字号、颜色、粗体和斜体，并移动文字位置。文字在当前编辑期间保持可编辑，保存后合并到图片中。

单击松开鼠标后立即开始复制，不再等待双击间隔；双击时第一下也会复制，第二下打开文件。挂图、复制、取下和进入编辑时使用简短提示音，可在托盘菜单关闭。

0.7.2 进一步修复了复制反馈：操作开始时显示“正在复制…”，数据成功放入剪贴板后显示“已复制”，减少成功提示被后续持久化重试拖延的情况。斜体按钮也改成了更容易辨认的大写 **I**。

### 升级与使用说明

- 升级前保存编辑，并从托盘菜单退出旧版。
- 已安装旧版的用户请运行新安装包升级，以更新卸载程序。卸载默认保留设置和收件箱图片，也可以明确选择删除。
- 视频裁剪保存后会替换原文件，请先确认需要保留的内容。
- 面向 Windows 10/11 x64，目前主要在 Windows 11 上验证；未提供原生 ARM64 版本，部分界面仍为中文。
- 程序尚未代码签名，Windows 可能提示未知发布者。附带的 SHA-256 文件用于核对文件完整性。
- 日常运行不上传截图，无账号、遥测或自动更新。安装时下载视频组件需要联网。

遇到问题时，请附上 Windows 版本、屏幕缩放比例、复现步骤和错误提示。

## English

Liangtu is a community Windows port of Tendedero. Screenshots hang on a line at the top of the screen, ready to copy, drag into another app, or annotate. Video preview and trimming are also available.

### Download

Choose **`Liangtu-0.7.2-Setup-x64-r1.exe`** for a standard installation. It includes the .NET desktop runtime and installs for the current user without administrator rights.

For a portable copy, download **`Liangtu-0.7.2-Windows-x64-portable.zip`**, extract the entire archive, and run `Start.cmd` or `Liangtu.exe`.

Video thumbnails and trimming require FFmpeg. The installer can download it, with fallback sources if the first download fails. It also accepts the specified component ZIP beside the installer for offline installation. The portable package does not include FFmpeg; image features work without it.

### Changes

The 0.7 series adds text editing directly on the image, including font, size, color, bold, italic, and positioning controls. Text stays editable during the current session and is flattened into the image when saved.

Copying now starts when the mouse button is released. A double-click copies on the first click and opens the file on the second. Short interaction sounds can be disabled from the tray menu.

Version 0.7.2 fixes delayed copy feedback: “Copying…” appears when the operation starts, and “Copied” appears once the data has been placed on the clipboard, without waiting for subsequent persistence retries. The italic button now uses a more recognizable capital **I**.

### Before using it

Save your edits and quit the previous version before upgrading. Existing installed copies should be upgraded through the new installer to receive the updated uninstaller. Uninstalling keeps settings and inbox images by default, with an explicit option to delete them.

Saving a trimmed video replaces the original file.

This build targets Windows 10/11 x64, with testing focused on Windows 11. Windows 10 and physical mixed-DPI multi-monitor setups need further testing. There is no native ARM64 build, and some dialogs remain Chinese-only.

The build is unsigned. SHA-256 files are included for integrity checks. The app has no accounts, telemetry, screenshot uploads, or automatic updater; downloading the optional video component requires a connection.

Based on [Tendedero](https://github.com/alejandrobujan/tendedero) by Alejandro Buján. Liangtu uses its own name and icon and is not an official Tendedero release.
