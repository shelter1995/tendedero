# 晾图 · Liangtu for Windows

把截图挂在屏幕顶端，随手复制、拖放、标注、收进篮子，或长按夹子钉在桌面。

这是 Alejandro Buján 的 [Tendedero](https://github.com/alejandrobujan/tendedero) 的社区 Windows 移植版，使用独立名称和图标，并非原项目的官方 Windows 发行版。

## 下载 0.10.7

- [Windows x64 安装程序](https://github.com/shelter1995/tendedero/releases/download/windows-v0.10.7/Liangtu-0.10.7-Setup-x64-r1.exe)：适合大多数用户，自带 .NET 桌面运行时，安装到当前用户目录。
- [Windows x64 便携 ZIP](https://github.com/shelter1995/tendedero/releases/download/windows-v0.10.7/Liangtu-0.10.7-Windows-x64-portable.zip)：完整解压后运行 Start.cmd 或 Liangtu.exe。
- [完整 Release 与 SHA-256 校验文件](https://github.com/shelter1995/tendedero/releases/tag/windows-v0.10.7)。已有安装版使用内置升级时，下载 Installer-r1.zip；便携版使用 portable.zip。

升级前保存编辑并从托盘退出旧版。0.8.2及更早版本请手动运行新安装程序或解压新便携包；后续版本可使用托盘“Windows更新与升级”。设置和收件箱沿用原目录。

支持 Windows 10/11 x64，主要在 Windows 11 验证。安装包未签名，Windows 可能显示未知发布者；附带 SHA-256 用于完整性核对。没有原生 ARM64 版。

## 日常使用

- 顶边悬停或 Ctrl+Alt+T 展开/收起；Ctrl+Alt+S 内置框选截图。两组快捷键可修改或清除。
- Win+Shift+S 继续由 Windows 截图工具处理。接管截图开启后接收系统图片和录屏；其他软件需开启“挂上复制的图片”，默认关闭。
- 单击复制原图，双击打开；长按图片进入标注，拖动分享；长按夹子450ms钉在桌面。拖动贴图移动、滚轮缩放；Esc/关闭或“挂回绳子”返回，独立篮子按钮用于收存。
- 图片篮子支持历史、搜索、多选、预览、挂回和保存副本。取下、容量淘汰不删除原文件；只有明确的回收站操作才删除。
- 图片和视频标注支持文字、画笔、荧光笔、箭头、形状及对象移动/缩放/旋转。视频还支持裁剪、元素时长和导出；保存后标注合成为像素，不是可跨次编辑的工程。
- 七套完整主题，包括棉麻画室与柔彩积木。0.10.7 让贴图/回挂边框连续过渡、工具栏单独淡入，并去掉多段固定等待；保留防闪烁的局部窗口交接。

绳子默认避让全屏应用。游戏勿扰会隐藏贴图、暂停自动展开和声音，仍接收图片；手动显示快捷键可主动唤出。截图位置有可靠选区时优先使用；第三方无选区时按剪贴板事件指针锚点推导，不能视为所有工具都返回了精确选区。

## 视频组件与数据

安装时可选下载 FFmpeg/FFprobe，包含镜像回退和固定 SHA-256 校验；也支持离线组件 ZIP。便携版不附带 FFmpeg，可复用本机或 tools/ 中的组件。没有组件时图片功能可用。详见[视频组件说明](https://github.com/shelter1995/tendedero/blob/windows-port/windows/installer/video-component-readme.txt)。

数据位于 `%LOCALAPPDATA%/Liangtu`，截图处理在本地完成，无账号、遥测或截图上传。检查/下载升级和安装视频组件需要联网。卸载默认保留数据，删除数据须明确选择。裁剪/保存可能替换原文件，保存前请确认。

## 源码与构建

Windows 实现在 [windows-port 分支的 windows/](https://github.com/shelter1995/tendedero/tree/windows-port/windows)。需要 Windows x64、.NET 8 SDK、PowerShell 7、Visual C++ Build Tools 与 Windows SDK。

```powershell
git clone --branch windows-port https://github.com/shelter1995/tendedero.git
cd tendedero/windows
pwsh -File test-safe.ps1
pwsh -File build.ps1
```

`src/Liangtu` 为 WPF 与 Windows 集成，`src/Liangtu.Core` 为布局/手势规则，`scripts` 为构建与验证，`installer` 为 Inno Setup 配置。安装器构建还需要 Inno Setup 放在 `.tools/InnoSetup`。上游 Swift 项目保留在仓库根部。

本轮486项受控回归通过，用户已确认本地版本效果；这不覆盖所有显卡、远程会话、Windows 10和物理混合DPI多屏。[0.10.7验证记录](https://github.com/shelter1995/tendedero/blob/windows-port/windows/docs/verification-v0.10.7.md)。报告问题时请说明版本、缩放、屏幕布局、复现步骤，分享截图或日志前检查私人内容。

保留原作者 MIT 许可证与名称/图标条款；打包保留 .NET、FFmpeg 等适用许可。晾图使用自己的品牌和生成素材。
