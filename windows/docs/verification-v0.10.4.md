# 0.10.4 动画运行反馈修复

日期：2026-10-28。此版本为本地修复候选；真实 Windows 截图工具、Snipaste 操作验收尚未完成，不能称所有截图工具均稳定。

## 已定位与修改

1. `LineMotion` 的新时钟在首次 WPF 渲染前显示终点值：展开首帧为 0，反向时从 -111 突然跳到 0。现在附加时钟后立即采样时间零，起点与当前呈现值连续。
2. 自动通知重复调用 `Reveal` 会重新启动动画，并重置手动打开、指针进入状态。现在同一目标仅延长停留；自动通知不撤销已排队的手动收起，第二次手动切换可取消排队收起。
3. 传输过程中 `Retract` 直接返回并丢弃请求。现在等待传输完成后执行；隐藏依据实际动画完成回调，旧回调以版本号失效，替代固定 300ms 延时。
4. 第三方图片在进入选区匹配器前被 Windows 专用进程判断排除。现在观察第三方无边框置顶全屏选区，同进程剪贴板来源、序列及物理尺寸验证通过后复用带主题边框的飞入。按键取消、尺寸不符、来源改变不借用坐标。鼠标松开时浮层消失不再丢掉已测量选区；框选后点击复制工具条保留刚完成的选区。
5. 棉麻篮子原来显式设置成上下两行，预览高度固定 178。现在左侧图库网格、右侧大图/详情/操作，保留筛选与选择。
6. 桌面贴图原来把缩略图宽度限制到 280–520 DIP。现在显示前读取原图尺寸，按目标屏幕 DPI 以接近 1:1 像素显示，大图等比限制在工作区约 80%，原图异步载入不重置用户缩放。1800×1000、200% 屏幕的窗口宽度由 520 增至约 924 DIP。
7. 根目录 `run.cmd` 原来指向 0.9.4，现指向 0.10.4；启动日志增加实际程序集版本，便于辨认运行包。

## 验证证据

- 首轮运行回归 8 项中 7 项失败，证据 `artifacts/feedback104-red/results.txt`。首帧、反向、手动状态、丢失收起、第三方匹配入口、棉麻分栏及贴图尺寸均被覆盖。
- 复制工具条点击的新增测试先失败，修复后通过：`artifacts/feedback104-origin-toolbar-red/results.txt` 与最终来源测试。
- 独立代码复核指出自动通知可能清除待收起请求，新增两项实际失败回归：`artifacts/feedback104-toggle-review-red/results.txt`；修复后通过。最终只读复核无剩余 Critical / Important 发现。
- 最新专项：`scripts/test-feedback-motion.ps1 -RunName feedback104-release-final`，显隐/布局/贴图 10 项、来源 24 项、剪贴板发布 26 项。结果分别写入 `artifacts/feedback104-release-final-*/results.txt`。额外覆盖收起途中显示器/DPI布局变化，检查原生窗口确实隐藏。
- 核心与安全 180 项：`artifacts/feedback104-safety-final.log`。篮子 37 项、主题飞行/透明窗 67 项、动效与生命周期 28 项，见 `artifacts/feedback104-BasketChecks`、`artifacts/feedback104-MotionPolishChecks`、`artifacts/feedback104-MotionPerformanceChecks`。
- 本机 WPF 动画窗口运行：12 图中位帧间隔约 13.13ms、p95 14.95ms；棉麻飞入中位 8.71ms、p95 13.18ms；柔彩飞入中位 8.88ms、p95 13.96ms。保留硬件渲染，重排测试 180 帧无逐帧 Canvas 布局。该受控窗口结果不代替真实截图工具验收。
- 棉麻分栏与贴图实窗渲染已查看：`artifacts/feedback104-release-final-FeedbackMotionChecks/linen-basket.png` 与 `readable-pin.png`。使用独立合成图片，未读取用户图库作测试。

## 参考与适用限制

REA 检查本机 PicRope 1.2.0.0，SHA256 为 `a17d7a83c5d114162fb076bb50780d0ec4187ed6c6c475832dd402928c6a74b0`。只读 IL 显示其 `ShowNewCapture` 接受一个 Point，再减图片尺寸、按屏幕边界限制成起始矩形；这说明其跨工具动画触发方式，但不足以证明取到了真实框选矩形。本轮继续使用实际观察和尺寸校验，不把当前光标推算成已知截图位置。

第三方工具仍须开启“挂上复制的图片”，原位匹配还需开启“接管截图”。不能关联到已观察选区的图片使用普通入场；窗口/全屏截图、选区调整及非标准浮层未声称全覆盖。游戏勿扰、自动挂图开关和全屏保护继续优先。

电脑控制工具启动已安装的 Windows 截图工具时返回 `Computer Use app approval timed out`。因此本轮未实际操作 Windows 截图工具和 Snipaste；没有把测试夹具、合成剪贴板对象或 WPF 窗口的运行结果写成真实工具通过。实际多屏和各第三方编码组合也仍待验收。

## 使用

修复候选：`dist/Liangtu-0.10.4-Windows-x64-portable.zip`。先退出旧版，再解压启动，或在现有托盘更新入口导入完整 ZIP。没有修改用户设置或替换正在运行的进程。包内版本、哈希、运行时和原生 DLL 检查结果见 `artifacts/portable-verification.txt`。
