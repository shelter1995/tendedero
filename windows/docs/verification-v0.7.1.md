# 0.7.1 单行文字属性与即时复制

日期：2026-10-24。用户明确要求去掉新增属性行、改用 B/I，并确认“松开立即复制，接受双击首击也复制”。音效先提供 A/B 试听，用户随后明确选择“B：轻量单次短音”，最终版本接入批准的 B 组。

## 已修改

- 文字属性与颜色共用一行，文字模式以字体/字号/B/I/删除图标替换笔宽。切换工具不改变画布上边界，760px 紧凑窗口不换行。
- 两种 ComboBox 共用 30px 高度、左侧 8px 内距和垂直居中排版。编辑字号的内部 TextBox 不再继承通用输入框的大内距。下拉列表和焦点跟随系统明暗主题。
- 粗体、斜体改为扁平 B/I ToggleButton，保留可见选中态与辅助功能名称。
- 单击松开立即触发 Copy；双击第二下触发 Open，长按与拖拽不误触复制。成功提示仅在 ClipboardWriter 成功后出现，但不再等待 200ms 淡入。
- 图片读取/解码及非 PNG 编码在后台完成；PNG 直接复用原始字节，保留透明度、交错及完整内容。使用原有 ReadWrite/Delete 共享标志读取外部编辑器仍打开的文件。

## 耗时

当前系统双击间隔为 500ms，旧版在鼠标松开后完整等待它；本版本取消这项等待。

合成 PNG、每尺寸 3 次，实际准备链路加受控 writer 的中位数：

| 分辨率 | 修改前 | 修改后 |
| --- | ---: | ---: |
| 1920×1080 | 217.6ms | 78.4ms |
| 3840×2160 | 761.0ms | 141.8ms |

记录：`artifacts/copy-prepare-before-data/latency.txt`、`artifacts/copy-prepare-after-data/latency.txt`。这些数值包含真实文件读取/解码/编码及现有 40ms 等待，不含原生剪贴板 Set/Flush 耗时，不能当作完整桌面复制延迟。创建私有 window station 被 Windows 以错误 5 拒绝，记录在 `artifacts/copy-latency-before-data/result.txt`；没有退回或改写用户当前剪贴板。

## 声音排查与审批状态

旧版应用代码对取下事件只调用一次 TakeDown 音效，但 240ms 音效内部包含 5ms 与 55ms 两次短击，单个音效本身有双响。新版经用户确认后改为 B 组的单次短音。这不能排除特定 Windows 环境另有系统声音；本轮没有录制用户桌面声音，不能将听到的每个声音确定归因。

两套候选保留在 `artifacts/sound-auditions/`，顺序为夹上、复制、取下/删除、进入编辑；每项亦有独立 WAV。用户选择 B 后，将四个批准的 WAV 原样嵌入程序集，运行时不重新合成。SoundChecks 用批准文件的 SHA256 固定校验四种映射，进入图片标注/视频裁剪成功后才播放编辑音。仍保留静音、启动恢复静音与 280ms 防叠响。将来改音色仍需先试听确认。

## 验证

- 核心 49 项、界面/安全 92 项全部通过：`artifacts/approved-sounds-green.log`。其中包含四个运行时音效与批准文件逐字节 SHA256 一致性验证。
- 修复前失败记录：`compact-toolbar-red.log`（额外属性行）、`immediate-copy-red.log`（复制等待）、`png-reuse-red.log`（重复编码）、`shared-copy-red.log`（外部文件共享读取回归），均在 artifacts 中。
- 标准与紧凑尺寸已渲染检查。真实测试窗口已确认字号下拉选择 48、生效保存、I 按钮切换和主题一致；界面输入来自隔离示例。
- 独立审阅发现的共享读取问题已补测试修复。沿用既有占用重试、格式/像素、最新复制取消及成功通知时机回归。

## 最终交付

- 最终便携包 `dist/Liangtu-0.7.1-Windows-x64-portable.zip`：483 个文件哈希及自带运行时校验通过，记录见 `artifacts/release-0.7.1-approved.log`。
- 最终安装包 `dist/Liangtu-0.7.1-Windows-x64-Installer-r1.zip`：重新编译并打包已批准 B 组音效的程序，记录见 `artifacts/installer-0.7.1-approved.log`。内部安装 EXE 的 SHA256 为 `0A9047F422C76E6DFAE2F46094C8C1E6192E60A64E45003C8082EF8E4A1B408E`。
- 音效确认前的临时发布目录已移到 `artifacts/pre-approved-sounds-0.7.1`，不作为交付版本。
