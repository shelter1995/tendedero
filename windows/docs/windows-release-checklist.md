# Windows 发布与升级

发行仓库：https://github.com/shelter1995/tendedero 。只匹配windows-v开头的语义版本与对应Windows资产。远端版本以GitHub实际发布为准，本地构建不等于公开发布；每次均核对远端实际状态。

1. 更新项目/安装器版本，运行核心、安全、对象、主题和更新检查。
2. 运行 scripts/package-portable.ps1，再运行 scripts/package-installer.ps1 -Version <版本>。便携包含apply-update.ps1、update-info.json（kind:portable）、SHA256SUMS.json；安装器ZIP含kind:installer、installerFile及完整清单，可直接导入安装版。
3. 核对源码与包一致，把源码提交到Windows分支；生成包、工具和临时文件不进源码仓库。
4. 经发布者确认后创建 windows-v<版本> Release。预发布标记Prerelease；应用默认只检查正式版。
5. 上传原始 Liangtu-<版本>-Setup-x64-r1.exe、.exe.sha256、Liangtu-<版本>-Windows-x64-portable.zip及.sha256；可同时上传Installer-r1.zip及校验文件。版本须与Tag一致。
6. 确认GitHub API资产digest为有效sha256，size正确；不得重命名旧安装器伪装新版本。
7. 用隔离的旧安装/便携副本验收升级、设置/视频保留、取消、损坏和文件占用。不要对正式用户数据做破坏性测试。

0.8.2及以前首次启用内置更新，需要手动安装带更新入口的新版一次（当前发布目标0.10.7）。本次0.10.7已获用户明确发布授权；先上传草稿并验证资产，再设为正式最新版。

助手保留临时目录 Liangtu-updates/<任务>/backup、plan.json、update-result.log。普通替换错误自动回滚，断电/强制结束可能需要用备份恢复。安装版沿用正式安装器维护卸载信息/启动项；/KEEPVIDEO=1复用现有FFmpeg/FFprobe，缺少组件则按原流程处理。
