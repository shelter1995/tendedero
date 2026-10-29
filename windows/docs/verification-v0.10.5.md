# 0.10.5：按 PicRope 修正第三方剪贴板飞入起点

## REA 与调用链

分析对象是本机已安装的 PicRope 1.2.0.0：`C:\Users\shelt\AppData\Local\Programs\PicRope\PicRope.exe`，SHA256 `A17D7A83C5D114162FB076BB50780D0EC4187ED6C6C475832DD402928C6A74B0`，MVID `58bf4f93-939a-4b0c-b5e9-839c2292a19d`。

本轮使用 REA 的 managed artifact、managed members、call_edges、field_accesses 及 Evidence 导出，并用 Mono.Cecil 对相同身份的文件作只读 CIL 反汇编。没有执行目标程序集或把推导的 C# 称为原源码。证据保存在 `artifacts/picrope-origin105/rea-evidence.json`；10 个完整方法的 CIL、token 与文件身份见同目录 `cil-manifest.json`。复现读取脚本为 `scripts/inspect-picrope-origin.ps1`。

1. `CapturePipeline.ClipboardChanged`，token `0x06000091`：在 `IL_0015` 调用 Cursor，在 `IL_0036` 写入 `clipboardOrigin`，随后设置 160ms 读取期限。
2. `<ReadClipboard>d__3a.MoveNext`，token `0x0600032c`：`IL_0039` 读取上述字段，`IL_003e` 保存为本次异步状态机的 origin；解码、写文件后，`IL_045b` 将它与图片一起传给 Arrived。
3. `DesktopController` 将 Arrived 绑定到 `ShelfWindow.ShowNewCapture`。后者 token `0x060001dd`，使用原图 PixelWidth/PixelHeight、显示器物理 Bounds 和 origin 计算起点。普通剪贴板路径没有要求观察到截图浮层或鼠标拖选。
4. `ShotFlight.Local`，token `0x06000244`，仅将矩形两端分别 PointFromScreen；物理矩形不预先再乘/除 DPI。

普通第三方图片的起点计算等价于：

```text
s = min(1, 0.7 * 屏幕宽 / 原图宽, 0.7 * 屏幕高 / 原图高)
w = 原图宽 * s; h = 原图高 * s
x = clamp(事件鼠标X - w, 屏幕左, 屏幕右 - w)
y = clamp(事件鼠标Y - h, 屏幕上, 屏幕下 - h)
```

原图像素宽高与该显示器完全相同时，PicRope 直接使用完整屏幕矩形，跳过 70% 上限。这是事件点推导的动画锚点，不是第三方工具返回的真实选区。

## 0.10.4 的具体遗漏

用户的本机日志记录：Snipaste 选区复制时只观察到 `(3147,1001)` 的同点按下/松开，剪贴板图片为 `1896×1173`，匹配器 candidate 为空。0.10.4 没有实现 PicRope 的普通剪贴板通用锚点分支，因此退回绳子下方的局部入场。上一轮仅研究了 ShowNewCapture，没有把 ClipboardChanged → 异步状态机 → 起点计算完整落到产品中。

## 本次实现

- 新增 `Windows/ClipboardFlightAnchor.cs`，按上述公式计算物理矩形。第三方纯图片在首次 await 前冻结事件鼠标点和所在显示器边界，并绑定该次剪贴板序列；读图、编码或重试后不再采样鼠标。
- 有已验证的实际选区时仍优先使用该选区；没有时，对已开启“挂上复制的图片”的第三方来源使用参考锚点。Windows 已有选区校验路径保留。
- 始终使用剪贴板原图像素尺寸，不用 600px 缩略图反推。沿用已有主题边框和独立物理坐标飞行窗口。
- 推导 origin 与 measured 标志分开；推导不冒充实测，不触发强制显示，不绕过 NewReveal、AutoHang、勿扰、全屏保护和隐私格式过滤。
- 独立复核发现两项关联回归并修复：尚未显示的推导起点可被后取得的实测选区替换；普通复制继续按 hash 去重，推导 origin 不再使普通去重失效。

## 验证

`ClipboardFlightChecks` 的无拖选 Snipaste 场景先在 0.10.4 代码失败：`artifacts/clipboard-flight105-red/results.txt`。复核指出的两项也先失败：`artifacts/clipboard-flight105-review-red/results.txt`。

最终专项 24 项通过，见 `artifacts/origin105-final-ClipboardFlightChecks/results.txt`。包括：事件点冻结、原图/缩略图区分、序列变化、70% 限制、整屏、负坐标/偏移屏幕、测量优先、隐私过滤、勿扰与开关、去重，以及真实 WPF 飞行窗口的首帧物理坐标、渲染坐标、DPI 与结束回收。首帧记录见同目录 `rendered-origin.txt`。独立只读复核确认两项问题已解决。

复跑命令：`scripts/test-feedback-motion.ps1 -RunName origin105-final`；原有显隐/布局 10 项、来源 24 项、Windows 剪贴板 26 项及本次 24 项。核心与安全日志为 `artifacts/origin105-safety-final.log`，主题飞行与动效日志为 `artifacts/origin105-MotionPolishChecks`、`artifacts/origin105-MotionPerformanceChecks`。

本轮尝试访问独立测试窗口时，电脑控制工具再次返回 `Computer Use app approval timed out`，因此未重新操作真实 Snipaste。以上区分了用户已有运行日志、发布程序静态证据和受控原生窗口验证，不声称已完成所有工具的人工操作验收。推导锚点与原始选区可以不同，这是参考算法本身的性质。

## 使用

`dist/Liangtu-0.10.5-Windows-x64-portable.zip`，先退出旧版后解压启动，或从现有托盘更新入口导入完整 ZIP。根目录 run.cmd 指向 0.10.5。0.10.4 保留，用户运行进程和设置没有被替换。
