# 晾图 Liangtu

[English](README.md) · 简体中文

晾图把截图挂在 Windows 桌面顶端的一根晾衣绳上。鼠标移到屏幕顶边，截图就会展开，方便你复制、标注，或拖进其他应用。

这是 Alejandro Buján 的 [Tendedero](https://github.com/alejandrobujan/tendedero) 的社区 Windows 移植版，使用独立的名称和图标。macOS 用户请前往原项目。

[普通用户](#普通用户) · [开发者](#开发者) · [Windows 源码](https://github.com/shelter1995/tendedero/tree/windows-port/windows)

## 普通用户

### 获取程序

当前仓库公开的是源码，[Releases](https://github.com/shelter1995/tendedero/releases) 暂未提供 Windows 安装包或便携 ZIP。开发者可以按下文构建；如果你已经拿到编译好的程序包，可以按下面的方式使用。

晾图面向 Windows 10/11 x64，目前主要在 Windows 11 上验证。Windows 10 仍需进一步测试，尚未提供原生 ARM64 版本。实时玻璃效果使用 Windows 11 合成接口。自包含程序包已带 .NET 桌面运行时，普通用户无需另行安装。

**便携 ZIP：** 将整个压缩包解压到文件夹，运行 `Start.cmd` 或 `Liangtu.exe`。请保留包内所有文件，包括 `GlassNative.dll`。

**安装包：** 运行安装程序，按向导完成安装。程序安装到当前用户目录，不需要管理员权限。选装视频功能时会联网下载 FFmpeg；仅安装图片功能可以离线完成。目前构建产物尚未签名。

### 第一次使用

1. 启动后，晾图会驻留系统托盘。把鼠标移到屏幕顶边，或按 `Ctrl+Alt+T`，即可展开晾衣绳。
2. 在托盘菜单中开启“接管截图”，接收 Windows 系统截图和录屏。`Win+Shift+S` 仍由 Windows 截图工具处理；晾图自己的框选截图快捷键是 `Ctrl+Alt+S`。
3. 如需接收其他应用复制的图片，开启“挂上复制的图片”。这个选项默认关闭，会忽略文本、文件列表和常见的剪贴板隐私标记。

两组全局快捷键都可以在托盘菜单中修改或清除。你还可以调整卡片大小、保留数量、额外监视的文件夹，以及是否在全屏应用上展开。

### 常用操作

| 操作 | 效果 |
| --- | --- |
| 单击截图 | 等待系统双击间隔后，复制原图 |
| 双击 | 用默认应用打开 |
| 中键单击 | 快速预览 |
| 长按 | 标注图片或裁剪视频 |
| 拖进其他应用 | 向接收应用提供图片或文件 |
| 拖进文件夹 | 向 Windows Shell 请求移动，最终结果由接收方决定 |
| 右键卡片 | 复制、打开、在资源管理器中显示或移除 |
| 左键托盘图标 | 显示或隐藏晾衣绳 |
| 右键托盘图标 | 打开设置或退出程序 |

移除收件箱中的内容时，文件会进入回收站。外部文件默认只从晾衣绳上取下，不删除原文件；右键菜单另有明确的“移到回收站”选项。超过保留数量时，较早的收件箱内容也可能被移除，需要长期保留的文件请另存到其他位置。

### 标注图片与裁剪视频

图片编辑器提供画笔、荧光笔、箭头、形状、文字、裁剪、旋转和局部橡皮擦。按住 `Shift` 可以画正方形、正圆，或按 45 度间隔吸附直线方向。滚轮缩放，`Space` + 拖动或中键拖动平移，`F` 适应窗口，`Ctrl+1` 显示实际大小。查看时的缩放不会改变导出的像素尺寸。

`Ctrl+Z` 撤销，`Ctrl+Y` 或 `Ctrl+Shift+Z` 重做，`Ctrl+S` 保存。普通图片保存时写回原文件；多帧图片和 WebP 的编辑结果会另存为收件箱中的 PNG。

视频可以预览、播放和定位。裁剪时拖动时间轴两端的手柄，或输入起止时间，再保存选中的片段。选区修改支持撤销和重做。**保存视频片段会在新文件成功生成后替换原文件**，选区历史不能恢复已经保存过的视频。

视频缩略图和裁剪需要 FFmpeg。便携版可将 `ffmpeg.exe` 放到程序的 `tools/` 文件夹，或加入 `PATH`。没有 FFmpeg 时，图片功能仍可使用，视频缩略图会显示为占位图。

### 本地数据与升级

晾图没有账号、遥测、截图上传或自动更新服务，日常使用在本机完成。安装时选装视频组件，以及主动打开外部链接，会单独使用网络。

| 位置 | 内容 |
| --- | --- |
| `%LOCALAPPDATA%/Liangtu/Inbox` | 晾图保存的截图和复制图片 |
| `%LOCALAPPDATA%/Liangtu/settings.json` | 设置及晾衣绳上文件的路径 |
| `%LOCALAPPDATA%/Liangtu/app.log` | 本地错误日志 |

替换或升级程序前，请先保存编辑，再从托盘菜单退出。卸载安装版会保留用户数据；删除便携版程序文件夹也不会删除上述数据目录。开机自启由你自行选择。

目前菜单中的“检查更新”打开的是上游 **macOS** 发布页面，不会更新这个 Windows 移植版。

### 已知限制与反馈

部分 Windows 专用窗口仍只有中文。真实混合缩放多屏、更多应用的拖放接收行为，以及 Windows 10 兼容性仍需进一步验证。自动化测试中的模拟屏幕不能覆盖所有真实桌面环境。

反馈问题时，请提供程序版本、Windows 版本、屏幕缩放与排列、复现步骤和错误提示。分享截图或日志前，请检查是否包含私人内容或文件路径。

## 开发者

Windows 实现在 **`windows-port` 分支**的 `windows/` 目录中。默认分支展示项目说明；获取代码请使用下面指定分支的克隆命令。应用使用 C#/.NET 8 和 WPF，小型 C++ 适配层负责 Windows Composition。原版 Swift 工程保留在仓库根目录。

### 构建环境

- Windows x64 开发环境和 .NET 8 SDK。
- PowerShell 7（`pwsh`）。打包脚本使用了 Windows PowerShell 5.1 不具备的 API。
- Visual Studio 2022 Build Tools，安装“使用 C++ 的桌面开发”、x64 MSVC 工具链和 Windows 10/11 SDK。原生构建脚本按标准 Visual Studio Installer 和 Windows Kits 路径查找工具。
- 运行视频集成测试时，需要将 FFmpeg 和 FFprobe 加入 `PATH`。

仓库已包含可直接使用的图标。只有重新运行 `scripts/make-icon.py` 生成图标时，才需要 Python 和 Pillow。

### 从源码构建并运行

```powershell
git clone --branch windows-port https://github.com/shelter1995/tendedero.git
cd tendedero/windows
pwsh -NoProfile -File ./scripts/build-native.ps1
dotnet build ./src/Liangtu -c Release
dotnet ./src/Liangtu/bin/Release/net8.0-windows/Liangtu.dll --show
```

后续命令均在 `windows/` 目录执行。

### 测试

```powershell
dotnet run --project ./tests/Core.Tests -c Release
pwsh -NoProfile -File ./test-safe.ps1
```

安全检查会构建原生适配层、离屏渲染 WPF 控件，并测试模拟屏幕几何；不会注入鼠标或键盘输入、读取剪贴板内容，也不会执行 OLE 拖放。[源码验证报告](https://github.com/shelter1995/tendedero/blob/windows-port/windows/docs/source-verification.md) 记录了已验证的范围和结果。

在可用的 Windows 桌面中执行视频集成与拖动定位测试：

```powershell
pwsh -NoProfile -File ./scripts/test-video.ps1
pwsh -NoProfile -File ./scripts/test-video-scrub.ps1 -SourceVideo ./artifacts/video-fixtures/source.mp4
```

这些检查会在 `artifacts/` 下生成合成媒体，并可能显示测试窗口。`test.ps1`、`scripts/test-recording-intake.ps1` 和 `scripts/test-installer.ps1` 涉及桌面或安装行为，运行前请先阅读脚本。

### 打包

```powershell
pwsh -NoProfile -File ./build.ps1
```

脚本会构建原生适配层、运行核心测试，在 `dist/` 下生成自包含便携 ZIP 和 SHA-256 校验文件，并检查发布文件清单、随包 .NET/WPF 运行库和原生 DLL 加载。目前打包脚本固定使用 .NET 8.0.28，首次构建需要访问 NuGet；同版本发布目录已存在时会拒绝覆盖。ZIP 不包含 FFmpeg。

如需生成安装包，先构建便携包，再将兼容的 Inno Setup 6 安装目录放到 `.tools/InnoSetup/`，确保该目录直接包含 `ISCC.exe`。安装脚本使用增强归档解压和下载页面 API。

```powershell
pwsh -NoProfile -File ./scripts/package-installer.ps1 -Version 0.6.3
```

版本参数必须与便携包一致。FFmpeg 下载地址、校验值和许可信息见 [`installer/video-component.json`](https://github.com/shelter1995/tendedero/blob/windows-port/windows/installer/video-component.json)。构建产物、下载工具、缓存和日志均不纳入 Git。

### 源码结构

下列路径均相对于 [`windows/`](https://github.com/shelter1995/tendedero/tree/windows-port/windows)。

| 路径 | 用途 |
| --- | --- |
| `src/Liangtu/` | WPF 应用、Windows 集成和 UI 检查入口 |
| `src/Liangtu.Core/` | 布局、手势规则、像素几何和视频选区历史 |
| `native/GlassNative.cpp` | Windows Composition 原生适配层 |
| `tests/` | 核心测试和手动拖放接收器 |
| `scripts/` 与 `installer/` | 构建、打包和集成检查 |
| `reference/tendedero/` | 固定版本的上游许可证和语言资源 |

## 致谢与许可

晾图基于 Alejandro Buján 的 Tendedero v1.6.0，源码基准为 `32a3e47187fdea7b2fd43b3ab5a913f0ed33724a`，保留上游[许可证及名称、图标条款](https://github.com/shelter1995/tendedero/blob/windows-port/windows/LICENSE)。原项目代码采用 MIT 许可证，其名称和图标不在该授权范围内。晾图使用独立品牌，并非 Tendedero 官方发布的版本。

上游翻译保留来源说明，Inno Setup 中文翻译保留贡献者信息；打包脚本分别保留适用的 .NET 和 FFmpeg 许可声明。
