# 0.10.6 贴图与 Esc 挂回闪烁修复

日期：2026-10-30。用户反馈长按夹子贴图、按 Esc 挂回时屏幕画面闪烁。

## 定位与修复

0.10.5 的贴图飞行使用绳子本身的透明 HWND。开始时把它由顶边 700px 高扩到整屏 2160px，结束再缩回，触发大面积原生绘图表面变化。现在使用独立、非激活的局部 `ArrivalFlight`，绳子尺寸不随贴图飞行变化。源图片姿态采用图片真实四角，保持当前主题边框和 DPI 换算。

原来的目标贴图先 Show，再将透明度改为 0；现在在 Show 前即隐藏目标并禁用输入。源卡片保留到飞行窗口跨过不同的渲染帧后才隐藏；落地先显示目标，再保留飞行两个提交帧，最后回收。

Esc 原来先 Close 贴图，再展开绳子、挂回数据、创建飞行窗口。现在 Esc、关闭叉和挂回按钮共用交接流程：源贴图继续显示，创建回挂画面并经过渲染交接后才关闭。`Store.Changed` 不再提前关闭正在 Returning 的贴图。落地也保留交叠提交帧。

跨屏 MoveTo 会取消旧显示器上的飞行，因此新的 source handoff 在迁移之后登记。手动挂回在同步 Hanging/Added 回调中保留手动语义，防止全屏保护把同一次回挂重新当成自动显示而隐藏窗口；DPI 维护只保留仍展开、仍处于同一显示器的手动状态。本次作用域结束即清除，后续普通挂图仍受原全屏保护。

完成和取消均只调用一次完成函数；隐藏、显示器变更、勿扰及关闭释放窗口、源贴图和渲染订阅。原文件、篮子、贴图尺寸、声音和截图来源规则保留。

## 失败到通过的证据

| 受控原生窗口指标 | 修复前实现 | 修复后 |
|---|---:|---:|
| 贴图时绳子高度 | 700 → 2160px | 始终不超过 700px |
| Esc 后源图/飞行/目标均不可见的帧 | 4 | 0 |
| 贴图逐帧可见状态空档 | 未作准确旧版统计 | 0 |

最初用“动画尚未结束”判断目标是否可见，报告了16帧，包含落地图片已经显示的误判。判定改为检查实际窗口可见性和 frame.Opacity 后，使用修复前源文件独立构建，仍复现4帧空档。准确基线：`artifacts/pin-handoff-baseline-corrected/results.txt`；基线工程与冻结测试源在 `artifacts/pin-handoff-baseline`，修复前实现文件在 `artifacts/pre-pin-handoff106`。

跨屏、全屏和 DPI 维护的失败记录分别在 `artifacts/pin-handoff-migration-red`、`artifacts/pin-handoff-fullscreen-red`、`artifacts/pin-handoff-dpi-red`。完成回调重复的问题先失败于 `artifacts/pin-handoff-completion-red`，修正后通过。

最终专项：`artifacts/pin106-final-PinHandoffChecks/results.txt`，20项通过。覆盖生产 App.Pin、Esc 按键路由、逐帧源/飞行/目标状态、Show 前透明度、落地清理、回调一次、取消、跨屏、全屏与 DPI 维护、后续自动挂图保护和原文件保留。独立只读复核确认已报告问题解决，未发现剩余 Critical / Important。

其余回归：显隐/布局10项、选区24项、Windows剪贴板26项、第三方起点24项、核心与安全180项、篮子37项、主题飞行67项、动效与素材生命周期28项，全部通过。总计416项。命令与日志：

- `scripts/test-feedback-motion.ps1 -RunName pin106-final` → `artifacts/pin106-motion-regressions.log`
- `test-safe.ps1` → `artifacts/pin106-safety.log`
- `artifacts/pin106-BasketChecks`、`artifacts/pin106-MotionPolishChecks`、`artifacts/pin106-MotionPerformanceChecks`

本机本轮12图压力检查帧间隔中位约17.26ms、p95约34.65ms，存在慢帧；本轮不宣称所有动画均恒定60fps。

这些是实际 WPF 窗口、HWND 尺寸和 Rendering 可见状态检查。Esc 通过窗口按键路由触发，未注入系统键盘输入；长按夹子后调用的生产贴图入口被直接执行。没有用屏幕录像重新验收用户的原始桌面、所有显卡/远程桌面和真实物理多屏，因此不把受控检测写成全部环境实屏通过。

## 使用

候选包：`dist/Liangtu-0.10.6-Windows-x64-portable.zip`。先从托盘退出旧版，再解压运行，或通过原更新入口导入完整ZIP。根目录 run.cmd 指向0.10.6。未替换用户运行中的进程或用户设置；0.10.5目录保留。
