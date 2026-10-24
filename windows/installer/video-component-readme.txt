FFmpeg 视频组件来源

组件：Jellyfin 官方 Windows x64 clang GPL 构建
版本：8.1.3-Jellyfin，发布标签 v8.1.3-1
文件：jellyfin-ffmpeg_8.1.3-1_portable_win64-clang-gpl.zip
字节数：68034152
SHA-256：3691f8b2d4c39c511b5faff1c8c4687bd782dac823e63ba69f0755f7ce1af9ce

优先下载：南京大学开源镜像站
https://mirrors.nju.edu.cn/jellyfin/files/ffmpeg/windows/8.x/8.1.3-1/win64/
备用：Jellyfin 官方仓库、GitHub 官方发布页
https://github.com/jellyfin/jellyfin-ffmpeg/releases/tag/v8.1.3-1

该文件哈希已与上游发布资产哈希核对。所有下载源和本地组件包都必须通过同一 SHA-256 校验。
组件由安装器联网取得，主安装包不包含这些可执行文件。

源代码及构建材料：
https://github.com/jellyfin/jellyfin-ffmpeg/tree/v8.1.3-1
许可证：GNU GPL version 3，完整文本见同目录 LICENSE.txt。
构建启用了 libx264、AAC、PNG 和本软件所需的视频处理能力。
可运行 ffmpeg.exe -version 查看完整构建参数，运行 ffmpeg.exe -L 查看许可证声明。

FFmpeg 原作者及贡献者版权归其各自所有。晾图通过独立命令行进程调用该组件。
