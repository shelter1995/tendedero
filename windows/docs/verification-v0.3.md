# 0.3 试用反馈修订验证 · 2026-10-23

## 已完成的改动

- 重新绘制应用和托盘图标，ICO 包含 16–256 像素多个尺寸，嵌入 EXE 和普通窗口。可用 scripts/make-icon.py 重建。
- 托盘与图片右键使用 CreatePopupMenu / TrackPopupMenu 的 Win32 HMENU，移除自绘 WPF 菜单模板。保留 Unicode 文案、勾选、禁用与子菜单；启用 Common Controls v6。这里是 Windows 桌面原生菜单，不是资源管理器专用的 Windows 11 新菜单扩展。
- 统一不透明明暗窗口、Microsoft YaHei UI 字体、整数布局、文本显示渲染；重做关于、快捷键和添加文字弹窗，修正欢迎窗口的内容高度和说明。
- 标注窗口改为左侧工具、上方颜色/笔宽、中央自适应画布、底部状态，提供选中态、工具提示、工具快捷键、Ctrl+S 保存。
- 两组全局快捷键分别持久化，支持清除、恢复默认、应用内重复检查和系统占用提示；设置录制期间暂时释放两组快捷键，关闭窗口时恢复注册。
- PNG 优先读取并保留其透明度；DIB 读取尊重上下行方向和行对齐，RGB 保留字节不再用作 alpha；备用 Bitmap 转 RGB。剪贴板占用/延迟渲染有限重试，以代数与序列号丢弃旧事件。继续排除文本、文件、自身复制和监控隐私标记；不再把“剪贴板历史可用性”标记误判为监控禁令。
- “接管截图”可接收 SnippingTool / ScreenClippingHost 所有者发布的图片；一般图片采集仍由独立开关控制。目录监听补充 Windows Known Folder Screenshots 重定向路径。同图从剪贴板和目录到达时按像素做 12 秒去重，外部文件不删除。

## 本轮证据

- 先用旧读取行为跑合成输入：PNG 优先、PNG-only、系统截图独立开关三项失败；修复后通过。
- Core.Tests：41 项通过，0 失败，包含 DPI/多屏模拟。
- SafeChecks：22 项通过，0 失败，包含 9 项剪贴板/快捷键检查、原有底层安全检查、原生菜单状态、离屏 UI 渲染、截图去重及普通文件导入。
- StoreParityChecks：8 项文件/列表行为通过；第 9 项调用 Clipboard.GetDataObject 时抛出 UnauthorizedAccessException（环境拒绝访问），未执行后续复制操作，不能计为通过。完整输出位于 artifacts/feedback-store.log。
- 构建：Release 编译 0 警告、0 错误。
- 渲染已查看：artifacts/feedback-checks-data/ui/editor.png、about.png、shortcut.png。它们是当前系统深色主题下的实际 WPF 内容渲染，使用合成示例图，不包含操作系统标题栏。
- 日志：artifacts/feedback-core.log、feedback-build.log、feedback-checks-data/results.txt。

## 尚未实测的范围

没有在本轮触发真实 Win+Shift+S、Snipaste F1、鼠标/键盘注入或桌面拖放。旧回归套件尝试读取系统剪贴板时被环境拒绝，未成功读取或写入用户剪贴板。因此上述真实截图链路、菜单失焦关闭/键盘导航、快捷键在其他应用占用时的交互仍需桌面验收。窗口浅色主题、高对比度和多 DPI 实机视觉也尚未截图验收。不要把合成测试通过表述为这些场景全部实测通过。

建议实际验收：开启“接管截图”后用 Win+Shift+S 框选；开启“挂上复制的图片”后用 Snipaste F1 框选并复制。两次均应出现内容正确的卡片；系统自动保存启用时同张只出现一次。随后分别更改/清除两组快捷键，再检查标注保存和取消。

## 参考

菜单生命周期依据 [Microsoft TrackPopupMenu 文档](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-trackpopupmenu)，DIB 方向与步幅依据 [BITMAPINFOHEADER 文档](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-bitmapinfoheader)。外部文档为 API 依据，不是本机交互测试证据。
