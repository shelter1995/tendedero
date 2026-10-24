# 0.6.4 卡片左上角点击修复

日期：2026-10-24。

## 原因与修复

卡片左上角控件 cross 与图片边框 frame 是同一个 Grid 下的兄弟控件。鼠标按下时调用 HitSurface.Contains(cross, frame, ...)；旧实现使用 TransformToAncestor(frame)，因 frame 不是 cross 的祖先而抛出 InvalidOperationException。异常被捕获后返回 false，导致按钮始终无法命中。

改用 TransformToVisual(relativeTo)，允许在兄弟控件和祖先控件之间转换坐标，继续使用控件实际尺寸和圆角轮廓检测。没有改变按钮图形、手势或文件处理逻辑。

用户确认保留原版行为：收件箱图片移到回收站，外部图片只从晾衣绳取下。

## 验证

- 修复前，新增回归在叉号中心失败：`Corner center missed at size=0.8, tilt=-8`，记录见 `artifacts/corner-hit-red.log`。
- 修复后，`test-safe.ps1` 成功结束，界面安全回归 77 项全部通过，记录见 `artifacts/corner-hit-green.log` 与 `artifacts/safety-v2-final-data/results.txt`。
- 新增测试使用实际 Card 控件，覆盖三档大小、三种倾斜角、图片悬停缩放及叉号缩放；验证中心命中、圆形边角和外部不命中，以及原有祖先坐标检测仍然有效。
- 本轮验证为离屏控件测试，未注入真实鼠标输入，也未执行真实文件回收操作。

## 运行

退出旧版后，使用根目录 `run.cmd` 或 `dist/Liangtu-0.6.4/Start.cmd`。安装程序仍为之前的 0.6.3，本轮提供新的 0.6.4 便携包。
