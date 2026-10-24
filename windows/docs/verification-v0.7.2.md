# 0.7.2 斜体 I 与实际剪贴板反馈

日期：2026-10-24。

## 修复

- 用户实际运行进程确认是 `dist/Liangtu-0.7.1/Liangtu.exe`，版本 0.7.1.0。
- 斜体按钮仍是大写字母 I，改用 Georgia 粗斜体、18px，明确显示上下衬线，不再像一条斜杠。32px 点击区域、主题、切换状态保持一致。
- 点击立即显示“正在复制…”，渲染前先登记新复制并取消旧请求，避免旧成功事件清除新反馈。准备/写入未完成时不提前显示成功。
- 剪贴板发布、确认所有权后立即通知“已复制”；持久化继续按原有规则重试。临时占用的初始重试间隔缩短为 15/30/60/100ms，再使用 150ms，次数有界。
- 外部应用在成功发布后覆盖剪贴板不误报失败；发布后的持久化异常使用不同提示，避免声称尚未复制。
- 托盘退出等待当前复制结束，并持久化仍持有的数据，包括新请求失败前被取消持久化的旧请求。期间不再接受新复制。若仍被占用且无法持久化，则暂缓退出并提示。
- 已确认的 B 组声音文件、音色和映射未改变。

## 真实桌面诊断

使用工作区合成 3840×2160 PNG，真实 Windows Clipboard.SetDataObject、OleIsCurrentClipboard、OleFlushClipboard，记录复制成功事件与 CompositionTarget.Rendering。

| 测试 | 第一次 | 第二次 | 第三次 |
| --- | ---: | ---: | ---: |
| 修改前成功反馈开始绘制 | 1026.6ms | 1239.0ms | 1368.1ms |
| 修改后处理中反馈开始绘制 | 2.5ms | 0.7ms | 0.6ms |
| 修改后成功反馈开始绘制 | 154.0ms | 485.5ms | 334.7ms |

记录：`artifacts/copy-desktop-before-data/timings.txt`、`artifacts/copy-desktop-after-data/timings.txt`。耗时随剪贴板占用变化，不能承诺固定时延。诊断直接调用复制链路与实际卡片反馈，非人为注入鼠标事件。后续增加的退出保护用受控调度回归验证，没有再改写桌面剪贴板。

根因：数据已发布后仍等待持久化，多次剪贴板占用重试把成功提示推迟约一秒。微软说明 OLE 数据对象已放在剪贴板时可用于粘贴，Flush 主要保证程序退出后数据仍可用：[OleFlushClipboard 官方文档](https://learn.microsoft.com/en-us/windows/win32/api/ole2/nf-ole2-oleflushclipboard)。

诊断的原剪贴板恢复分别发生 OpenClipboard/CloseClipboard 错误，未能确认恢复。测试未记录原剪贴板内容；已经向用户说明并建议重新复制需要的内容。诊断程序的非零退出码来自恢复步骤，不应当作端到端完整测试通过。

## 回归

- `artifacts/publish-feedback-red.log`：先复现成功提示等待持久化的问题。
- `artifacts/publication-edge-red.log`：所有权检查临时占用的失败复现。
- `artifacts/exit-copy-red.log`：旧请求已发布、新请求失败后退出仍须持久化旧数据的失败复现。
- 最终核心测试 49 项、界面/安全回归 99 项通过：`artifacts/feedback-0.7.2-final.log`。
- 标准/紧凑布局渲染位于 `artifacts/safety-v2-final-data/ui/text-editor-1120.png`、`text-editor-760.png`，大写 I 视觉检查通过。

## 产物

便携包：`dist/Liangtu-0.7.2-Windows-x64-portable.zip`，483 个文件哈希、自带 .NET/WPF 和原生 DLL 校验通过，记录 `artifacts/release-0.7.2.log`。安装包编译记录 `artifacts/installer-0.7.2.log`。当前用户的 0.7.1 进程未被强制退出，需退出旧版后运行新版。
