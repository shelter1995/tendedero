# 晾图 0.2 验证与未完成项
日期：2026-10-23。上游基准仍为 Tendedero v1.6.0。

## 已确认的使用环境
用户通过远程连接操作；本地物理屏为 3840×2160、16:9；当前远程会话实际报告 1920×1080。未修改显示设置。用户已确认先做模拟多屏拓扑，不把模拟结果计作双屏实机通过。

## 启动与远程可见性
移除了正式窗口的 WDA_EXCLUDEFROMCAPTURE。旧测试模式曾跳过这一设置，因此旧自测没有覆盖该故障。
在正式启动路径、独立测试数据下，远程画面已经看到晾衣绳；Ctrl+Alt+T 实际触发 WM_HOTKEY 并收起。注册成功记录在 artifacts/acceptance/profile/app.log。
恢复非空列表的启动展示，run.cmd 显式 --show；第二次启动通过命名事件唤起已有实例；冲突热键记录错误并提示。

## 玻璃
新增 native/GlassNative.cpp，通过 Windows Composition HostBackdropBrush 提供真实背景材质；逐个玻璃形状进行几何裁剪，WPF 叠加上游 0.75 白渐变描边、0.5 黑外描边和照片内描边。
第一种整窗 blur 试验造成整窗模糊，已放弃。当前 Composition 版本的可视区域已在远程画面确认只覆盖卡片。
原生 HWND 输入区域现在也按玻璃轮廓裁剪，拖动期间隐藏；不再假定 HTTRANSPARENT 能穿透其他进程。
Windows 与 Apple 的系统材质算法不同，仍未完成同素材、同背景的 macOS 逐像素比对，不能宣称材质输出已完全一致。

## 跨应用实际结果
用户协助手动完成真实跨窗口手势：
- Explorer：sample-0.png，返回 Move，源文件消失，列表3→2，目标 SHA-256 与原始 fixture 相同。
- 独立 WinForms 接收进程：sample-1.png，返回 Copy，sourceExists=True，文件和 Bitmap 都为640×400。接收记录在 artifacts/acceptance/receiver/received.json。
自动化工具不允许拖动终点越出源窗口，所以没有把工具内部格式检查说成真实跨应用手势。

## 输入事件与后续修复
第二次拖放后用户报告鼠标无法移出远程桌面客户端。停止两个测试进程后用户确认恢复。没有关闭远程连接，没有调用 ClipCursor 或修改远程软件设置。
已发现并修复的确定风险：
- 整屏原生玻璃 HWND 的输入区域未裁剪。
- 成功移交给 Shell 的 HBITMAP 被重复释放。
- WPF IDataObject 不支持 Shell 私有格式 SetData；改用 Shell backing store，并对非所有权转移介质调用 CopyStgMedium。
- WM_CANCELMODE 现在能请求取消 OLE 拖动，释放范围仅限自己的源窗口。
- 原生 DispatcherQueue 关闭期间泵消息；合成更新失败时隐藏玻璃层。

上述风险不能证明是远程客户端光标边界锁定的唯一原因。最终修订后没有再次进行鼠标/键盘自动化或人工跨窗口拖放，故鼠标问题的最终桌面回归仍未验收。所有测试窗口现已停止。

## 本轮检查
- 41 项核心测试：包含29组DPI/拓扑模拟，artifacts/core-v2-final.txt。
- 9 项数据行为回归：artifacts/store-parity-green.txt（独立测试，仅生成的文件）。
- 9 项无输入安全检查：artifacts/safety-v2-final-data/results.txt。
无输入检查不显示窗口，不读写用户剪贴板内容，不调用 DoDragDrop，不发送鼠标键盘事件；覆盖输入区域、Shell文件/Bitmap载荷、私有格式生命周期、预览初始化、11语言资源、正式隐藏窗口捕获标志、原生合成初始化和第二实例唤起。

## 仍未验收
- 最终修订的跨窗口鼠标操作及取消回弹实测。
- 真正两台不同 DPI 显示器、热插拔和远程断线重连。
- macOS 逐像素/逐帧对照；Windows系统字体、声音与原版的差异。
- 接收方矩阵目前只包含 Explorer 和独立 WinForms 进程，没有涵盖所有聊天、浏览器和编辑软件。
