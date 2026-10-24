# 晾图 0.6.3 安装包验证

日期：2026-10-24。应用功能冻结于 0.6.3，本轮新增安装与分发脚本，没有修改应用功能代码。

## 交付

- 安装程序：`dist/Share-Liangtu-0.6.3/Liangtu-0.6.3-Setup-x64.exe`，51,575,315 字节（约 49.2 MiB）。
- 分发 ZIP：`dist/Liangtu-0.6.3-Windows-x64-Installer.zip`，包含安装 EXE、简体中文使用说明和 EXE 的 SHA-256。
- 对应校验文件分别位于 EXE/ZIP 旁。
- 支持 Windows 10/11 x64；不需要管理员权限。默认安装至当前用户 LocalAppData/Programs/Liangtu。
- 自带 .NET 8.0.28/WPF，不依赖朋友电脑上是否安装 .NET。
- 开始菜单入口、卸载入口、可选桌面快捷方式。默认不开机自启；若用户原来已开启，只更新该启动项的程序路径。
- 安装/卸载会检测应用单实例互斥量，要求先退出正在运行的晾图，不强行关闭编辑窗口。
- 卸载保留 LocalAppData/Liangtu 下的截图与设置；仅清理安装器管理的文件、快捷方式和对应启动项。

## 视频组件

用户选择“小安装包，视频组件安装时联网下载”。安装器主文件不嵌入 FFmpeg；选择视频组件时直接从 Gyan.dev 下载固定版本的 essentials 7z 包，解压后安装 ffmpeg.exe、ffprobe.exe，以及上游许可证和构建说明。

- 版本：FFmpeg 8.1.2 essentials，下载 33,876,939 字节。
- URL：<https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-8.1.2-essentials_build.7z>
- SHA-256：`e25b682664025d49034c981afb4bae36238a40f29a3cc1c713ad9a8b5b3528f6`
- 哈希来源：<https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-8.1.2-essentials_build.7z.sha256>
- 上游构建与许可说明：<https://www.gyan.dev/ffmpeg/builds/>；该提供方说明其构建采用 GPLv3。
- 来源、版本、哈希及下载方式也记录在安装目录 `licenses/video-component.json`；下载后的 LICENSE、README 保存在 `licenses/FFmpeg`。

下载/校验失败发生在写入应用文件之前。可重试，或返回选择组件页取消视频支持，先离线安装图片功能。重新运行安装器可以补装；取消组件不会卸载此前已安装的视频组件。

## 构建来源

使用 Inno Setup 6.7.3，官方安装工具下载后已验证 Authenticode 状态为 Valid，签名主体为 Pyrsys B.V.。工具位于工作区 `.tools/InnoSetup`。

- 官方下载页：<https://jrsoftware.org/isdl.php>
- 签名验证说明：<https://jrsoftware.org/isdl-verify.php>
- 中文翻译来自官方翻译索引链接：<https://jrsoftware.org/files/istrans/>，源文件保存在 `installer/languages/ChineseSimplified.isl`，保留原作者信息。
- 应用文件只从已校验的 0.6.3 便携包清单逐项复制；排除开发说明和便携启动脚本，加入面向朋友的说明与组件来源。未打包个人截图、设置、日志或测试数据。

构建：`scripts/package-installer.ps1`。首次需先准备 `.tools/InnoSetup/ISCC.exe` 以及对应版本的已验证便携发布目录。脚本默认使用全新暂存目录，防止夹带旧文件。

## 安装验收

使用独立测试 AppId、安装目录、数据目录和启动项名称；没有安装、卸载或覆盖用户正式晾图。测试源码 `scripts/test-installer.ps1`，结果 `artifacts/install-qa/results.txt`。

**36 项检查通过**，包括：

1. 错误组件哈希被拒绝，未产生半安装状态。
2. 正在运行的应用互斥量阻止安装，保留现有会话。
3. 仅图片功能可安装，不下载视频组件；不创建开机启动项。
4. 当前用户卸载注册、开始菜单和快捷方式目标正确。
5. 实际走网络下载视频组件并补装成功，不使用预下载缓存代替该链路。
6. 覆盖安装保留数据，已有开机启动选择得到保留并更新路径。
7. 下载组件在清空常规 PATH 后仍能运行、生成缩略图、精确重编码裁剪；输出 2 秒并保留音轨。
8. 卸载移除程序、快捷方式、卸载注册与本安装拥有的启动项，保留测试截图/设置目录。
9. 卸载后重装仍保留数据，重新校验运行库及文件哈希通过。
10. 选择桌面快捷方式时创建成功，卸载时清理成功。

各次安装后的 `--verify-package` 确认 482 个应用文件哈希、自带 .NET/WPF 运行库及原生 DLL 加载。中文信息页、路径页、组件选择页已实际查看，未执行用户正式安装。

## 已知边界

安装程序和应用本身尚未代码签名，Windows 可能显示未知发布者；哈希只用于完整性核对。没有关闭、绕过或修改系统安全设置。

验证在当前 Windows 桌面完成，没有宣称已经覆盖所有朋友的电脑或干净 Windows 10 虚拟机。联网视频组件依赖上游下载地址可达；下载失败可以退回图片功能安装。实际运行的视频解码仍受 Windows 媒体组件影响。
