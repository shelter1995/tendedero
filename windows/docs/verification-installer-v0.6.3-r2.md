# 晾图 0.6.3 安装器 r2 验证

日期：2026-10-24。修订范围为视频组件下载与安装；应用版本仍为 0.6.3。用户暂停的 VMware 录屏偏色问题未改动。

## 交付

- `dist/Share-Liangtu-0.6.3-r2/Liangtu-0.6.3-Setup-x64-r2.exe`：51,951,331 字节，约 49.5 MiB。
- EXE SHA-256：`97DC3AE713CE0348B96176B5D5617270693B79240A8D716ABDB62711B28FC7C3`。
- `dist/Liangtu-0.6.3-Windows-x64-Installer-r2.zip`：51,463,208 字节，包含安装器、中文使用说明与 EXE 校验文件。
- ZIP SHA-256：`823F0011EE6A20CD0F10966E1AC896DB71F16B0CA0FA55BE5517EC30680A4537`。
- 保留原 r1 分发包。安装器和应用仍未代码签名。
- 逐项核对正式安装暂存目录中来自原便携包的 480 个文件，哈希全部一致。只更新分发说明与视频组件元信息，不重新编译应用。

## 下载来源与完整性

原 Gyan 下载源在用户另一台电脑的网络中超时。改用 Jellyfin 官方 Windows x64 clang GPL 构建，以获得可核验的国内镜像；这不是原 Gyan 包的字节级镜像。

- 版本：`8.1.3-Jellyfin`，发布标签 `v8.1.3-1`。
- 文件：`jellyfin-ffmpeg_8.1.3-1_portable_win64-clang-gpl.zip`。
- 大小：68,034,152 字节，约 64.9 MiB，界面按约 65 MB 提示；比原组件约 33 MB 更大。
- SHA-256：`3691f8b2d4c39c511b5faff1c8c4687bd782dac823e63ba69f0755f7ce1af9ce`。
- 首选：[南京大学镜像目录](https://mirrors.nju.edu.cn/jellyfin/files/ffmpeg/windows/8.x/8.1.3-1/win64/)。
- 备用：[Jellyfin 官方目录](https://repo.jellyfin.org/files/ffmpeg/windows/8.x/8.1.3-1/win64/)、[GitHub 官方发布](https://github.com/jellyfin/jellyfin-ffmpeg/releases/tag/v8.1.3-1)。
- 哈希来源：[官方发布资产列表](https://github.com/jellyfin/jellyfin-ffmpeg/releases/expanded_assets/v8.1.3-1)。实际国内镜像下载的哈希与官方一致。
- 本机使用 `Invoke-WebRequest -NoProxy` 下载用时 13.68 秒；未更改系统代理或 VPN。这是本机观察，不保证所有网络速度。

三个源按顺序尝试，网络错误或哈希不符时切换；用户主动取消会停止，不继续尝试。各来源均验证同一固定 SHA-256。网络超时使用 Inno Setup 下载器默认行为，未增加独立的每源限时机制。

安装器还支持同目录原始组件 ZIP，或 `/VIDEOARCHIVE=` 指定文件；先校验再解压，损坏文件拒绝安装。全部来源失败时，可返回组件选择页取消视频功能，先装图片功能。组件下载与校验发生在应用文件写入之前。

GPLv3 许可证取自 Jellyfin 同一发布标签，随安装器保存；组件来源、版本、哈希与源代码链接位于 `installer/video-component.json` 和 `installer/video-component-readme.txt`，安装后位于 `licenses`。

## 验证结果

1. `scripts/test-installer.ps1`：**36 项通过**，记录 `artifacts/install-qa-r2/results.txt`。包含仅图片离线安装、真实联网下载/补装、哈希拒绝、应用互斥量、启动项保留、快捷方式、卸载与数据保留、自带运行库及下载后组件的独立运行。`online-video-upgrade.log` 确认首选南京大学镜像下载并校验成功，下载阶段约 34 秒。
2. `scripts/test-installer-failover.ps1`：**13 项通过**，记录 `artifacts/installer-r2-failover/results.txt`。测试版仅将 URL 替换为本机服务：首源返回 HTTP 504，第二源返回错误内容，第三源返回已核验的官方包。安装成功且两个 EXE 哈希一致。停掉服务后全部来源失败，未产生半安装；同目录正确 ZIP 在无下载请求下完成安装，损坏 ZIP 被拒绝。
3. 新组件按应用实际裁剪参数检查五种现有格式，并解码生成缩略图，记录 `artifacts/installer-r2-formats/results.txt`。MP4/MOV/M4V 输出 2.000 秒，AVI 2.089796 秒，WMV 2.067 秒，均保留音轨；容器时长容差为 0.15 秒。此检查不等同于再次验收 Windows 播放界面。
4. 正式安装器编译成功，输出 ZIP 与 EXE 的旁置校验文件逐项核对通过。使用独立测试 AppId 和工作区数据目录，未覆盖用户正式安装或关闭其编辑窗口。
5. 测试结束确认测试卸载注册项、测试开机启动项和本地测试服务均已清理。

## 边界

本机验证不能替代朋友实际网络的复测。国内镜像不可用时，海外备用源仍可能受网络影响，可用同目录组件 ZIP 绕开安装时联网。没有宣称每个备用公网源都在当前网络完整下载成功；切换逻辑使用受控本地服务验证。应用视频预览仍使用原 Windows 媒体路径，未修改录制、解码或色彩转换代码。
